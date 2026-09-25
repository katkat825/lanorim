using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Combat
{
    // THE COMBAT COMMAND API - the layer the combat UX calls (docs/combat_ux.md). The screen asks
    // what the hero can do, the player picks one, the screen asks who or where it may go and what
    // it would do there, and the player confirms or cancels. Nothing here draws, and nothing here
    // waits on a click: the questions it cannot answer itself (a reaction set to "ask", a die on
    // the felt) go out through the asker and the dice source, which the table implements.
    //
    // ASSUMPTIONS, written down (they are also in the run log):
    // - one hero, driven by the player; every other actor has a brain. a Friendly actor with no
    //   brain simply ends its turn
    // - the hero's rolls are the physical dice when the table says so (TableResolver); the GM's
    //   are behind the screen
    // - a preview is the rules' own arithmetic on the numbers as they stand now; it cannot see a
    //   reaction the target has not spent yet (a Shield), and says so by being a chance, not a
    //   promise
    // - Q and E rotate a line or cone a quarter turn; the square the mouse is over also points it
    //   (Template.Toward), and the confirmed facing is whichever was last set
    public enum SessionPhase
    {
        // the hero's turn, nothing picked
        Choosing,

        // an option picked that needs a target, a square or a facing
        Targeting,

        // it is somebody else's turn (the session plays them; this is seen only mid-play)
        Waiting,

        Over,
    }

    public enum Targeting
    {
        None,
        Creature,
        Creatures,
        Square,
        Direction,
    }

    public enum OptionKind
    {
        Attack,
        Spell,
        Again,
        Dash,
        Disengage,
        Hide,
        StandUp,
        BreakFree,
        Shake,
        Item,
        Feature,
        Shape,
        Surge,
        Flee,
        EndTurn,
    }

    // ONE BUTTON ON THE ACTION BAR: what it is, what it costs, whether it can be pressed now and,
    // when it cannot, why - as a key the tooltip shows
    public sealed class ActionOption
    {
        public string Id { get; init; } = "";

        public OptionKind Kind { get; init; }

        public string NameKey { get; init; } = "";

        public Spend Cost { get; init; } = Spend.Action;

        public bool Enabled { get; init; }

        public string WhyNotKey { get; init; }

        // 1 to 9 on the keyboard, 0 for the ones past nine (reached by Tab and the mouse)
        public int Hotkey { get; set; }

        public Targeting Targeting { get; init; }

        public int MaxTargets { get; init; } = 1;

        public IReadOnlyList<string> Modes { get; init; } = Array.Empty<string>();

        public IReadOnlyList<DamageType> DamageChoices { get; init; } = Array.Empty<DamageType>();

        public Attack Attack { get; init; }

        public Spell Spell { get; init; }

        public Feature Feature { get; init; }

        public Form Form { get; init; }

        public Item Item { get; init; }

        public override string ToString() =>
            $"{Hotkey}: {Id} ({Cost.ToString().ToLowerInvariant()})" + (Enabled ? "" : $" - {WhyNotKey}");
    }

    // WHAT A CHOICE WOULD DO, before it is made: the hit chance and expected damage, or the save
    // and whether a success halves it, and the squares a template would cover
    public sealed class Preview
    {
        public bool Legal { get; init; }

        public string WhyNotKey { get; init; }

        public IReadOnlyList<Actor> Targets { get; init; } = Array.Empty<Actor>();

        // 0 to 1; -1 when there is no attack roll
        public double HitChance { get; init; } = -1;

        public int? SaveDc { get; init; }

        public Ability? SaveAbility { get; init; }

        public bool HalfOnSave { get; init; }

        // the chance the (first) target fails its save
        public double FailChance { get; init; } = -1;

        public DiceRoll Damage { get; init; }

        public double ExpectedDamage { get; init; }

        public IReadOnlyList<Cell> Template { get; init; } = Array.Empty<Cell>();

        public Facing? Facing { get; init; }
    }

    public sealed class ActionResult
    {
        public bool Done { get; init; }

        public string WhyNotKey { get; init; }

        public Blow Blow { get; init; }

        public Casting Casting { get; init; }

        public IReadOnlyList<Cell> Walked { get; init; } = Array.Empty<Cell>();

        public Attempt Attempt { get; init; }

        public static ActionResult No(string why) => new ActionResult { Done = false, WhyNotKey = why };
    }

    public sealed class CombatSession
    {
        readonly ItemShelf _shelf;
        readonly FormShelf _forms;

        public CombatSession(Battle battle, ItemShelf shelf = null, FormShelf forms = null)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _shelf = shelf;
            _forms = forms;
        }

        public Battle Battle { get; }

        public Encounter Fight => Battle.Fight;

        public Hero Hero => Battle.Hero;

        public Turn Turn { get; private set; }

        public SessionPhase Phase { get; private set; } = SessionPhase.Waiting;

        public Outcome Outcome => Fight.Outcome;

        public static string Why(string reason) =>
            KeyConventions.Key(KeyConventions.UiNs, "combat_why", reason, "name");

        public static IEnumerable<string> Keys() =>
            new[] { "no_action", "no_bonus", "no_reaction", "cannot_act", "out_of_range", "not_seen",
                    "no_target", "spent", "not_on_your_turn", "rooted", "too_far", "pinned",
                    "nothing_to_break", "nobody_to_shake", "no_uses", "charmed", "not_an_edge",
                    "shifted", "no_surge", "choose_mode", "choose_type" }
                .Select(Why);

        // --- the flow -----------------------------------------------------------------------------

        // plays everyone before the hero, and stops on the hero's turn (or the end)
        public void Start() => PlayUntilTheHero();

        public ActionResult EndTurn()
        {
            if (Phase == SessionPhase.Over || Turn == null) return ActionResult.No(Why("not_on_your_turn"));

            Selected = null;
            Fight.EndTurn();
            PlayUntilTheHero();

            return new ActionResult { Done = true };
        }

        void PlayUntilTheHero()
        {
            Turn = null;
            Phase = SessionPhase.Waiting;

            while (true)
            {
                Turn next = Fight.Next();

                if (next == null || Fight.Over)
                {
                    Phase = SessionPhase.Over;
                    return;
                }

                if (ReferenceEquals(next.Actor, Hero.Actor))
                {
                    Turn = next;
                    Phase = SessionPhase.Choosing;
                    return;
                }

                ITactics brain = Battle.BrainOf(next.Actor);

                if (brain != null) brain.Take(Fight, next);

                if (!next.Ended && !Fight.Over) Fight.EndTurn();

                // concentration the monster's turn broke (a hero knocked out, a caster downed)
                Battle.Magic.Check(Fight.Actors);
            }
        }

        bool MyTurn => Phase != SessionPhase.Over && Turn != null && !Turn.Ended &&
                       ReferenceEquals(Turn.Actor, Hero.Actor);


        // --- what the hero can do --------------------------------------------------------------------

        public IReadOnlyList<ActionOption> Options()
        {
            var options = new List<ActionOption>();

            if (!MyTurn) return options;

            Actor me = Hero.Actor;

            foreach (Attack attack in Hero.Attacks)
                options.Add(new ActionOption
                {
                    Id = "attack:" + attack.Id, Kind = OptionKind.Attack, NameKey = attack.NameKey,
                    Cost = Spend.Action, Attack = attack, Targeting = Targeting.Creature,
                    Enabled = WhyNotAttack(attack) == null, WhyNotKey = WhyNotAttack(attack),
                });

            if (Hero.Caster != null)
            {
                foreach (Spell spell in Hero.Caster.Known.Where(s => !s.Answers))
                {
                    string why = WhyNotSpell(spell);

                    options.Add(new ActionOption
                    {
                        Id = "spell:" + spell.Id, Kind = OptionKind.Spell, NameKey = spell.NameKey,
                        Cost = spell.CastingTime == CastingTime.BonusAction ? Spend.Bonus : Spend.Action,
                        Spell = spell, Targeting = TargetingOf(spell, false),
                        MaxTargets = MaxTargets(spell, spell.Level),
                        Modes = spell.Modes,
                        DamageChoices = spell.Effects.SelectMany(e => e.DamageChoices).Distinct().ToList(),
                        Enabled = why == null, WhyNotKey = why,
                    });
                }

                foreach (Spell held in Hero.Caster.Known.Where(s => s.Repeat.HasValue &&
                                                                   Battle.Magic.CanRepeat(Hero.Caster, s)))
                {
                    Spend cost = held.Repeat == CastingTime.BonusAction ? Spend.Bonus : Spend.Action;
                    string why = Turn.Can(cost) ? null : Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

                    options.Add(new ActionOption
                    {
                        Id = "again:" + held.Id, Kind = OptionKind.Again, NameKey = held.NameKey,
                        Cost = cost, Spell = held, Targeting = TargetingOf(held, true),
                        Enabled = why == null, WhyNotKey = why,
                    });
                }
            }

            options.Add(Simple("dash", OptionKind.Dash, Spend.Action));
            options.Add(Simple("disengage", OptionKind.Disengage, Spend.Action));
            options.Add(Simple("hide", OptionKind.Hide, Spend.Action));

            // the Rogue's Cunning Action: the same three for a bonus action
            foreach ((string id, OptionKind kind, Manoeuvre m) in new[]
                     {
                         ("dash", OptionKind.Dash, Manoeuvre.Dash),
                         ("disengage", OptionKind.Disengage, Manoeuvre.Disengage),
                         ("hide", OptionKind.Hide, Manoeuvre.Hide),
                     })
                if (me.QuickOnBonus.HasFlag(m))
                    options.Add(Simple(id, kind, Spend.Bonus, "bonus_" + id));

            if (me.Has(Condition.Prone))
                options.Add(new ActionOption
                {
                    Id = "stand_up", Kind = OptionKind.StandUp, NameKey = UiName("stand_up"),
                    Cost = Spend.Movement,
                    Enabled = !me.IsPinned(Condition.Prone),
                    WhyNotKey = me.IsPinned(Condition.Prone) ? Why("pinned") : null,
                });

            if (me.Has(Condition.Restrained) || me.Has(Condition.Grappled))
                options.Add(Simple("break_free", OptionKind.BreakFree, Spend.Action));

            if (Fight.Field.Where(me) is Cell here &&
                Fight.Field.Adjacent(here).Any(a => Battle.Magic.CanBeShaken(a)))
                options.Add(Simple("shake", OptionKind.Shake, Spend.Action));

            foreach (Stack stack in Hero.Pack.Stacks.Where(s => s.Item.Kind == ItemKind.Consumable &&
                                                                !s.Item.Heals.IsNothing))
                options.Add(new ActionOption
                {
                    Id = "item:" + stack.Item.Id, Kind = OptionKind.Item, NameKey = stack.Item.NameKey,
                    Cost = stack.Item.UseTime, Item = stack.Item,
                    Enabled = Turn.Can(stack.Item.UseTime),
                    WhyNotKey = Turn.Can(stack.Item.UseTime) ? null : Why("no_bonus"),
                });

            foreach (Feature feature in Hero.Activatable.Where(f => f.Trait == Trait.Stance ||
                                                                    f.Trait == Trait.Recovery))
            {
                bool uses = Hero.UsesLeft(feature) > 0;
                bool bonus = Turn.Can(Spend.Bonus);

                options.Add(new ActionOption
                {
                    Id = "feature:" + feature.Id, Kind = OptionKind.Feature, NameKey = feature.NameKey,
                    Cost = Spend.Bonus, Feature = feature,
                    Enabled = uses && bonus,
                    WhyNotKey = !uses ? Why("no_uses") : !bonus ? Why("no_bonus") : null,
                });
            }

            foreach (Form form in Hero.FormsOpen(_forms))
            {
                string refused = Hero.RefusesShape(form, Turn);

                options.Add(new ActionOption
                {
                    Id = "shape:" + form.Id, Kind = OptionKind.Shape, NameKey = form.NameKey,
                    Cost = Spend.Bonus, Form = form,
                    Enabled = refused == null, WhyNotKey = refused == null ? null : Why("no_uses"),
                });
            }

            if (Hero.Budget.ExtraActionsLeft > 0)
                options.Add(new ActionOption
                {
                    Id = "surge", Kind = OptionKind.Surge, NameKey = UiName("surge"), Cost = Spend.Free,
                    Enabled = true,
                });

            if (Fight.CanFlee(Turn))
                options.Add(new ActionOption
                {
                    Id = "flee", Kind = OptionKind.Flee, NameKey = UiName("flee"), Cost = Spend.Free,
                    Enabled = true,
                });

            options.Add(new ActionOption
            {
                Id = "end_turn", Kind = OptionKind.EndTurn, NameKey = UiName("end_turn"),
                Cost = Spend.Free, Enabled = true,
            });

            // 1 to 9 for the first nine that do something - attacks, then spells, then the rest
            int key = 1;

            foreach (ActionOption option in options.Where(o => o.Kind != OptionKind.EndTurn))
                option.Hotkey = key <= 9 ? key++ : 0;

            return options;
        }

        static string UiName(string id) => KeyConventions.Key(KeyConventions.UiNs, "combat_action", id, "name");

        public static IEnumerable<string> ActionKeys() =>
            new[] { "dash", "disengage", "hide", "bonus_dash", "bonus_disengage", "bonus_hide",
                    "stand_up", "break_free", "shake", "surge", "flee", "end_turn" }
                .Select(UiName)
                .Concat(Keys())
                .Concat(ReactionPolicies.Keys());

        ActionOption Simple(string id, OptionKind kind, Spend cost, string name = null)
        {
            string why = Turn.Can(cost) ? null : Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

            if (!Hero.Actor.CanAct) why = Why("cannot_act");

            return new ActionOption
            {
                Id = (cost == Spend.Bonus ? "bonus_" : "") + id, Kind = kind,
                NameKey = UiName(name ?? id), Cost = cost, Enabled = why == null, WhyNotKey = why,
            };
        }

        string WhyNotAttack(Attack attack)
        {
            Actor me = Hero.Actor;

            if (!me.CanAct) return Why("cannot_act");

            if (!Turn.Can(Spend.Action) && Turn.Limited <= 0) return Why("no_action");

            if (!me.CanUse(attack)) return Why("no_target");

            if (!Fight.Field.Enemies(me).Any(e => !e.IsDown && Fight.Field.InRange(me, e, attack.Reaches)))
                return Why("out_of_range");

            return null;
        }

        string WhyNotSpell(Spell spell)
        {
            if (!Hero.Actor.CanAct) return Why("cannot_act");

            if (Hero.Actor.IsShifted) return Why("shifted");

            if (!Hero.Caster.CanCast(spell, spell.Level)) return Why("spent");

            Spend cost = spell.CastingTime == CastingTime.BonusAction ? Spend.Bonus : Spend.Action;

            if (!Turn.Can(cost)) return Why(cost == Spend.Bonus ? "no_bonus" : "no_action");

            return null;
        }

        static Targeting TargetingOf(Spell spell, bool again)
        {
            IEnumerable<SpellEffect> effects = spell.Effects.Where(e => again ? e.Repeats : !e.RepeatOnly);

            if (effects.Any(e => e.Reach.IsDirected())) return Targeting.Direction;

            if (effects.Any(e => e.Reach.NeedsATargetSquare() ||
                                 e.Kind == Primitive.Shift && e.Reach == Reach.Caster ||
                                 e.Kind == Primitive.Shift && e.Reach == Reach.Zone))
                return Targeting.Square;

            if (effects.Any(e => e.Reach == Reach.Creatures)) return Targeting.Creatures;

            if (effects.Any(e => e.Reach == Reach.Creature)) return Targeting.Creature;

            return Targeting.None;
        }

        static int MaxTargets(Spell spell, int castAt) =>
            spell.Effects.Where(e => e.Reach == Reach.Creatures)
                 .Select(e => e.TargetsAt(spell.Level, castAt))
                 .DefaultIfEmpty(1)
                 .Max();


        // --- picking one ----------------------------------------------------------------------------

        public ActionOption Selected { get; private set; }

        public string Mode { get; private set; }

        public DamageType DamageType { get; private set; }

        public int CastAt { get; private set; }

        public Facing Facing { get; private set; } = Facing.East;

        public Side Side { get; private set; } = Side.Left;

        public bool Select(ActionOption option, string mode = null,
                           DamageType damageType = DamageType.None, int castAt = -1)
        {
            if (!MyTurn || option == null || !option.Enabled) return false;

            // a thing with nothing to aim at happens straight away
            if (option.Targeting == Targeting.None && option.Kind != OptionKind.Spell &&
                option.Kind != OptionKind.Again)
            {
                Selected = option;
                return true;
            }

            Selected = option;
            Mode = mode ?? option.Modes.FirstOrDefault();
            DamageType = damageType != DamageType.None
                ? damageType
                : option.DamageChoices.FirstOrDefault();
            CastAt = option.Spell == null ? 0 : Math.Max(option.Spell.Level, castAt);
            Phase = SessionPhase.Targeting;

            return true;
        }

        public void Cancel()
        {
            Selected = null;
            Mode = null;
            DamageType = DamageType.None;

            if (MyTurn) Phase = SessionPhase.Choosing;
        }

        // Q and E, or the scroll wheel
        public Facing Rotate(int quarters)
        {
            int f = ((int)Facing + quarters) % 4;
            Facing = (Facing)(f < 0 ? f + 4 : f);
            return Facing;
        }

        public void Point(Facing facing) => Facing = facing;

        public void Pick(Side side) => Side = side;

        // who the selected option may go at
        public IReadOnlyList<Actor> LegalTargets()
        {
            if (Selected == null || !MyTurn) return Array.Empty<Actor>();

            Actor me = Hero.Actor;

            if (Selected.Kind == OptionKind.Attack)
                return Fight.Field.Enemies(me)
                            .Where(e => !e.IsDown && Fight.Field.InRange(me, e, Selected.Attack.Reaches) &&
                                        !me.HasFrom(Condition.Charmed, e))
                            .OrderBy(e => Fight.Field.Distance(me, e))
                            .ThenBy(e => e.Id, StringComparer.Ordinal)
                            .ToList();

            if (Selected.Spell == null) return Array.Empty<Actor>();

            int range = Math.Max(1, Selected.Spell.RangeAt(me.Level));
            bool kind = Kindly(Selected.Spell);

            // a kindly spell goes on friends (and the caster); a harmful one on foes
            return Fight.Actors.Where(a => kind ? a.Side == me.Side : a.Side != me.Side)
                        .Where(a => ReferenceEquals(a, me) && kind ||
                                    Fight.Field.InRange(me, a, range) && Fight.Sees(me, a))
                        .Where(a => kind || !a.IsDown || Selected.Spell.Does(Primitive.Stabilize))
                        .OrderBy(a => Fight.Field.Distance(me, a))
                        .ThenBy(a => a.Id, StringComparer.Ordinal)
                        .ToList();
        }

        // a spell for friends: healing, a ward, a boon with no save
        static bool Kindly(Spell spell) =>
            spell.Does(Primitive.Heal) || spell.Does(Primitive.Ward) ||
            spell.Does(Primitive.Stabilize) || spell.Does(Primitive.Relieve) ||
            spell.Effects.All(e => e.Kind == Primitive.Sway && !e.Save.HasValue ||
                                   e.Kind == Primitive.Narrate || e.Kind == Primitive.Reveal ||
                                   e.Kind == Primitive.Illuminate || e.Kind == Primitive.Conjure);

        // where a square-aimed option may be put
        public IReadOnlyList<Cell> LegalSquares()
        {
            if (Selected?.Spell == null || !MyTurn || !(Fight.Field.Where(Hero.Actor) is Cell here))
                return Array.Empty<Cell>();

            int range = Math.Max(1, Selected.Spell.RangeAt(Hero.Actor.Level));

            return Fight.Field.Map.Cells
                        .Where(c => Battlefield.Distance(here, c) <= range && Fight.Field.CanSee(here, c))
                        .ToList();
        }


        // --- previews ----------------------------------------------------------------------------

        public Preview Preview(Actor target) => Preview(new[] { target }, null);

        public Preview Preview(IReadOnlyList<Actor> targets, Cell? square)
        {
            if (Selected == null || !MyTurn) return new Preview { Legal = false, WhyNotKey = Why("no_target") };

            Actor me = Hero.Actor;
            Actor first = targets?.FirstOrDefault(t => t != null);

            if (Selected.Kind == OptionKind.Attack)
            {
                if (first == null) return new Preview { Legal = false, WhyNotKey = Why("no_target") };

                bool legal = LegalTargets().Contains(first);
                Attack attack = Selected.Attack;

                bool close = Fight.Field.Distance(me, first) <= 1;
                Advantage lean = Strike.Lean(me, first, close, Fight.Sees(me, first), Fight.Sees(first, me),
                                             Fight.Band(me, first, attack));
                int cover = Fight.Cover(me, first);

                double hit = AttackChance(attack.Modifier(me), first.ArmorClass + cover, lean);
                DiceRoll swing = attack.DamageFor(me);

                return new Preview
                {
                    Legal = legal, WhyNotKey = legal ? null : Why("out_of_range"),
                    Targets = new[] { first }, HitChance = hit, Damage = swing,
                    ExpectedDamage = hit * Math.Max(0, swing.Average),
                };
            }

            Spell spell = Selected.Spell;

            if (spell == null) return new Preview { Legal = true };

            bool again = Selected.Kind == OptionKind.Again;
            List<SpellEffect> effects = spell.Effects
                                             .Where(e => again ? e.Repeats : !e.RepeatOnly)
                                             .Where(e => e.Mode.Length == 0 || e.Mode == Mode)
                                             .ToList();

            IReadOnlyList<Cell> template = Template(spell, effects, square);

            IReadOnlyList<Actor> caught = Selected.Targeting == Targeting.Direction ||
                                          Selected.Targeting == Targeting.Square
                ? Fight.Field.Caught(template).ToList()
                : (targets ?? Array.Empty<Actor>()).Where(t => t != null).ToList();

            Actor about = caught.FirstOrDefault(a => a.Side != me.Side) ?? caught.FirstOrDefault();

            SpellEffect damage = effects.FirstOrDefault(e => e.Kind == Primitive.Damage);
            SpellEffect saved = effects.FirstOrDefault(e => e.Save.HasValue);
            SpellEffect rolled = effects.FirstOrDefault(e => e.AttackRoll);

            DiceRoll dice = damage?.AmountAt(spell.Level, Math.Max(CastAt, spell.Level), me.Level) ?? default;

            double hitChance = -1, fail = -1, expected = 0;

            if (about != null && rolled != null)
            {
                bool close = Fight.Field.Distance(me, about) <= 1;
                Advantage lean = Strike.Lean(me, about, close, Fight.Sees(me, about), Fight.Sees(about, me));
                hitChance = AttackChance(Hero.Caster.AttackModifier, about.ArmorClass + Fight.Cover(me, about),
                                         lean);
                expected = hitChance * Math.Max(0, dice.Average);
            }
            else if (about != null && saved != null)
            {
                fail = SaveFailChance(about.SaveModifier(saved.Save.Value), Hero.Caster.SaveDc,
                                      about.SaveAdvantage(saved.Save.Value));
                expected = Math.Max(0, dice.Average) *
                           (fail + (saved.OnSave == OnSave.Half ? (1 - fail) * 0.5 : 0));
            }
            else
            {
                expected = Math.Max(0, dice.Average);
            }

            // a template that catches no foe promises nothing - the HUD must not show a cone into
            // empty air as its full damage
            bool templated = Selected.Targeting == Targeting.Direction || Selected.Targeting == Targeting.Square;
            int enemies = templated
                ? caught.Count(a => a.Side != me.Side)
                : Math.Max(1, caught.Count(a => a.Side != me.Side));

            return new Preview
            {
                Legal = LegalAim(spell, targets, square),
                Targets = caught,
                HitChance = hitChance,
                SaveDc = saved != null ? Hero.Caster.SaveDc : null,
                SaveAbility = saved?.Save,
                HalfOnSave = saved?.OnSave == OnSave.Half,
                FailChance = fail,
                Damage = dice,
                ExpectedDamage = expected * (Selected.Targeting == Targeting.Creature ? 1 : enemies),
                Template = template,
                Facing = Selected.Targeting == Targeting.Direction ? Facing : null,
            };
        }

        bool LegalAim(Spell spell, IReadOnlyList<Actor> targets, Cell? square)
        {
            switch (Selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    return targets != null && targets.Count > 0 && targets.All(t => LegalTargets().Contains(t));

                case Targeting.Square:
                    return square.HasValue && LegalSquares().Contains(square.Value);

                default:
                    return true;
            }
        }

        // the squares a spell's shape would cover for this aim - what the board lights up under
        // the mouse
        IReadOnlyList<Cell> Template(Spell spell, IReadOnlyList<SpellEffect> effects, Cell? square)
        {
            if (!(Fight.Field.Where(Hero.Actor) is Cell here)) return Array.Empty<Cell>();

            var cells = new List<Cell>();

            foreach (SpellEffect e in effects)
            {
                switch (e.Reach)
                {
                    case Reach.Line:
                        cells.AddRange(Fight.Field.Line(here, Facing, e.Length, Math.Max(1, e.Width)));
                        break;
                    case Reach.Cone:
                        cells.AddRange(Fight.Field.Cone(here, Facing, e.Length));
                        break;
                    case Reach.Cube:
                        cells.AddRange(Fight.Field.Cube(here, Facing, e.Length));
                        break;
                    case Reach.Burst when square.HasValue:
                        cells.AddRange(Fight.Field.Burst(square.Value, e.Radius));
                        break;
                    case Reach.Square when square.HasValue:
                        cells.AddRange(Fight.Field.Square(square.Value, Math.Max(1, e.Length)));
                        break;
                    case Reach.Around:
                        cells.AddRange(Fight.Field.Burst(here, e.Radius));
                        break;
                    case Reach.Place when square.HasValue && e.Kind == Primitive.Zone:
                        cells.AddRange(Fight.Field.Burst(square.Value, e.Radius));
                        break;
                }
            }

            return cells.Distinct().ToList();
        }

        // SRD: a natural 1 misses and a natural 20 hits, whatever the numbers
        public static double AttackChance(int modifier, int armorClass, Advantage advantage)
        {
            double p = Math.Clamp((21 - (armorClass - modifier)) / 20.0, 0.05, 0.95);

            return Lean(p, advantage);
        }

        // a save has no automatic success or failure in v1 (Attempt's rule)
        public static double SaveFailChance(int modifier, int dc, Advantage advantage)
        {
            double pass = Math.Clamp((21 - (dc - modifier)) / 20.0, 0, 1);

            return 1 - Lean(pass, advantage);
        }

        static double Lean(double p, Advantage advantage) => advantage switch
        {
            Advantage.Advantage => 1 - (1 - p) * (1 - p),
            Advantage.Disadvantage => p * p,
            _ => p,
        };


        // --- doing it ----------------------------------------------------------------------------

        public ActionResult Confirm(Actor target) =>
            Confirm(target == null ? Array.Empty<Actor>() : new[] { target }, null);

        public ActionResult Confirm(Cell square) => Confirm(Array.Empty<Actor>(), square);

        // a line, a cone, or anything with nothing to aim at
        public ActionResult Confirm() => Confirm(Array.Empty<Actor>(), null);

        public ActionResult Confirm(IReadOnlyList<Actor> targets, Cell? square)
        {
            if (!MyTurn) return ActionResult.No(Why("not_on_your_turn"));

            ActionOption option = Selected;

            if (option == null) return ActionResult.No(Why("no_target"));

            ActionResult result = Do(option, targets ?? Array.Empty<Actor>(), square);

            if (result.Done)
            {
                Selected = null;
                Mode = null;
                DamageType = DamageType.None;

                Battle.Magic.Check(Fight.Actors);
                Fight.Judge();

                if (Fight.Over) Phase = SessionPhase.Over;
                else if (MyTurn) Phase = SessionPhase.Choosing;
            }

            return result;
        }

        // pick and do in one - what a hotkey on an option with nothing to aim at does
        public ActionResult Take(ActionOption option)
        {
            if (option?.Kind == OptionKind.EndTurn) return EndTurn();

            if (!Select(option)) return ActionResult.No(option?.WhyNotKey ?? Why("no_target"));

            return Confirm();
        }

        ActionResult Do(ActionOption option, IReadOnlyList<Actor> targets, Cell? square)
        {
            Actor me = Hero.Actor;

            switch (option.Kind)
            {
                case OptionKind.Attack:
                {
                    Actor target = targets.FirstOrDefault();

                    if (target == null) return ActionResult.No(Why("no_target"));

                    Blow blow = Hero.Hit(Fight, Turn, target, option.Attack);

                    return blow == null ? ActionResult.No(Why("out_of_range")) : new ActionResult { Done = true, Blow = blow };
                }

                case OptionKind.Spell:
                case OptionKind.Again:
                {
                    Aim aim = AimFor(option, targets, square);

                    if (option.Modes.Count > 0 && string.IsNullOrEmpty(Mode))
                        return ActionResult.No(Why("choose_mode"));

                    Casting casting = option.Kind == OptionKind.Again
                        ? Battle.Magic.Again(Hero.Caster, option.Spell, aim, Turn, Fight)
                        : Battle.Magic.Cast(Hero.Caster, option.Spell, aim, CastAt, Fight, Turn);

                    if (!casting.Cast) return new ActionResult { Done = false, WhyNotKey = Why("no_target"), Casting = casting };

                    Hero.Receive(casting, _shelf);

                    return new ActionResult { Done = true, Casting = casting };
                }

                case OptionKind.Dash:
                    return Done(Fight.Dash(Turn, option.Cost));

                case OptionKind.Disengage:
                    return Done(Fight.Disengage(Turn, option.Cost));

                case OptionKind.Hide:
                {
                    Attempt hid = Fight.Hide(Turn, option.Cost);
                    return hid == null ? ActionResult.No(Why("no_action")) : new ActionResult { Done = true, Attempt = hid };
                }

                case OptionKind.StandUp:
                    return Done(Turn.StandUp(), "rooted");

                case OptionKind.BreakFree:
                {
                    Condition held = me.Has(Condition.Restrained) ? Condition.Restrained : Condition.Grappled;
                    Attempt escape = Battle.Magic.BreakFree(Fight, Turn, held);

                    return escape == null
                        ? ActionResult.No(Why("nothing_to_break"))
                        : new ActionResult { Done = true, Attempt = escape };
                }

                case OptionKind.Shake:
                {
                    Actor sleeper = targets.FirstOrDefault() ??
                                    (Fight.Field.Where(me) is Cell here
                                         ? Fight.Field.Adjacent(here).FirstOrDefault(a => Battle.Magic.CanBeShaken(a))
                                         : null);

                    return Done(Battle.Magic.Shake(Fight, Turn, sleeper), "nobody_to_shake");
                }

                case OptionKind.Item:
                    return Done(Hero.Use(option.Item.Id, Fight.Resolver, Turn) >= 0, "no_bonus");

                case OptionKind.Feature:
                {
                    if (!Turn.Take(Spend.Bonus)) return ActionResult.No(Why("no_bonus"));

                    return Done(Hero.Invoke(option.Feature, Fight.Resolver), "no_uses");
                }

                case OptionKind.Shape:
                    return Done(Hero.Shift(option.Form, Turn), "no_uses");

                case OptionKind.Surge:
                    return Done(Turn.Surge(), "no_surge");

                case OptionKind.Flee:
                    return Done(Fight.Flee(Turn), "not_an_edge");

                case OptionKind.EndTurn:
                    return EndTurn();
            }

            return ActionResult.No(Why("no_target"));
        }

        static ActionResult Done(bool done, string why = "no_action") =>
            done ? new ActionResult { Done = true } : ActionResult.No(Why(why));

        Aim AimFor(ActionOption option, IReadOnlyList<Actor> targets, Cell? square)
        {
            var aim = new Aim(targets, square,
                              option.Targeting == Targeting.Direction ? Facing : (Facing?)null,
                              mode: Mode, damageType: DamageType)
            {
                Side = Side,
            };

            return aim;
        }


        // --- moving --------------------------------------------------------------------------------

        // every square the hero could walk to with what is left of the turn, and what each costs
        public IReadOnlyDictionary<Cell, int> Reachable() =>
            MyTurn && !Hero.Actor.IsRooted
                ? Fight.Field.Reachable(Hero.Actor, Turn.SquaresLeft)
                : new Dictionary<Cell, int>();

        public IReadOnlyList<Cell> PathTo(Cell cell) =>
            MyTurn ? Fight.Field.RouteFor(Hero.Actor, cell) ?? (IReadOnlyList<Cell>)Array.Empty<Cell>()
                   : Array.Empty<Cell>();

        public ActionResult Move(Cell cell)
        {
            if (!MyTurn) return ActionResult.No(Why("not_on_your_turn"));

            if (Hero.Actor.IsRooted) return ActionResult.No(Why("rooted"));

            if (!Reachable().ContainsKey(cell)) return ActionResult.No(Why("too_far"));

            IReadOnlyList<Cell> walked = Fight.Walk(Turn, cell);

            Battle.Magic.Check(Fight.Actors);
            Fight.Judge();

            if (Fight.Over) Phase = SessionPhase.Over;

            return new ActionResult { Done = walked.Count > 1, Walked = walked };
        }

        // the open edge squares the hero could run off the map from
        public IEnumerable<Cell> Exits => Fight.Field.Exits;

        public override string ToString() =>
            $"{Phase}: {Fight}" + (Turn != null ? $", {Turn}" : "") +
            (Selected != null ? $", aiming {Selected.Id}" : "");
    }
}
