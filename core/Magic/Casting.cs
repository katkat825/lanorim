using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Core.Magic
{
    // what one effect did to one creature. a casting is a list of these, one per target per
    // effect, and nothing in it is a decision - it is a report.
    public sealed class Landing
    {
        public Landing(SpellEffect effect, Actor target, bool landed, int amount = 0,
                       Attempt attempt = null, Condition condition = Condition.None)
        {
            Effect = effect;
            Target = target;
            Landed = landed;
            Amount = amount;
            Attempt = attempt;
            Condition = condition;
        }

        public SpellEffect Effect { get; }

        public Actor Target { get; }

        public bool Landed { get; }

        // damage dealt, hit points healed, temporary hit points granted
        public int Amount { get; }

        // the attack roll or the saving throw, when there was one
        public Attempt Attempt { get; }

        public Condition Condition { get; }

        public override string ToString() =>
            $"{Effect.Kind.Id()} on {Target?.Id ?? "the ground"}: " +
            (Landed ? "landed" : "resisted") + (Amount != 0 ? $" {Amount}" : "");
    }

    public sealed class Casting
    {
        public Casting(Spell spell, Actor caster, int castAt, bool cast, string refusal = null,
                       IReadOnlyList<Landing> landings = null, IReadOnlyList<Cell> squares = null)
        {
            Spell = spell;
            Caster = caster;
            CastAt = castAt;
            Cast = cast;
            Refusal = refusal ?? "";
            Landings = landings ?? Array.Empty<Landing>();
            Squares = squares ?? Array.Empty<Cell>();
        }

        public Spell Spell { get; }

        public Actor Caster { get; }

        public int CastAt { get; }

        public bool Cast { get; }

        // why not, in engineer's English - never shown to a player, the UI greys the card instead
        public string Refusal { get; }

        public IReadOnlyList<Landing> Landings { get; }

        // the squares a zone, a wall or a light now covers
        public IReadOnlyList<Cell> Squares { get; }

        public int TotalDamage =>
            Landings.Where(l => l.Effect.Kind == Primitive.Damage).Sum(l => l.Amount);

        public int TotalHealing =>
            Landings.Where(l => l.Effect.Kind == Primitive.Heal).Sum(l => l.Amount);

        public IEnumerable<Actor> Touched => Landings.Select(l => l.Target).Where(a => a != null).Distinct();

        public static Casting Refused(Spell spell, Actor caster, int castAt, string why) =>
            new Casting(spell, caster, castAt, false, why);

        public override string ToString() =>
            Cast
                ? $"{Caster?.Id} casts {Spell?.Id} at level {CastAt}: " +
                  (Landings.Count == 0 ? "nothing to report" : string.Join("; ", Landings))
                : $"{Caster?.Id} cannot cast {Spell?.Id}: {Refusal}";
    }

    // where a spell was aimed. a spell wants creatures, a square, or neither
    public sealed class Aim
    {
        public Aim(IEnumerable<Actor> creatures = null, Cell? square = null)
        {
            Creatures = (creatures ?? Enumerable.Empty<Actor>()).Where(a => a != null).ToList();
            Square = square;
        }

        public IReadOnlyList<Actor> Creatures { get; }

        public Cell? Square { get; }

        public static Aim At(params Actor[] creatures) => new Aim(creatures);

        public static Aim On(Cell square) => new Aim(null, square);

        public static readonly Aim Nothing = new Aim();
    }

    // the one thing that casts a spell. it reads the primitives and does what they say; there is
    // no per-spell code anywhere in the engine, which is the whole point of the primitive list.
    public sealed class Incantation
    {
        readonly IResolver _resolver;

        public Incantation(IResolver resolver) =>
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        // who is holding what up. concentration is binary and one at a time; taking a new one
        // drops the old one and every boon it put out, wherever those landed.
        readonly Dictionary<Actor, List<Thread>> _held = new();

        public Casting Cast(Caster caster, Spell spell, Aim aim, int castAt = -1,
                            Encounter fight = null, Turn turn = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null) return Casting.Refused(null, null, 0, "no spell");

            if (castAt < 0) castAt = spell.Level;

            if (!caster.Knows(spell.Id))
                return Casting.Refused(spell, caster.Actor, castAt, "not on the sheet");

            if (!caster.CanCast(spell, castAt))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       $"needs {spell.CostAt(castAt)} mana, has {caster.Actor.Mana}");

            aim ??= Aim.Nothing;

            if (!caster.Actor.CanAct)
                return Casting.Refused(spell, caster.Actor, castAt, "cannot act");

            if (turn != null && !turn.Take(Spend.Action))
                return Casting.Refused(spell, caster.Actor, castAt, "no action left");

            if (!spell.IsCantrip && !caster.Actor.SpendMana(spell.CostAt(castAt)))
                return Casting.Refused(spell, caster.Actor, castAt, "mana went missing");

            // taking up a new concentration drops whatever was being held, and its boons with it
            if (spell.Concentration) Hold(caster.Actor, spell.Id);

            var landings = new List<Landing>();
            var squares = new List<Cell>();

            foreach (SpellEffect effect in spell.Effects)
                Resolve(caster, spell, effect, aim, castAt, fight, landings, squares);

            return new Casting(spell, caster.Actor, castAt, true, null, landings, squares);
        }

        void Resolve(Caster caster, Spell spell, SpellEffect effect, Aim aim, int castAt,
                     Encounter fight, List<Landing> landings, List<Cell> squares)
        {
            IReadOnlyList<Actor> targets = Targets(caster, spell, effect, aim, castAt, fight);

            if (effect.Reach == Reach.Place || effect.Kind == Primitive.Zone ||
                effect.Kind == Primitive.Illuminate)
            {
                Cell? centre = aim.Square ?? (fight != null ? fight.Field.Where(caster.Actor) : null);

                if (centre.HasValue && fight != null)
                    squares.AddRange(fight.Field.Burst(centre.Value, Math.Max(0, effect.Radius)));
                else if (centre.HasValue)
                    squares.Add(centre.Value);
            }

            foreach (Actor target in targets)
                landings.Add(Apply(caster, spell, effect, target, castAt));
        }

        IReadOnlyList<Actor> Targets(Caster caster, Spell spell, SpellEffect effect, Aim aim,
                                     int castAt, Encounter fight)
        {
            switch (effect.Reach)
            {
                case Reach.Caster:
                    return new[] { caster.Actor };

                case Reach.Place:
                    return Array.Empty<Actor>();

                case Reach.Around:
                {
                    Cell? here = fight?.Field.Where(caster.Actor);

                    if (!here.HasValue || fight == null) return Array.Empty<Actor>();

                    // SRD bursts centred on you spare you; the eight-foot fireball in your own lap
                    // is a different spell
                    return fight.Field.Caught(here.Value, effect.Radius)
                                .Where(a => !ReferenceEquals(a, caster.Actor))
                                .ToList();
                }

                case Reach.Burst:
                {
                    if (fight == null || !aim.Square.HasValue) return aim.Creatures;

                    return fight.Field.Caught(aim.Square.Value, effect.Radius).ToList();
                }

                case Reach.Creatures:
                    return aim.Creatures.Take(effect.TargetsAt(spell.Level, castAt)).ToList();

                default:
                    return aim.Creatures.Take(1).ToList();
            }
        }

        Landing Apply(Caster caster, Spell spell, SpellEffect effect, Actor target, int castAt)
        {
            DiceRoll amount = effect.AmountAt(spell.Level, castAt, caster.Actor.Level);

            // an attack roll, a saving throw, or neither - and never both
            Attempt attempt = null;
            bool landed = true;

            if (effect.AttackRoll)
            {
                attempt = _resolver.Resolve(RollKind.Attack, caster.AttackModifier,
                                            target.ArmorClass,
                                            caster.Actor.AttackAdvantage
                                                  .And(target.AdvantageAgainstMe));

                landed = attempt.Succeeded;
            }
            else if (effect.Save.HasValue)
            {
                attempt = Checks.Save(_resolver, target, effect.Save.Value, caster.SaveDc);

                landed = attempt.Failed;
            }

            switch (effect.Kind)
            {
                case Primitive.Damage:
                {
                    if (!landed && effect.OnSave == OnSave.Negates)
                        return new Landing(effect, target, false, 0, attempt);

                    int rolled = Math.Max(0, _resolver.Roll(amount));

                    // SRD: a critical doubles a spell's damage dice the same as a weapon's
                    if (attempt != null && attempt.IsCritical)
                        rolled += Math.Max(0, _resolver.Roll(amount));

                    if (!landed && effect.OnSave == OnSave.Half) rolled /= 2;

                    int suffered = target.Suffer(rolled, effect.DamageType);

                    // a save-for-half that was made still landed *something*, and the report has
                    // to say so or the log reads as a miss
                    return new Landing(effect, target,
                                       landed || effect.OnSave == OnSave.Half || suffered > 0,
                                       suffered, attempt);
                }

                case Primitive.Heal:
                {
                    int healed = target.Mend(Math.Max(0, _resolver.Roll(amount)));

                    return new Landing(effect, target, healed > 0, healed);
                }

                case Primitive.Ward:
                {
                    int ward = Math.Max(0, _resolver.Roll(amount));

                    target.Health.GrantTemporary(ward);

                    return new Landing(effect, target, ward > 0, ward);
                }

                case Primitive.Afflict:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    bool applied = target.Apply(effect.Condition);

                    if (applied && effect.Duration == Duration.Concentration)
                        Remember(caster.Actor, target, spell.Id, effect.Condition);

                    return new Landing(effect, target, applied, 0, attempt, effect.Condition);
                }

                case Primitive.Relieve:
                {
                    bool removed = target.Remove(effect.Condition);

                    return new Landing(effect, target, removed, 0, null, effect.Condition);
                }

                case Primitive.Sway:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    target.Boons.Add(Sway(spell, effect));

                    if (effect.Duration == Duration.Concentration)
                        Remember(caster.Actor, target, spell.Id);

                    return new Landing(effect, target, true, effect.Sway, attempt);
                }

                case Primitive.Dispel:
                {
                    int ended = target.Boons.EndFrom(effect.Note);

                    if (string.IsNullOrEmpty(effect.Note))
                    {
                        ended = target.Boons.All.Count;
                        target.Boons.Clear();
                        Release(target);
                    }

                    return new Landing(effect, target, ended > 0, ended);
                }

                // Shift, Zone, Illuminate, Reveal, Summon and Narrate change the world rather than
                // the creature: the casting reports them and the campaign or the board layer acts
                default:
                    return new Landing(effect, target, landed, 0, attempt);
            }
        }

        static Boon Sway(Spell spell, SpellEffect effect) =>
            new Boon(spell.Id, spell.Id, effect.Duration,
                     effect.Sway, effect.SwayDice,
                     attacks: (effect.Touches & Sways.Attacks) != 0,
                     saves: (effect.Touches & Sways.Saves) != 0,
                     checks: (effect.Touches & Sways.Checks) != 0,
                     damage: (effect.Touches & Sways.Damage) != 0,
                     armorClass: (effect.Touches & Sways.ArmorClass) != 0 ? effect.Sway : 0,
                     skill: effect.Skill);


        // --- concentration ----------------------------------------------------------------------

        // one thread per thing the held spell did to somebody. keeping the condition on the thread
        // is what lets a dropped concentration lift exactly what it put there and nothing else.
        readonly struct Thread
        {
            public Thread(Actor target, string spellId, Condition condition)
            {
                Target = target;
                SpellId = spellId;
                Condition = condition;
            }

            public Actor Target { get; }

            public string SpellId { get; }

            public Condition Condition { get; }
        }

        void Hold(Actor caster, string spellId)
        {
            string dropped = caster.Concentrate(spellId);

            if (string.IsNullOrEmpty(dropped)) return;

            Unwind(caster, dropped);
        }

        void Remember(Actor caster, Actor touched, string spellId,
                      Condition condition = Condition.None)
        {
            if (!_held.TryGetValue(caster, out List<Thread> threads))
                _held[caster] = threads = new List<Thread>();

            threads.Add(new Thread(touched, spellId, condition));
        }

        void Unwind(Actor caster, string spellId)
        {
            if (!_held.TryGetValue(caster, out List<Thread> threads)) return;

            foreach (Thread thread in threads.Where(t => t.SpellId == spellId).ToList())
            {
                thread.Target.Boons.EndFrom(spellId);

                if (thread.Condition != Condition.None) thread.Target.Remove(thread.Condition);
            }

            threads.RemoveAll(t => t.SpellId == spellId);
        }

        // the caster went down, or let go on purpose
        public void Release(Actor caster)
        {
            if (caster == null) return;

            string dropped = caster.EndConcentration();

            if (!string.IsNullOrEmpty(dropped)) Unwind(caster, dropped);
        }

        // called every time anything goes down: SRD drops concentration when the caster does
        public void Check(IEnumerable<Actor> actors)
        {
            foreach (Actor actor in (actors ?? Enumerable.Empty<Actor>()).ToList())
                if (actor.IsDown && actor.IsConcentrating)
                    Release(actor);
        }

        public bool IsHolding(Actor caster) => caster != null && caster.IsConcentrating;
    }
}
