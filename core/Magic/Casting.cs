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
                       IReadOnlyList<Landing> landings = null, IReadOnlyList<Cell> squares = null,
                       IReadOnlyList<Cell> covered = null, bool countered = false)
        {
            Spell = spell;
            Caster = caster;
            CastAt = castAt;
            Cast = cast;
            Refusal = refusal ?? "";
            Landings = landings ?? Array.Empty<Landing>();
            Squares = squares ?? Array.Empty<Cell>();
            Covered = covered ?? Array.Empty<Cell>();
            Countered = countered;
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

        // the squares a line, a cone or a cube swept, whether anybody was standing in them or
        // not - what the board lights up so the player sees the shape that was thrown
        public IReadOnlyList<Cell> Covered { get; }

        // stopped mid-cast by somebody's reaction. the action is gone and the resource is not:
        // SRD 5.2.1's Counterspell wastes the casting, and the slot is not expended
        public bool Countered { get; }

        public int TotalDamage =>
            Landings.Where(l => l.Effect.Kind == Primitive.Damage).Sum(l => l.Amount);

        public int TotalHealing =>
            Landings.Where(l => l.Effect.Kind == Primitive.Heal).Sum(l => l.Amount);

        public IEnumerable<Actor> Touched => Landings.Select(l => l.Target).Where(a => a != null).Distinct();

        // the items the spell made, for the content layer to put in the caster's pack
        public IEnumerable<(string item, int count)> Conjured =>
            Landings.Where(l => l.Effect.Kind == Primitive.Conjure && l.Landed)
                    .Select(l => (l.Effect.Item, l.Amount));

        public static Casting Refused(Spell spell, Actor caster, int castAt, string why) =>
            new Casting(spell, caster, castAt, false, why);

        public static Casting Stopped(Spell spell, Actor caster, int castAt) =>
            new Casting(spell, caster, castAt, false, "countered", countered: true);

        public override string ToString() =>
            Cast
                ? $"{Caster?.Id} casts {Spell?.Id} at level {CastAt}: " +
                  (Landings.Count == 0 ? "nothing to report" : string.Join("; ", Landings))
                : $"{Caster?.Id} cannot cast {Spell?.Id}: {Refusal}";
    }

    // where a spell was aimed. a spell wants creatures, a square, a direction, or none of them.
    // a line or a cone pointed at a square is thrown toward it (Template.Toward), which is how a
    // player aims one with a click; a facing is for everything that is not a click.
    public sealed class Aim
    {
        public Aim(IEnumerable<Actor> creatures = null, Cell? square = null, Facing? facing = null,
                   Skill skill = Skill.None, Ability? ability = null,
                   IEnumerable<Cell> squares = null, string mode = null,
                   DamageType damageType = DamageType.None)
        {
            Mode = mode ?? "";
            DamageType = damageType;
            Creatures = (creatures ?? Enumerable.Empty<Actor>()).Where(a => a != null).ToList();
            Squares = (squares ?? (square.HasValue ? new[] { square.Value } : Array.Empty<Cell>()))
                .ToList();
            Square = square ?? (Squares.Count > 0 ? Squares[0] : (Cell?)null);
            Facing = facing;
            Skill = skill;
            Ability = ability;
        }

        // every square picked, for a spell with more than one burst centre. Square is the first
        public IReadOnlyList<Cell> Squares { get; }

        // what the caster chose as they cast: Guidance's skill, Hex's ability
        public Skill Skill { get; }

        public Ability? Ability { get; }

        public Aim Choosing(Skill skill) =>
            new Aim(Creatures, Square, Facing, skill, Ability, Squares, Mode, DamageType)
            { Weapon = Weapon };

        public Aim Choosing(Ability ability) =>
            new Aim(Creatures, Square, Facing, Skill, ability, Squares, Mode, DamageType)
            { Weapon = Weapon };

        // which of a spell's modes: "blindness" or "deafness", "enlarge" or "reduce"
        public string Mode { get; }

        public Aim Choosing(string mode) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, mode, DamageType)
            { Weapon = Weapon, Side = Side };

        // which side of a wall is its business side: for a straight wall, left or right of the
        // way it runs; for a ring, Left is inside and Right outside. Wall of Fire's burning side
        public Side Side { get; init; }

        public Aim On(Side side) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, DamageType)
            { Weapon = Weapon, Side = side };

        // the damage type the caster picked: Chromatic Orb's
        public DamageType DamageType { get; }

        public Aim Choosing(DamageType type) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, type) { Weapon = Weapon };

        // the weapon a spell swings: True Strike's
        public Attack Weapon { get; init; }

        public Aim With(Attack weapon) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, DamageType)
            { Weapon = weapon };

        public static Aim OnMany(params Cell[] squares) =>
            new Aim(null, null, null, Skill.None, null, squares);

        public IReadOnlyList<Actor> Creatures { get; }

        public Cell? Square { get; }

        public Facing? Facing { get; }

        public static Aim At(params Actor[] creatures) => new Aim(creatures);

        public static Aim On(Cell square) => new Aim(null, square);

        public static Aim Toward(Facing facing) => new Aim(null, null, facing);

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

            if (caster.Actor.IsShifted)
                return Casting.Refused(spell, caster.Actor, castAt, "in a borrowed shape");

            if (caster.Actor.Boons.NoCasting)
                return Casting.Refused(spell, caster.Actor, castAt, "can't cast in this form");

            // THE ONLY QUESTION ASKED OF THE RESOURCE, and it does not say which mode answered
            // it. A slots caster with no 3rd-level slot and a points caster who has already
            // cast their one 6th today are refused by the same line.
            if (!caster.CanCast(spell, castAt))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       $"nothing left to cast a level {castAt} spell with " +
                                       $"({caster.Resource?.Describe() ?? "no spell resource"})");

            aim ??= Aim.Nothing;

            // a Shield on your own turn is not a Shield. the only way to cast one is to be offered
            // the moment it answers - Answer, below
            if (spell.Answers)
                return Casting.Refused(spell, caster.Actor, castAt,
                                       spell.IsReaction
                                           ? "a reaction spell is cast when its moment comes, not on a turn"
                                           : "cast right after the hit it rides on, not on its own");

            if (!caster.Actor.CanAct)
                return Casting.Refused(spell, caster.Actor, castAt, "cannot act");

            // pointing it is part of casting it: on a board, a line with no direction goes nowhere
            if (fight != null && spell.NeedsADirection && !aim.Facing.HasValue &&
                !aim.Square.HasValue)
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "a line, a cone or a cube has to be pointed somewhere");

            // on a board, what it is aimed at has to be in its range and in sight. SRD's touch is
            // range 0 in the data and one square on the grid
            if (fight != null && OutOfReach(caster, spell, aim, fight) is string far)
                return Casting.Refused(spell, caster.Actor, castAt, far);

            // a choice the spell leaves to the caster has to have been made
            if (spell.Effects.Any(e => e.ChosenSkill) && aim.Skill == Skill.None)
                return Casting.Refused(spell, caster.Actor, castAt, "choose a skill for it");

            if (spell.Effects.Any(e => e.ChosenAbility) && !aim.Ability.HasValue)
                return Casting.Refused(spell, caster.Actor, castAt, "choose an ability for it");

            if (spell.Effects.Any(e => e.ChosenAbility && e.AbilityChoices.Count > 0 &&
                                       !e.AbilityChoices.Contains(aim.Ability.Value)))
                return Casting.Refused(spell, caster.Actor, castAt, "not an ability it can choose");

            // a minute's casting or an hour's is not a fight's (Identify, Raise Dead, Foresight)
            if (fight != null && spell.OutOfCombat)
                return Casting.Refused(spell, caster.Actor, castAt, "takes too long to cast in a fight");

            if (spell.Strikes && aim.Weapon == null)
                return Casting.Refused(spell, caster.Actor, castAt, "choose the weapon it strikes with");

            if (fight != null && UnderTheZone(caster, spell, aim, fight, null) is string outside)
                return Casting.Refused(spell, caster.Actor, castAt, outside);

            if (fight != null && spell.Effects.Any(e => e.Kind == Primitive.Zone && e.Unoccupied) &&
                aim.Square.HasValue && fight.Field.At(aim.Square.Value) != null)
                return Casting.Refused(spell, caster.Actor, castAt, "that square is taken");

            if (spell.Modes.Count > 0 && !spell.Modes.Contains(aim.Mode))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "choose how to cast it: " + string.Join(" or ", spell.Modes));

            foreach (SpellEffect choice in spell.Effects.Where(e => e.ChosenDamageType &&
                                                                    InMode(e, aim)))
                if (!choice.DamageChoices.Contains(aim.DamageType))
                    return Casting.Refused(spell, caster.Actor, castAt,
                                           "choose a damage type for it");

            // SRD 5.2.1 Charmed: no damaging or magical effect aimed at the charmer. v1 reads it
            // as "nothing hostile": a heal or a blessing still may, a curse with no save may not
            if (aim.Creatures.Any(t => caster.Actor.HasFrom(Condition.Charmed, t)) &&
                spell.Effects.Any(e => !(e.Kind == Primitive.Heal || e.Kind == Primitive.Ward ||
                                         e.Kind == Primitive.Relieve || e.Kind == Primitive.Stabilize ||
                                         e.Kind == Primitive.Sway && !e.Save.HasValue && Kindly(e))))
                return Casting.Refused(spell, caster.Actor, castAt, "charmed by the target");

            bool bonus = spell.CastingTime == CastingTime.BonusAction;

            if (turn != null && !turn.Take(bonus ? Spend.Bonus : Spend.Action))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       bonus ? "no bonus action left" : "no action left");

            return Go(caster, spell, aim, castAt, fight, null);
        }

        static string OutOfReach(Caster caster, Spell spell, Aim aim, Encounter fight)
        {
            Cell? here = fight.Field.Where(caster.Actor);

            if (!here.HasValue) return null;

            int reach = Math.Max(1, spell.RangeAt(caster.Actor.Level));

            // a spell that swings a weapon reaches as far as the weapon does, and the swing says so
            bool picksCreatures = !spell.Strikes &&
                                  spell.Effects.Any(e => e.Reach == Reach.Creature ||
                                                         e.Reach == Reach.Creatures);

            // Spiritual Weapon: the attack comes from the force, at a creature beside it
            SpellEffect near = spell.Effects.FirstOrDefault(e => e.NearZone > 0);
            // Dimension Door: the one creature it takes along stands beside the caster
            bool passenger = spell.Effects.Any(e => e.Passenger);

            if (picksCreatures && near == null && !passenger)
                foreach (Actor target in aim.Creatures.Where(a => !ReferenceEquals(a, caster.Actor)))
                    if (!fight.Field.InRange(caster.Actor, target, reach) ||
                        !fight.Sees(caster.Actor, target))
                        return $"{target.Id} is out of range or out of sight";

            if (near != null && aim.Square.HasValue)
                foreach (Actor target in aim.Creatures)
                    if (!(fight.Field.Where(target) is Cell at) ||
                        Battlefield.Distance(at, aim.Square.Value) > near.NearZone)
                        return $"{target.Id} is not beside it";

            if (passenger)
                foreach (Actor target in aim.Creatures.Where(a => !ReferenceEquals(a, caster.Actor)))
                    if (fight.Field.Distance(caster.Actor, target) > 1)
                        return $"{target.Id} has to be beside you to come along";

            // Chain Lightning: the bolts leap from the first target to others within 30 feet of
            // it, and no creature is struck by two
            SpellEffect leap = spell.Effects.FirstOrDefault(e => e.NearFirst > 0);

            if (leap != null && aim.Creatures.Count > 1)
            {
                if (aim.Creatures.Distinct().Count() != aim.Creatures.Count)
                    return "a creature can be struck by only one of the bolts";

                Actor first = aim.Creatures[0];

                foreach (Actor other in aim.Creatures.Skip(1))
                    if (fight.Field.Distance(first, other) > leap.NearFirst)
                        return $"{other.Id} is too far from the first target";
            }

            bool picksSquares = spell.Effects.Any(e => e.Reach == Reach.Burst ||
                                                       e.Reach == Reach.Place ||
                                                       e.Reach == Reach.Wall);

            // Dimension Door: a place you can visualize or describe, not only one you can see
            bool unseen = spell.Effects.Any(e => e.Unseen);

            if (picksSquares)
                foreach (Cell square in aim.Squares)
                    if (Battlefield.Distance(here.Value, square) > reach ||
                        !unseen && !fight.Field.CanSee(here.Value, square))
                        return $"{square} is out of range or out of sight";

            if (unseen && aim.Square.HasValue &&
                Battlefield.Distance(here.Value, aim.Square.Value) > reach)
                return $"{aim.Square.Value} is out of range";

            return null;
        }

        // a reaction spell, cast because the fight offered its moment and the caster's chooser
        // took it. the reaction itself was spent by the window that offered it; what is left is
        // the resource and the spell. aimed at whoever caused the moment - the one who hit you,
        // the one casting - and an effect that reaches only the caster ignores that.
        public Casting Answer(Caster caster, Spell spell, Moment moment, Encounter fight = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null || moment == null)
                return Casting.Refused(spell, caster.Actor, 0, "nothing to answer");

            bool targeted = moment.Trigger == Trigger.Targeted && spell.AnswersSpell.Length > 0 &&
                            moment.Spell == spell.AnswersSpell;

            if (!spell.Answers || spell.Trigger != moment.Trigger && !targeted)
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "that spell does not answer that moment");

            if (!caster.Knows(spell.Id))
                return Casting.Refused(spell, caster.Actor, spell.Level, "not on the sheet");

            // a Shield is still a spell, and a bear does not cast it
            if (caster.Actor.IsShifted || caster.Actor.Boons.NoCasting)
                return Casting.Refused(spell, caster.Actor, spell.Level, "in a borrowed shape");

            if (!caster.CanCast(spell, spell.Level))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "nothing left to cast it with");

            // a smite lands on the one the caster just hit; everything else on whoever caused it
            Actor at = moment.Trigger == Trigger.Struck ? moment.Target : moment.Source;

            return Go(caster, spell, Aim.At(at), spell.Level, fight, moment);
        }

        // the part every cast shares, once the turn has paid for it
        Casting Go(Caster caster, Spell spell, Aim aim, int castAt, Encounter fight,
                   Moment answering)
        {
            // THE CAST WINDOW. before the resource is paid and before anything resolves, every
            // creature hostile to the caster is offered the chance to stop it. a Counterspell can
            // itself be countered - it comes through here like everything else - and one reaction
            // a round each is what keeps that from going on forever.
            if (fight != null)
            {
                Moment casting = Moment.Cast(caster.Actor, spell.Id, castAt);

                fight.Offer(casting, fight.Field.Enemies(caster.Actor));

                if (casting.Stopped) return Casting.Stopped(spell, caster.Actor, castAt);
            }

            // paid after the action is taken and before anything resolves, so a cast that is
            // refused downstream has still cost what it cost - which is the table's rule. a
            // countered cast is the exception, above, because SRD 5.2.1 says so in as many words
            if (!caster.Pay(spell, castAt))
                return Casting.Refused(spell, caster.Actor, castAt,
                                       "the spell resource would not pay");

            // casting gives an unseen caster away (SRD's Invisible) - an Invisibility ends here
            caster.Actor.Boons.Cast();
            Unveil(caster.Actor, fight);

            // Foresight: casting it again ends the one already cast
            if (spell.EndsPrevious)
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id && p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            _offered.Clear();
            _empoweredThisCast = false;
            _blessedThisCast = false;

            // Major Image at 4+: no concentration at all
            bool holds = spell.Concentration &&
                         !(spell.ConcentrationBelow > 0 && castAt >= spell.ConcentrationBelow);

            // taking up a new concentration drops whatever was being held, and its boons with it
            if (holds)
            {
                Hold(caster.Actor, spell.Id);
                _heldAt[caster.Actor] = castAt;
            }
            else if (spell.Repeat.HasValue && spell.Lasts != Duration.Instant)
            {
                // Produce Flame: no concentration, and still repeatable for as long as it lasts
                _lasting.Add((caster.Actor, spell.Id));
            }

            // the damage type picked at the cast, kept for the repeats
            if (aim.DamageType != DamageType.None) _chosenType[(caster.Actor, spell.Id)] = aim.DamageType;

            var landings = new List<Landing>();
            var squares = new List<Cell>();
            var covered = new List<Cell>();

            Run(caster, spell, spell.Effects, aim, castAt, fight, answering,
                landings, squares, covered);

            return new Casting(spell, caster.Actor, castAt, true, null, landings, squares,
                               covered.Distinct().ToList());
        }

        void Run(Caster caster, Spell spell, IEnumerable<SpellEffect> effects, Aim aim, int castAt,
                 Encounter fight, Moment answering, List<Landing> landings, List<Cell> squares,
                 List<Cell> covered, bool again = false)
        {
            // Produce Flame's hurl is its later actions, not its casting
            if (!again) effects = effects.Where(e => !e.RepeatOnly);
            // the last saving throw each creature made against this casting, for an effect that
            // shares it rather than asking for another
            var saves = new Dictionary<Actor, Attempt>();

            int previous = landings.Count;

            foreach (SpellEffect effect in effects.Where(e => InMode(e, aim) && !e.OnEnd))
            {
                // who the effect just before this one actually landed on, for one that follows it
                HashSet<Actor> hit = new HashSet<Actor>(
                    landings.Skip(previous).Where(l => l.Landed && l.Target != null)
                            .Select(l => l.Target));

                previous = landings.Count;

                Resolve(caster, spell, effect, aim, castAt, fight, answering,
                        landings, squares, covered, saves, effect.Follows ? hit : null);
            }
        }

        static bool InMode(SpellEffect effect, Aim aim) =>
            effect.Mode.Length == 0 || effect.Mode == aim.Mode;

        // SPIRITUAL WEAPON'S LATER TURNS. the spell is already cast and already paid for; while
        // the caster still holds it, a later turn can spend the spell's repeat - a bonus action
        // for the weapon - to do the effects marked as repeating again. nothing else of the spell
        // happens twice, and nothing is paid twice.
        public Casting Again(Caster caster, Spell spell, Aim aim, Turn turn = null,
                             Encounter fight = null)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            if (spell == null || !spell.Repeat.HasValue)
                return Casting.Refused(spell, caster.Actor, spell?.Level ?? 0,
                                       "nothing about that spell repeats");

            if (caster.Actor.Concentrating != spell.Id && !_lasting.Contains((caster.Actor, spell.Id)))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "not holding that spell any more");

            if (spell.MovesWhenDown && MarkStillStanding(caster, spell))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       "it moves only once the creature it is on drops to 0 hit points");

            aim ??= Aim.Nothing;

            if (aim.DamageType == DamageType.None &&
                _chosenType.TryGetValue((caster.Actor, spell.Id), out DamageType chosen))
                aim = aim.Choosing(chosen);

            if (fight != null && OutOfReach(caster, spell, aim, fight) is string far)
                return Casting.Refused(spell, caster.Actor, spell.Level, far);

            if (fight != null && UnderTheZone(caster, spell, aim, fight, ZonesOf(caster.Actor)
                    .FirstOrDefault(z => z.Source == spell.Id)) is string outside)
                return Casting.Refused(spell, caster.Actor, spell.Level, outside);

            if (!caster.Actor.CanAct)
                return Casting.Refused(spell, caster.Actor, spell.Level, "cannot act");

            bool bonus = spell.Repeat == CastingTime.BonusAction;

            if (turn != null && !turn.Take(bonus ? Spend.Bonus : Spend.Action))
                return Casting.Refused(spell, caster.Actor, spell.Level,
                                       bonus ? "no bonus action left" : "no action left");

            // the level it was cast at is the level it keeps swinging at
            int castAt = _heldAt.TryGetValue(caster.Actor, out int held) ? held : spell.Level;

            var landings = new List<Landing>();
            var squares = new List<Cell>();
            var covered = new List<Cell>();

            aim ??= Aim.Nothing;

            // a repeating shift that reaches the zone MOVES the zone - Moonbeam's beam, a Flaming
            // Sphere rolled - and whoever it now covers is washed over as if they had walked in
            foreach (SpellEffect move in spell.Effects.Where(e => e.Repeats && e.Kind == Primitive.Shift &&
                                                                  e.Reach == Reach.Zone))
            {
                SpellZone zone = ZonesOf(caster.Actor).FirstOrDefault(z => z.Source == spell.Id &&
                                                                          !z.FollowsOwner);

                if (zone == null || !aim.Square.HasValue || fight == null) continue;

                if (move.Length > 0 &&
                    Battlefield.Distance(zone.Centre.Value, aim.Square.Value) > move.Length)
                    return Casting.Refused(spell, caster.Actor, castAt,
                                           "further than it can be moved");

                List<Actor> wereIn = fight.CaughtIn(zone).ToList();

                if (move.Rams)
                {
                    // Flaming Sphere: rolled square by square, and the first creature in its way
                    // is rammed and stops it for the turn
                    Cell at = zone.Centre.Value;
                    Actor rammed = null;

                    foreach (Cell next in Sight.Between(at, aim.Square.Value).Skip(1))
                    {
                        Actor there = fight.Field.At(next);

                        if (there != null && !ReferenceEquals(there, caster.Actor))
                        {
                            rammed = there;
                            break;
                        }

                        if (!fight.Field.Map.Contains(next) || !fight.Field.Map.CanCross(at, next))
                            break;

                        at = next;
                    }

                    zone.MoveTo(at);
                    fight.ZoneMoved(zone, wereIn);

                    if (rammed != null && zone.Pulses.Has(Core.Combat.Pulse.Ram) && fight.Affects(zone, rammed))
                        zone.Act(fight, rammed, Core.Combat.Pulse.Ram);

                    continue;
                }

                zone.MoveTo(aim.Square.Value);
                fight.ZoneMoved(zone, wereIn);
            }

            // an aura on the moved zone, and Moonbeam's hold on a creature it turned back, follow it
            if (fight != null) Refresh(fight);

            // Hex, Hunter's Mark: the mark leaves the fallen creature for the new one
            if (spell.MovesWhenDown)
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id && p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            // Telekinesis: one target at a time - a new one ends the spell on whoever it was on
            if (spell.Effects.Any(e => e.Repeats && e.Switches))
                foreach (Actor was in _placed.Where(p => p.Spell == spell.Id &&
                                                         p.Caster != null &&
                                                         ReferenceEquals(p.Caster.Actor, caster.Actor) &&
                                                         !aim.Creatures.Contains(p.Target))
                                             .Select(p => p.Target).Distinct().ToList())
                    Lift(was, spell.Id, fight);

            Run(caster, spell,
                spell.Effects.Where(e => e.Repeats &&
                                         !(e.Kind == Primitive.Shift && e.Reach == Reach.Zone)),
                aim, castAt, fight, null, landings, squares, covered, again: true);

            return new Casting(spell, caster.Actor, castAt, true, null, landings, squares,
                               covered.Distinct().ToList());
        }

        readonly Dictionary<Actor, int> _heldAt = new();

        // whether a repeating spell can be done again now: held in concentration, or lasting -
        // and for a mark, only once the creature it was on has dropped
        public bool CanRepeat(Caster caster, Spell spell) =>
            caster != null && spell != null && spell.Repeat.HasValue &&
            (caster.Actor.Concentrating == spell.Id || _lasting.Contains((caster.Actor, spell.Id))) &&
            !(spell.MovesWhenDown && MarkStillStanding(caster, spell));

        bool MarkStillStanding(Caster caster, Spell spell) =>
            _placed.Any(p => p.Spell == spell.Id && p.Caster != null &&
                             ReferenceEquals(p.Caster.Actor, caster.Actor) && !p.Target.IsDown);

        // who has already been offered the chance to answer being targeted by this cast
        readonly HashSet<Actor> _offered = new();

        // once a casting: Empowered Evocation's one damage roll, Blessed Healer's one heal
        bool _empoweredThisCast;
        bool _blessedThisCast;

        // spells that repeat without being held: who cast them, and which
        readonly HashSet<(Actor, string)> _lasting = new();

        // the damage type picked at the cast, for the repeats: Wyrmbreath Boon's breath
        readonly Dictionary<(Actor, string), DamageType> _chosenType = new();

        // Call Lightning: a bolt must fall under the cloud. at the cast the cloud is not there yet,
        // so it is where it will be; after, it is where it is
        static string UnderTheZone(Caster caster, Spell spell, Aim aim, Encounter fight,
                                   SpellZone made)
        {
            if (!spell.Effects.Any(e => e.WithinZone) || !aim.Square.HasValue) return null;

            SpellEffect area = spell.Effects.FirstOrDefault(e => e.Kind == Primitive.Zone);

            if (area == null) return null;

            bool under;

            if (made != null)
                under = made.Covers(fight.Field, aim.Square.Value);
            else
            {
                Cell? centre = area.OnCaster ? fight.Field.Where(caster.Actor) : aim.Square;

                under = centre.HasValue &&
                        Battlefield.Distance(centre.Value, aim.Square.Value) <= area.Radius;
            }

            return under ? null : "it has to fall under the spell's area";
        }

        void Resolve(Caster caster, Spell spell, SpellEffect effect, Aim aim, int castAt,
                     Encounter fight, Moment answering, List<Landing> landings,
                     List<Cell> squares, List<Cell> covered, Dictionary<Actor, Attempt> saves,
                     HashSet<Actor> only)
        {
            IReadOnlyList<Actor> targets =
                Targets(caster, spell, effect, aim, castAt, fight, covered);

            if (only != null) targets = targets.Where(only.Contains).ToList();

            // "creatures of your choice": an area that leaves the caster's own side alone
            if (effect.SparesAllies && effect.Kind != Primitive.Zone)
                targets = targets.Where(t => t.Side != caster.Actor.Side).ToList();

            // Entangle: "each creature (other than you)"
            if (effect.SparesCaster)
                targets = targets.Where(t => !ReferenceEquals(t, caster.Actor)).ToList();

            // Hypnotic Pattern: only a creature that can see the pattern - some square of it, not
            // past a wall or through a heavily obscured square (its own included) - and not Blinded
            if (effect.NeedsSight && fight != null && covered.Count > 0)
            {
                List<Cell> pattern = covered.Distinct().ToList();

                targets = targets.Where(t => fight.Field.Where(t) is Cell at &&
                                             pattern.Any(c => fight.Field.CanSee(at, c) &&
                                                              !fight.Obscured(at, c, t))).ToList();
            }

            // Shield: a creature a spell has picked out may answer it, once a cast, before it lands
            if (fight != null && (effect.Reach == Reach.Creature || effect.Reach == Reach.Creatures))
                foreach (Actor picked in targets.Where(t => !ReferenceEquals(t, caster.Actor)).Distinct().ToList())
                    if (_offered.Add(picked))
                        fight.Offer(Moment.Targeted(caster.Actor, picked, spell.Id), picked);

            // Dispel Magic on an effect, Disintegrate on a creation of force: whatever spell holds
            // the aimed square
            if (effect.Kind == Primitive.Dispel && effect.Reach == Reach.Place && fight != null &&
                aim.Square.HasValue)
            {
                int ended = DispelAt(caster, aim.Square.Value, castAt, fight, effect.EndsForce);
                landings.Add(new Landing(effect, null, ended > 0, ended));
                return;
            }

            // Slow's "up to six creatures of your choice": the caster's foes first, then by id, so
            // the same cast picks the same six
            if (effect.UpTo > 0 && targets.Count > effect.UpTo)
                targets = targets.OrderBy(t => t.Side == caster.Actor.Side ? 1 : 0)
                                 .ThenBy(t => t.Id, StringComparer.Ordinal)
                                 .Take(effect.UpTo).ToList();

            // Sunburst: magical darkness in the area is dispelled
            if (effect.DispelsDarkness && fight != null) Brighten(fight, effect, aim, covered);

            // A ZONE ON A BOARD IS A REAL THING NOW: it goes on the fight, acts when its pulses
            // come round, and comes off when the spell does
            if (effect.Kind == Primitive.Zone && fight != null)
                MakeZone(caster, spell, effect, aim, castAt, fight);

            if (effect.Reach == Reach.Place || effect.Kind == Primitive.Zone ||
                effect.Kind == Primitive.Illuminate)
            {
                // a light or a zone the caster carries sits on the caster, whatever else was aimed
                bool carried = effect.Reach == Reach.Caster || effect.Reach == Reach.Around;

                Cell? here = fight?.Field.Where(caster.Actor);
                Cell? centre = carried ? here ?? aim.Square : aim.Square ?? here;

                if (centre.HasValue && fight != null)
                    squares.AddRange(fight.Field.Burst(centre.Value, Math.Max(0, effect.Radius)));
                else if (centre.HasValue)
                    squares.Add(centre.Value);
            }

            foreach (Actor target in targets)
                landings.Add(Apply(caster, spell, effect, aim, target, castAt, fight, answering,
                                   saves));

            // Chromatic Orb: two matching dice and it leaps to another creature the caster picked,
            // within reach of the last one, once per slot level, never to the same one twice
            if (effect.Leaps > 0 && targets.Count > 0)
            {
                var struck = new HashSet<Actor>(targets);
                Actor last = targets[targets.Count - 1];
                int leaps = Math.Max(1, castAt);

                while (leaps > 0 && Matched(_lastFaces))
                {
                    Actor next = aim.Creatures.FirstOrDefault(
                        a => !struck.Contains(a) &&
                             (fight == null || fight.Field.Distance(last, a) <= effect.Leaps));

                    if (next == null) break;

                    struck.Add(next);
                    leaps--;
                    last = next;

                    _lastFaces = Array.Empty<int>();
                    landings.Add(Apply(caster, spell, effect, aim, next, castAt, fight, answering,
                                       saves));
                }
            }
        }

        IReadOnlyList<int> _lastFaces = Array.Empty<int>();

        static bool Matched(IReadOnlyList<int> faces) =>
            faces.GroupBy(f => f).Any(g => g.Count() > 1);

        IReadOnlyList<Actor> Targets(Caster caster, Spell spell, SpellEffect effect, Aim aim,
                                     int castAt, Encounter fight, List<Cell> covered)
        {
            switch (effect.Reach)
            {
                case Reach.Line:
                case Reach.Cone:
                case Reach.Cube:
                {
                    Cell? here = fight?.Field.Where(caster.Actor);

                    // off the board there is no shape, so whoever the caller says was in it was
                    if (!here.HasValue || fight == null) return aim.Creatures;

                    Facing? facing = aim.Facing ??
                                     (aim.Square.HasValue
                                          ? Template.Toward(here.Value, aim.Square.Value)
                                          : (Facing?)null);

                    if (!facing.HasValue) return Array.Empty<Actor>();

                    List<Cell> swept = (effect.Reach switch
                    {
                        Reach.Line => fight.Field.Line(here.Value, facing.Value, effect.Length,
                                                       Math.Max(1, effect.Width)),
                        Reach.Cone => fight.Field.Cone(here.Value, facing.Value, effect.Length),
                        _ => fight.Field.Cube(here.Value, facing.Value, effect.Length),
                    }).ToList();

                    covered.AddRange(swept);

                    // the shape starts past the caster's own square, so the caster is never in it
                    return fight.Field.Caught(swept).ToList();
                }

                case Reach.Caster:
                    return new[] { caster.Actor };

                // an aura cast with no board to stand on is on the caster alone: Pass without
                // Trace on a sneak through the campaign's story
                case Reach.Zone when effect.WhileInside && fight == null:
                    return new[] { caster.Actor };

                // acts only when its zone pulses, never on the cast itself
                case Reach.Zone:
                    return Array.Empty<Actor>();

                case Reach.Square:
                {
                    if (fight == null || !aim.Square.HasValue) return aim.Creatures;

                    List<Cell> square = fight.Field.Square(aim.Square.Value,
                                                           Math.Max(1, effect.Length)).ToList();

                    covered.AddRange(square);

                    return fight.Field.Caught(square).ToList();
                }

                case Reach.Place:
                case Reach.Wall:
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

                    // several centres, one creature caught once however many it stands in
                    return aim.Squares.Take(Math.Max(1, effect.Points))
                              .SelectMany(c => fight.Field.Caught(c, effect.Radius))
                              .Distinct()
                              .ToList();
                }

                // several picks, and SRD lets a Scorching Ray or an Eldritch Blast put more than
                // one of them on the same creature - so a repeated creature is kept, not merged
                case Reach.Creatures:
                    return aim.Creatures
                              .Take(effect.TargetsAt(spell.Level, castAt, caster.Actor.Level))
                              .ToList();

                default:
                {
                    // Spiritual Weapon: the creature beside the force - the one named, or the
                    // nearest foe beside it when the aim named only the square
                    if (effect.NearZone > 0 && fight != null)
                    {
                        Cell? force = aim.Square ??
                                      ZonesOf(caster.Actor).FirstOrDefault(z => z.Source == spell.Id)?.Centre;

                        if (!force.HasValue) return Array.Empty<Actor>();

                        IEnumerable<Actor> beside = fight.Field.Pieces
                            .Where(a => !a.IsDown && fight.Field.Where(a) is Cell at &&
                                        Battlefield.Distance(at, force.Value) <= effect.NearZone);

                        return aim.Creatures.Count > 0
                            ? aim.Creatures.Where(beside.Contains).Take(1).ToList()
                            : beside.Where(a => a.Side != caster.Actor.Side)
                                    .OrderBy(a => a.Health.Current)
                                    .ThenBy(a => a.Id, StringComparer.Ordinal)
                                    .Take(1).ToList();
                    }

                    return aim.Creatures.Take(1).ToList();
                }
            }
        }

        // every zone of magical darkness the effect's area reaches ends, and the spell holding it
        // with it
        void Brighten(Encounter fight, SpellEffect effect, Aim aim, List<Cell> covered)
        {
            var area = new HashSet<Cell>(covered);

            if (effect.Reach == Reach.Burst)
                foreach (Cell centre in aim.Squares.Take(Math.Max(1, effect.Points)))
                    area.UnionWith(fight.Field.Burst(centre, effect.Radius));

            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight &&
                                                                           z.zone.Magical)
                                                               .ToList())
            {
                if (!zone.Squares(fight.Field).Any(area.Contains)) continue;

                if (zone.Owner.Concentrating == zone.Source) Release(zone.Owner);
                else
                {
                    fight.EndZone(zone);
                    _zones.Remove((where, zone));
                }
            }
        }

        // whether this creature is one the effect can touch at all: Hold Person's Humanoid, an
        // elf's Trance against Sleep, Power Word Stun's 150 hit points. a creature it cannot touch
        // is untouched - no save, nothing lands - the way SRD's "is unaffected" reads
        static bool Touches(SpellEffect effect, Actor target)
        {
            if (effect.OnlyTags.Count > 0 && !effect.OnlyTags.Any(target.Is)) return false;

            if (effect.ExceptTags.Any(target.Is)) return false;

            if (effect.OnlyAtOrBelow > 0 && target.Health.Current > effect.OnlyAtOrBelow) return false;

            if (effect.OnlyAbove > 0 && target.Health.Current <= effect.OnlyAbove) return false;

            if (effect.NeedsSight && target.Has(Condition.Blinded)) return false;

            if (effect.MaxSize.HasValue && target.CurrentSize > effect.MaxSize.Value) return false;

            return true;
        }

        Landing Apply(Caster caster, Spell spell, SpellEffect effect, Aim aim, Actor target,
                      int castAt, Encounter fight, Moment answering,
                      Dictionary<Actor, Attempt> saves)
        {
            if (!Touches(effect, target)) return new Landing(effect, target, false);

            // SRD 5.2.1 Globe of Invulnerability: a spell cast from outside the barrier cannot
            // affect anything inside it
            if (fight != null && Shielded(fight, caster.Actor, target, castAt))
                return new Landing(effect, target, false);

            DiceRoll amount = effect.AmountAt(spell.Level, castAt, caster.Actor.Level);

            // Cure Wounds' "+ your spellcasting ability modifier" - once, not per die
            int modifier = effect.AddsModifier ? caster.Actor.AbilityModifier(caster.Ability) : 0;

            // an attack roll, a saving throw, or neither - and never both
            Attempt attempt = null;
            bool landed = true;

            if (effect.AttackRoll)
            {
                bool close = fight == null || fight.Field.Distance(caster.Actor, target) <= 1 ||
                             effect.NearZone > 0;

                // a ranged spell attack beside an enemy that can see you is at disadvantage, the same
                // as a bow (SRD 5.2.1 Ranged Attacks in Close Combat)
                bool ranged = spell.Range > 1 && effect.NearZone == 0;

                Advantage lean = Strike.Lean(caster.Actor, target, close,
                                             fight?.Sees(caster.Actor, target),
                                             fight?.Sees(target, caster.Actor),
                                             ranged && fight != null && fight.Crowded(caster.Actor)
                                                 ? Advantage.Disadvantage
                                                 : Advantage.Flat,
                                             fight?.FearInSight(caster.Actor));

                int cover = fight?.Cover(caster.Actor, target) ?? 0;

                attempt = _resolver.Resolve(RollKind.Attack, caster.AttackModifier,
                                            target.ArmorClass + cover, lean, caster.Actor);

                caster.Actor.Boons.Attacked();
                target.Boons.AttackedAt();
                Unveil(caster.Actor, fight);

                attempt = Strike.Closing(attempt, target, close);

                // a spell attack is an attack roll, and SRD's Shield answers any attack roll that
                // hits - so the same window a sword gets, and the same re-reading afterwards
                if (attempt.Succeeded && fight != null)
                {
                    fight.Offer(Moment.Hit(caster.Actor, target, attempt), target);
                    attempt = attempt.Rejudged(target.ArmorClass + cover);
                }

                attempt = Strike.Decoyed(_resolver, caster.Actor, target, attempt);

                landed = attempt.Succeeded;
            }
            else if (effect.Save.HasValue &&
                     !(effect.SaveIfUnwilling && target.Side == caster.Actor.Side))
            {
                // Charm Person: "with Advantage if you or your allies are fighting it"
                Advantage extra = effect.AdvantageIfFought && fight != null &&
                                  target.Side != caster.Actor.Side
                    ? Advantage.Advantage
                    : Advantage.Flat;

                // Flesh to Stone's Construct: a save it makes without rolling
                bool automatic = effect.AutoSaveTags.Any(target.Is);

                // Blight's Plant: a save it fails without rolling
                bool fails = effect.AutoFailTags.Any(target.Is);

                // Shatter's Construct: the save at disadvantage
                if (effect.DisadvantageTags.Any(target.Is))
                    extra = Advantages.Of(extra == Advantage.Advantage, true);

                // Fey Ancestry, Brave, Dwarven Resilience: advantage on a save against the condition
                if (effect.Kind == Primitive.Afflict && target.HasAdvantage("save_vs:" + effect.Condition.Id()))
                    extra = extra.And(Advantage.Advantage);

                // SRD cover adds to Dexterity saves against what comes from the far side of it
                int cover = effect.Save == Ability.Dexterity && fight != null
                    ? fight.Cover(caster.Actor, target)
                    : 0;

                attempt = effect.SameSave && saves.TryGetValue(target, out Attempt earlier)
                    ? earlier
                    : automatic
                        ? new Attempt(RollKind.Save, D20Roll.Fixed(20, 0), int.MinValue)
                        : fails
                            ? new Attempt(RollKind.Save, D20Roll.Fixed(1, 0), int.MaxValue)
                            : Checks.Save(_resolver, target, effect.Save.Value, DcFor(caster, spell) - cover,
                                          extra);

                saves[target] = attempt;

                // what lands on a success rather than a failure lands the other way round
                landed = effect.OnSave == OnSave.OnSuccess ? attempt.Succeeded : attempt.Failed;

                // Sleet Storm: the failed save costs the creature its concentration
                if (landed && effect.BreaksConcentration) Release(target);
            }

            switch (effect.Kind)
            {
                case Primitive.Damage:
                {
                    // a missed attack roll deals nothing, the same as a negating save. this used to
                    // check only the save, so a Fire Bolt that missed still burned for full damage
                    // SRD 5.2.1 Potent Cantrip (p.82): a damaging cantrip that misses, or is saved
                    // against, still does half its damage and nothing else
                    bool potent = !landed && spell.IsCantrip && caster.Actor.Is("potent_cantrip") &&
                                  (effect.AttackRoll || effect.OnSave == OnSave.Negates);

                    if (!landed && !potent && (effect.AttackRoll || effect.OnSave == OnSave.Negates))
                        return new Landing(effect, target, false, 0, attempt);

                    DamageType type = effect.ChosenDamageType ? aim.DamageType : effect.DamageType;

                    // Shield: "you take no damage from Magic Missile"
                    if (target.Boons.Wards(spell.Id))
                        return new Landing(effect, target, false, 0, attempt);

                    // Power Word Kill: at or below the line, no damage roll - it simply dies. a
                    // Death Ward stops it, and is spent doing so
                    if (effect.SlaysAtOrBelow > 0 && !target.IsDown &&
                        target.Health.Current <= effect.SlaysAtOrBelow)
                    {
                        Boon ward = target.Boons.DeathWard;

                        if (ward != null)
                        {
                            target.Boons.Remove(ward);
                            return new Landing(effect, target, false, 0, attempt);
                        }

                        int had = target.Health.Current;

                        target.Suffer(had, DamageType.None);
                        if (target.Side == Allegiance.Hero) target.Perish();

                        return new Landing(effect, target, true, had, attempt);
                    }

                    // Searing Smite's burning, Vitriolic Sphere's second splash: nothing now, a
                    // placement the target's turns will settle
                    if (effect.Recurs || effect.Delayed)
                    {
                        Place(new Placement
                        {
                            Target = target, Caster = caster, Spell = spell.Id, Level = castAt,
                            Dc = DcFor(caster, spell), Duration = effect.Delayed ? Duration.NextTurnEnd
                                                                         : effect.Duration,
                            Burns = effect.Recurs ? amount : default,
                            Later = effect.Delayed ? amount : default,
                            DamageType = type,
                            EndSave = effect.EndSave,
                        }, fight);

                        if (effect.Duration == Duration.Concentration)
                            Remember(caster.Actor, target, spell.Id);

                        return new Landing(effect, target, true, 0, attempt);
                    }

                    int rolled = Math.Max(0, _resolver.Roll(amount, caster.Actor, out _lastFaces)) + modifier;

                    // Call Lightning's storm
                    if (effect.BonusIf.Length > 0 && fight != null && fight.Setting.Contains(effect.BonusIf))
                        rolled += Math.Max(0, _resolver.Roll(effect.BonusAmount, caster.Actor));

                    // SRD: a critical doubles a spell's damage dice the same as a weapon's - and a
                    // smite's dice are the hit's, so its critical doubles them too
                    bool critical = attempt != null && attempt.IsCritical ||
                                    answering?.Trigger == Trigger.Struck &&
                                    answering.Attempt != null && answering.Attempt.IsCritical;

                    if (critical) rolled += Math.Max(0, _resolver.Roll(amount, caster.Actor));

                    // Divine Smite's extra die against a fiend or an undead
                    if (!effect.ExtraAmount.IsNothing && effect.ExtraAgainst.Any(target.Is))
                    {
                        DiceRoll extraDice = critical ? effect.ExtraAmount.Doubled() : effect.ExtraAmount;
                        rolled += Math.Max(0, _resolver.Roll(extraDice, caster.Actor));
                    }

                    // SRD 5.2.1 Empowered Evocation (p.82): the Intelligence modifier on one damage
                    // roll of an Evocation spell - the first it rolls
                    if (spell.School == School.Evocation && caster.Actor.Is("empowered_evocation") &&
                        !_empoweredThisCast)
                    {
                        rolled += Math.Max(0, caster.Actor.AbilityModifier(caster.Ability));
                        _empoweredThisCast = true;
                    }

                    // SRD 5.2.1 Evasion (p.63): a Dexterity save for half is none on a success and
                    // half on a failure - while the creature can act
                    bool evades = effect.Save == Ability.Dexterity && effect.OnSave == OnSave.Half &&
                                  target.Is("evasion") && !target.IsIncapacitated;

                    if (evades) rolled = landed ? rolled / 2 : 0;
                    else if (!landed && effect.OnSave == OnSave.Half) rolled /= 2;

                    if (potent) rolled /= 2;

                    int suffered = target.Suffer(rolled, type);

                    // a mark pays out on any attack roll that hits, a spell's included
                    if (effect.AttackRoll && landed)
                        foreach (Boon mark in target.Boons.MarksFrom(caster.Actor).ToList())
                        {
                            DiceRoll dice = attempt.IsCritical ? mark.Mark.Doubled() : mark.Mark;
                            int extra = Math.Max(0, _resolver.Roll(dice, caster.Actor));

                            rolled += extra;
                            suffered += target.Suffer(extra, mark.MarkType);
                        }

                    if (fight != null && suffered > 0) fight.Hurt(caster.Actor, target, suffered);

                    // Disintegrate: brought to 0 by it, the creature is gray dust
                    if (effect.Dust && suffered > 0 && target.Health.Current <= 0)
                    {
                        target.TurnToDust();
                        if (target.Side == Allegiance.Hero) target.Perish();
                    }

                    // Moonbeam: a failed save turns a shape-shifted creature back, and it can't
                    // shift again until it is out of the beam
                    if (effect.RevertsShape && attempt != null && attempt.Failed && target.IsShifted)
                    {
                        target.Revert();
                        target.Boons.Add(new Boon(RevertedId(spell.Id), spell.Id, Duration.Concentration)
                                         { NoShifting = true });
                    }

                    // Finger of Death: a Humanoid it kills rises at the start of the caster's next
                    // turn, on the caster's side
                    if (effect.RaisesAs.Length > 0 && fight != null && suffered > 0 &&
                        (effect.RaisesTag.Length == 0 || target.Is(effect.RaisesTag)) &&
                        (target.IsDead || target.IsDown && target.Side != Allegiance.Hero))
                        Place(new Placement
                        {
                            Target = target, Caster = caster, Spell = spell.Id, Level = castAt,
                            Duration = Duration.Encounter, RaisesAs = effect.RaisesAs,
                        }, fight);

                    // Murmur of Dread: a failed save sends it running on its own reaction
                    if (effect.ReactionFlee && attempt != null && attempt.Failed && fight != null &&
                        !target.IsDown && target.CanAct && fight.TakeReaction(target))
                        RunFrom(fight, new Turn(target, fight.BudgetFor(target), fight.Round),
                                caster.Actor);

                    // Chromatic Orb: two matching dice and it leaps
                    // (the leap is resolved by the caller, which has the other targets)

                    // a save-for-half that was made still landed *something*, and the report has
                    // to say so or the log reads as a miss
                    return new Landing(effect, target,
                                       landed || effect.OnSave == OnSave.Half || suffered > 0,
                                       suffered, attempt);
                }

                case Primitive.Heal:
                {
                    // Raise Dead: the dead come back with the amount, and nothing else heals them
                    // Disintegrate's dust is past Raise Dead (only True Resurrection or Wish)
                    if (effect.Revives && target.Dust)
                        return new Landing(effect, target, false);

                    if (effect.Revives && target.IsDead)
                    {
                        int at = Math.Max(1, _resolver.Roll(amount, caster.Actor) + modifier);

                        target.Raise(at);

                        return new Landing(effect, target, true, target.Health.Current);
                    }

                    // SRD 5.2.1 Supreme Healing (p.40): the healing dice at their highest
                    int dice = caster.Actor.Is("supreme_healing")
                        ? amount.Maximum
                        : _resolver.Roll(amount, caster.Actor);

                    // Disciple of Life (p.40): a spell slot's healing restores 2 + the slot's level more
                    int disciple = castAt >= 1 && caster.Actor.Is("disciple_of_life") ? 2 + castAt : 0;

                    int healed = target.Mend(Math.Max(0, dice + modifier + disciple));

                    // Blessed Healer (p.40): healing someone else with a slot heals you 2 + its level
                    if (healed > 0 && castAt >= 1 && !ReferenceEquals(target, caster.Actor) &&
                        caster.Actor.Is("blessed_healer") && !_blessedThisCast)
                    {
                        caster.Actor.Mend(2 + castAt);
                        _blessedThisCast = true;
                    }

                    return new Landing(effect, target, healed > 0, healed);
                }

                case Primitive.Ward:
                {
                    int ward = Math.Max(0, _resolver.Roll(amount, caster.Actor));

                    target.Health.GrantTemporary(ward);

                    return new Landing(effect, target, ward > 0, ward);
                }

                case Primitive.Strike:
                    return Swing(caster, spell, effect, aim, target, fight);

                // the items are the content layer's to hand over: Casting.Conjured
                case Primitive.Conjure:
                    return new Landing(effect, target, true, Math.Max(1, effect.Count));

                case Primitive.Direct:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    // done at the start of its next turn, and then it is over
                    Place(new Placement
                    {
                        Target = target, Caster = caster, Spell = spell.Id, Level = castAt,
                        Dc = DcFor(caster, spell), Duration = Duration.NextTurnEnd, Owner = target,
                        Command = effect.Command,
                    }, fight);

                    if (fight != null) Watch(fight);

                    return new Landing(effect, target, true, 0, attempt);
                }

                case Primitive.Disarm:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    // Telekinesis: the caster's spellcasting ability against the holder's
                    if (effect.Contest.HasValue)
                    {
                        Attempt mine = Checks.Check(_resolver, caster.Actor, caster.Ability, 0);
                        Attempt theirs = Checks.Check(_resolver, target, effect.Contest.Value, 0);

                        if (mine.Total <= theirs.Total)
                            return new Landing(effect, target, false, 0, mine);

                        attempt = mine;
                    }

                    target.Disarm(aim.Square ?? fight?.Field.Where(target));

                    return new Landing(effect, target, true, 0, attempt);
                }

                // Misty Step, Dimension Door: the caster goes to the aimed square
                case Primitive.Shift when effect.Reach == Reach.Caster:
                {
                    if (fight == null || !aim.Square.HasValue)
                        return new Landing(effect, target, true);

                    Cell to = aim.Square.Value;

                    // a seen, empty square within the spell's range - or, for Dimension Door, one
                    // it can visualize or describe
                    bool reachable = fight.Field.Where(target) is Cell from &&
                                     Battlefield.Distance(from, to) <= spell.RangeAt(caster.Actor.Level) &&
                                     (effect.Unseen || fight.Field.CanSee(from, to));

                    // SRD 5.2.1 Dimension Door: the one willing creature beside you, to a space
                    // within 5 feet of where you arrive
                    Actor rider = effect.Passenger
                        ? aim.Creatures.FirstOrDefault(a => !ReferenceEquals(a, target) &&
                                                            fight.Field.Distance(target, a) <= 1)
                        : null;

                    Cell? riderTo = rider == null
                        ? null
                        : fight.Field.Map.Cells.Where(c => fight.Field.Map.IsPassable(c) && c != to &&
                                                           Battlefield.Distance(c, to) <= 1 &&
                                                           !fight.Field.Occupies(c, rider))
                                   .OrderBy(c => c.Y).ThenBy(c => c.X)
                                   .Select(c => (Cell?)c).FirstOrDefault();

                    // arriving in an occupied space: 4d6 force to each traveller, and it fails
                    bool blocked = fight.Field.Occupies(to, target) || !fight.Field.Map.IsPassable(to) ||
                                   rider != null && !riderTo.HasValue;

                    if (reachable && effect.Unseen && blocked)
                    {
                        int hurt = 0;

                        foreach (Actor traveller in new[] { target, rider }.Where(a => a != null))
                        {
                            int took = traveller.Suffer(Math.Max(0, _resolver.Roll(DiceRoll.Parse("4d6"), caster.Actor)),
                                                        DamageType.Force);
                            fight.Hurt(caster.Actor, traveller, took);
                            hurt += took;
                        }

                        return new Landing(effect, target, false, hurt);
                    }

                    if (!reachable || blocked)
                        return new Landing(effect, target, false);

                    // Forcecage: magical travel out of it needs a Charisma save
                    if (effect.Teleports && Caged(fight, target, to) is SpellZone cage)
                    {
                        Attempt save = Checks.Save(_resolver, target, Ability.Charisma,
                                                   cage.Caster.SaveDc);

                        if (save.Failed) return new Landing(effect, target, false, 0, save);
                    }

                    if (!fight.Field.Place(target, to)) return new Landing(effect, target, false);

                    fight.Observer.Moved(target, new[] { to });

                    if (rider != null && riderTo.HasValue && fight.Field.Place(rider, riderTo.Value))
                        fight.Observer.Moved(rider, new[] { riderTo.Value });

                    return new Landing(effect, target, true);
                }

                // Thunderwave: pushed straight away from the caster, stopped by walls and bodies
                case Primitive.Shift when effect.Push > 0:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    if (fight == null || !(fight.Field.Where(caster.Actor) is Cell from) ||
                        !(fight.Field.Where(target) is Cell at))
                        return new Landing(effect, target, landed, 0, attempt);

                    int dx = Math.Sign(at.X - from.X);
                    int dy = Math.Sign(at.Y - from.Y);
                    Cell to = at;

                    for (int i = 0; i < effect.Push; i++)
                    {
                        var next = new Cell(to.X + dx, to.Y + dy);

                        if (!fight.Field.Map.IsPassable(next) ||
                            (dx == 0 || dy == 0) && !fight.Field.Map.CanCross(to, next))
                            break;

                        if (fight.Field.Occupies(next, target)) break;

                        to = next;
                    }

                    bool pushed = to != at && fight.Shove(target, to);

                    return new Landing(effect, target, pushed, 0, attempt);
                }

                case Primitive.Shift when effect.Reach == Reach.Creature ||
                                          effect.Reach == Reach.Creatures:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    if (fight == null || !aim.Square.HasValue)
                        return new Landing(effect, target, landed, 0, attempt);

                    Cell to = aim.Square.Value;
                    Cell? from = fight.Field.Where(target);
                    Cell? here = fight.Field.Where(caster.Actor);

                    bool fits = from.HasValue && here.HasValue &&
                                Battlefield.Distance(from.Value, to) <= Math.Max(1, effect.Length) &&
                                Battlefield.Distance(here.Value, to) <= spell.RangeAt(caster.Actor.Level);

                    bool moved = fits && fight.Shove(target, to);

                    return new Landing(effect, target, moved, 0, attempt);
                }

                case Primitive.Stabilize:
                {
                    bool steadied = target.Stabilize();

                    return new Landing(effect, target, steadied);
                }

                case Primitive.Afflict:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    bool applied = target.Apply(effect.Condition, caster.Actor);

                    // Fear: what it holds falls where it stands
                    if (effect.Disarms && (applied || target.Has(effect.Condition)))
                        target.Disarm(fight?.Field.Where(target));

                    if (applied && effect.Duration == Duration.Concentration)
                        Remember(caster.Actor, target, spell.Id, effect.Condition);

                    if (applied)
                    {
                        Place(new Placement
                        {
                            Target = target, Caster = caster, Spell = spell.Id, Level = castAt,
                            Condition = effect.Condition, Dc = DcFor(caster, spell),
                            Escape = effect.Escape, EscapeSkill = effect.EscapeSkill,
                            EscapeDc = effect.EscapeDc,
                            EndsOnAct = effect.EndsOnAct,
                            WhileInZone = effect.WhileInZone,
                            PermanentAfterRounds = effect.PermanentAfterRounds,
                            Since = fight?.Round ?? 0,
                            RepeatSave = effect.RepeatSave ? effect.Save : effect.EndSave,
                            Duration = effect.Duration,
                            Owner = effect.Until == Until.Caster ? caster.Actor : target,
                            EndsOnDamage = effect.EndsOnDamage,
                            SaveOnDamage = effect.SaveOnDamage,
                            Shakeable = effect.Shakeable,
                            Worsens = effect.Worsens,
                            WorsensAfter = Math.Max(1, effect.WorsensAfter),
                            EndsAfter = Math.Max(1, effect.EndsAfter),
                            Flees = effect.Flees,
                            RepeatUnseen = effect.RepeatUnseen,
                            // the save that put it there counts: Sleep worsens on the second
                            // failure, Flesh to Stone on the third
                            Fails = attempt != null && attempt.Kind == RollKind.Save &&
                                    attempt.Failed ? 1 : 0,
                        }, fight);

                        if (effect.Pinned) target.Pin(effect.Condition);

                        fight?.Changed(target, effect.Condition, true);
                    }

                    // SRD 5.2.1: an Incapacitated creature's concentration is broken
                    if (applied && target.IsIncapacitated && target.IsConcentrating)
                        Release(target);

                    return new Landing(effect, target, applied, 0, attempt, effect.Condition);
                }

                case Primitive.Relieve:
                {
                    if (effect.RestoresAbilities)
                        return new Landing(effect, target, target.Scores.Restore());

                    bool removed = target.Remove(effect.Condition);

                    // the spell that put it there is over on this creature too
                    foreach (Placement held in _placed.Where(p => ReferenceEquals(p.Target, target) &&
                                                                  p.Condition == effect.Condition)
                                                      .ToList())
                        _placed.Remove(held);

                    return new Landing(effect, target, removed, 0, null, effect.Condition);
                }

                case Primitive.Sway:
                {
                    if (!landed && effect.OnSave != OnSave.None)
                        return new Landing(effect, target, false, 0, attempt);

                    // Aid's 5, rolled once - the maximum and the current both go up by it
                    int raise = effect.RaisesMaximum ? Math.Max(0, _resolver.Roll(amount, caster.Actor)) : 0;

                    target.Boons.Add(Sway(spell, effect, caster, aim, raise));

                    if (raise > 0) target.Mend(raise);

                    if (effect.Duration == Duration.Concentration)
                        Remember(caster.Actor, target, spell.Id);

                    Place(new Placement
                    {
                        Target = target, Caster = caster, Spell = spell.Id, Level = castAt,
                        Dc = DcFor(caster, spell), Duration = effect.Duration,
                        Owner = effect.Until == Until.Caster ? caster.Actor : target,
                        // Slow: a save at the end of each of its turns ends it
                        RepeatSave = effect.RepeatSave ? effect.Save : effect.EndSave,
                        Escape = effect.Escape, EscapeSkill = effect.EscapeSkill,
                        EscapeDc = effect.EscapeDc,
                        GoneAfterRounds = effect.GoneAfterRounds, GoneTags = effect.GoneTags,
                        EndsAtZero = effect.EndsAtZero,
                        Since = fight?.Round ?? 0,
                    }, fight);

                    // Banishment, Maze: off the board until the spell ends on it
                    if (effect.Banishes) fight?.Banish(target, spell.Id);

                    return new Landing(effect, target, true, raise > 0 ? raise : effect.Sway, attempt);
                }

                case Primitive.Dispel:
                {
                    int ended = effect.Curses ? Uncurse(target) : Dispel(caster, target, castAt);

                    return new Landing(effect, target, ended > 0, ended);
                }

                case Primitive.Counter:
                {
                    // only a casting can be countered, and only the one being answered. anywhere
                    // else this is a spell with nothing in front of it
                    bool casting = answering != null && answering.Trigger == Trigger.Cast &&
                                   ReferenceEquals(answering.Source, target);

                    if (!casting) return new Landing(effect, target, false, 0, attempt);

                    if (landed) answering.Stop();

                    return new Landing(effect, target, landed, 0, attempt);
                }

                // Shift, Zone, Illuminate, Reveal, Summon and Narrate change the world rather than
                // the creature: the casting reports them and the campaign or the board layer acts
                default:
                    return new Landing(effect, target, landed, 0, attempt);
            }
        }

        // TRUE STRIKE: one attack with the weapon chosen at the cast, swinging with the caster's
        // spellcasting ability, dealing the weapon's type or the spell's (the better for the
        // caster), with extra dice by cantrip tier. it goes through the fight's own swing, so the
        // target's reactions and every rule of an attack roll apply
        Landing Swing(Caster caster, Spell spell, SpellEffect effect, Aim aim, Actor target,
                      Encounter fight)
        {
            Attack weapon = aim.Weapon;

            if (weapon == null) return new Landing(effect, target, false);

            DamageType type = weapon.DamageType;

            if (effect.RewriteDamageType != DamageType.None &&
                target.DefenseAgainst(effect.RewriteDamageType).Apply(100) >
                target.DefenseAgainst(weapon.DamageType).Apply(100))
                type = effect.RewriteDamageType;

            var attack = new Attack(weapon.Id, weapon.Damage, type, caster.Ability,
                                    proficient: true, reach: weapon.Reach, range: weapon.Range,
                                    longRange: weapon.LongRange, hand: weapon.Hand,
                                    attackBonus: weapon.AttackBonus,
                                    damageBonus: weapon.DamageBonus,
                                    addsAbilityToDamage: weapon.AddsAbilityToDamage);

            var riders = new List<Rider>();

            if (effect.ExtraTiers.Count > 0)
            {
                DiceRoll extra = effect.ExtraTiers[Math.Min(effect.ExtraTiers.Count - 1,
                                                            SpellEffect.CantripTiers(caster.Actor.Level))];

                if (!extra.IsNothing)
                    riders.Add(new Rider(spell.Id, extra,
                                         effect.DamageType == DamageType.None
                                             ? DamageType.Radiant
                                             : effect.DamageType));
            }

            Blow blow;

            if (fight != null)
            {
                if (!fight.Field.InRange(caster.Actor, target, attack.Reaches))
                    return new Landing(effect, target, false);

                blow = fight.Swing(caster.Actor, target, attack,
                                   fight.Band(caster.Actor, target, attack), riders);
            }
            else
            {
                blow = Strike.Make(_resolver, caster.Actor, target, attack, riders: riders);
            }

            return new Landing(effect, target, blow.Hit, blow.Suffered, blow.Attempt);
        }

        static Boon Sway(Spell spell, SpellEffect effect, Caster caster, Aim aim, int raise = 0,
                         string id = null) =>
            new Boon(id ?? spell.Id, spell.Id, effect.Duration,
                     effect.Sway, effect.SwayDice,
                     attacks: (effect.Touches & Sways.Attacks) != 0,
                     saves: (effect.Touches & Sways.Saves) != 0,
                     checks: (effect.Touches & Sways.Checks) != 0,
                     damage: (effect.Touches & Sways.Damage) != 0,
                     armorClass: (effect.Touches & Sways.ArmorClass) != 0 ? effect.Sway : 0,
                     skill: effect.ChosenSkill ? aim.Skill : effect.Skill,
                     advantageOnChecks: (effect.Leans & Leans.AdvantageOnChecks) != 0,
                     disadvantageOnChecks: (effect.Leans & Leans.DisadvantageOnChecks) != 0,
                     advantageOnAttacks: (effect.Leans & Leans.AdvantageOnAttacks) != 0,
                     disadvantageOnAttacks: (effect.Leans & Leans.DisadvantageOnAttacks) != 0,
                     advantageAgainst: (effect.Leans & Leans.AdvantageAgainst) != 0,
                     disadvantageAgainst: (effect.Leans & Leans.DisadvantageAgainst) != 0,
                     save: effect.LeansOn)
            {
                AdvantageOnSaves = (effect.Leans & Leans.AdvantageOnSaves) != 0,
                DisadvantageOnSaves = (effect.Leans & Leans.DisadvantageOnSaves) != 0,
                Resists = effect.Resists,
                Speed = effect.Speed,
                NoOpportunityAttacks = effect.NoOpportunityAttacks,
                Exposed = effect.Exposes,
                // a mark is the caster's by definition - it pays out on *their* hits
                Owner = effect.Until == Until.Caster || !effect.Mark.IsNothing
                    ? caster.Actor
                    : null,
                Once = effect.Once,
                EndsOnAttack = effect.EndsOnAttack,
                Mark = effect.Mark,
                MarkType = effect.DamageType,
                CheckAbility = effect.ChosenAbility ? aim.Ability : effect.LeansOn,
                UnarmoredBase = effect.UnarmoredBase,
                Truesight = effect.Truesight,
                SpeedDoubled = effect.SpeedChange == SpeedChange.Double,
                SpeedHalved = effect.SpeedChange == SpeedChange.Half,
                SpeedZero = effect.SpeedChange == SpeedChange.Zero,
                MaxHitPoints = raise,
                DeathWard = effect.DeathWard,
                NoReactions = effect.NoReactions,
                ActionOrBonus = effect.ActionOrBonus,
                NoActions = effect.NoActions,
                LimitedAction = effect.LimitedAction,
                WeaponDice = effect.WeaponDice,
                WeaponDiceLess = effect.WeaponDiceLess,
                Decoys = effect.Decoys,
                EasesPerLongRest = effect.EasesPerLongRest,
                SizeStep = effect.SizeStep,
                Rewrite = Rewrite(effect, caster),
                FlySpeed = effect.FlySpeed,
                NoAttacks = effect.NoAttacks,
                NoCasting = effect.NoCasting,
                ImmuneTo = effect.Immune,
                WardsSpell = effect.WardsSpell,
                IfSeen = effect.IfSeen,
                NotVsTruesight = effect.NotVsTruesight,
            };

        // Shillelagh: the weapons it names swing with the caster's spellcasting ability and roll
        // the effect's die, which grows at 5, 11 and 17 the way a cantrip's does
        static WeaponRewrite Rewrite(SpellEffect effect, Caster caster)
        {
            if (effect.Weapons.Count == 0) return null;

            DiceRoll die = effect.RewriteDie;

            if (effect.RewriteDieTiers.Count > 0)
            {
                int tier = SpellEffect.CantripTiers(caster.Actor.Level);
                die = effect.RewriteDieTiers[Math.Min(tier, effect.RewriteDieTiers.Count - 1)];
            }

            return new WeaponRewrite(effect.Weapons, caster.Ability, die, effect.RewriteDamageType);
        }


        // --- what is on whom, for Dispel Magic --------------------------------------------------

        // every lasting thing a spell put on a creature, and at what level it was cast. Dispel
        // Magic's rule is about levels - a 3rd-level dispel ends anything of 3rd level or lower
        // and has to beat 10 + the level for anything higher - so the level has to be kept. and
        // since 2026-09-24 everything that happens to it on the creature's turns and when it is
        // hurt: expiry, a burning, a delayed splash, a waking, a worsening
        sealed class Placement
        {
            public Actor Target;
            public Caster Caster;
            public string Spell;
            public int Level;
            public Condition Condition;

            // what it takes to shake it: the caster's DC, and the check or the save
            public int Dc;
            public Ability? Escape;
            public Skill EscapeSkill;
            public int EscapeDc;
            public Ability? RepeatSave;

            // Banishment's full minute, counted from the round it landed
            public int GoneAfterRounds;
            public bool EndsAtZero;
            public bool EndsOnAct;
            public bool WhileInZone;
            public int PermanentAfterRounds;
            public string RaisesAs = "";
            public IReadOnlyList<string> GoneTags = Array.Empty<string>();
            public int Since;

            // how long, and whose turn counts it
            public Duration Duration;
            public Actor Owner;
            public bool Armed;

            public DamageEnds EndsOnDamage;
            public bool SaveOnDamage;
            public bool Shakeable;

            public Condition Worsens;
            public int WorsensAfter = 1;
            public int EndsAfter = 1;
            public int Fails;
            public int Successes;

            // damage at the start of each of the target's turns, and damage once at the end of
            // its next one
            public DiceRoll Burns;
            public DiceRoll Later;
            public DamageType DamageType;
            public Ability? EndSave;

            // the fight it was put on in, when there was one
            public Encounter Fight;

            // a turn decided for it (Command), or a flight every turn (Fear), and the repeat save
            // only out of the caster's sight
            public Command Command;
            public bool Flees;
            public bool RepeatUnseen;

            public bool Timed => Duration == Characters.Duration.NextTurn ||
                                 Duration == Characters.Duration.NextTurnEnd ||
                                 Duration == Characters.Duration.TurnEnd;

            public bool NeedsWatching =>
                Timed || RepeatSave.HasValue || EndSave.HasValue || Command != Command.None ||
                GoneAfterRounds > 0 || EndsAtZero || EndsOnAct || WhileInZone ||
                PermanentAfterRounds > 0 || RaisesAs.Length > 0 ||
                Flees ||
                EndsOnDamage != DamageEnds.None || SaveOnDamage || !Burns.IsNothing ||
                !Later.IsNothing;
        }

        readonly List<Placement> _placed = new();

        void Place(Placement placement, Encounter fight)
        {
            placement.Fight = fight;
            _placed.Add(placement);

            if (fight != null && placement.NeedsWatching) Watch(fight);
        }

        // SRD 5.2.1 Invisibility: it ends "immediately after the target makes an attack roll, deals
        // damage, or casts a spell"
        void Unveil(Actor actor, Encounter fight)
        {
            if (actor == null) return;

            fight?.Reveal(actor);

            foreach (string spell in _placed.Where(p => ReferenceEquals(p.Target, actor) && p.EndsOnAct)
                                            .Select(p => p.Spell).Distinct().ToList())
                Lift(actor, spell, fight);
        }

        static string RevertedId(string spell) => spell + ".reverted";

        // a sway that only helps its bearer: no penalty, no mark, nothing leaning against it
        static bool Kindly(SpellEffect effect) =>
            effect.Sway >= 0 && effect.Mark.IsNothing &&
            (effect.Leans & (Leans.DisadvantageOnAttacks | Leans.AdvantageAgainst |
                             Leans.DisadvantageOnChecks | Leans.DisadvantageOnSaves)) == 0;

        // the DC: the caster's, or 8 + proficiency + the ability the spell names (a Dragonborn's
        // breath is Constitution's, SRD 5.2.1 p.84)
        static int DcFor(Caster caster, Spell spell) =>
            spell?.DcAbility is Ability own
                ? 8 + caster.Actor.ProficiencyBonus + caster.Actor.AbilityModifier(own)
                : caster.SaveDc;

        // a concentration thread let go of without ending what it did: Flesh to Stone's stone
        void Forget(Actor caster, Actor target, string spell)
        {
            if (_held.TryGetValue(caster, out List<Thread> threads))
                threads.RemoveAll(t => ReferenceEquals(t.Target, target) && t.SpellId == spell);
        }

        // SRD 5.2.1 Dispel Magic on "a magical effect", and Disintegrate on "a creation of magical
        // force": every spell with a zone on the square ends - Dispel Magic's at or below the slot,
        // or on a spellcasting check against 10 + its level
        int DispelAt(Caster caster, Cell square, int castAt, Encounter fight, bool forceOnly)
        {
            int ended = 0;

            foreach (SpellZone zone in _zones.Where(z => z.fight == fight && z.zone.Covers(fight.Field, square))
                                             .Select(z => z.zone).ToList())
            {
                if (forceOnly && !zone.Spell.ForceCreation) continue;

                int level = zone.CastAt;

                bool ends = forceOnly || level <= castAt ||
                            Checks.Check(_resolver, caster.Actor, caster.Ability, 10 + level).Succeeded;

                if (!ends) continue;

                Actor owner = zone.Caster.Actor;

                if (owner.Concentrating == zone.Source) Release(owner);
                else EndZones(owner, zone.Source);

                ended++;
            }

            return ended;
        }

        // ending a spell on one creature: its boons, the conditions it put there, its books - and
        // anything it does as it ends (Haste's lethargy)
        void Lift(Actor target, string spell, Encounter fight = null)
        {
            target.Boons.EndFrom(spell);

            List<Placement> going = _placed.Where(p => ReferenceEquals(p.Target, target) &&
                                                       p.Spell == spell).ToList();

            foreach (Placement placed in going.Where(p => p.Condition != Condition.None))
            {
                target.Remove(placed.Condition);
                fight?.Changed(target, placed.Condition, false);
            }

            _placed.RemoveAll(p => ReferenceEquals(p.Target, target) && p.Spell == spell);

            Ending(going.Select(p => p.Caster).FirstOrDefault(c => c != null), spell, target,
                   fight ?? going.Select(p => p.Fight).FirstOrDefault(f => f != null));
        }

        // one placement over - a timed blinding, a sleep shaken off - without touching the rest
        // of what the same spell did to the same creature
        void Drop(Placement placed, Encounter fight)
        {
            _placed.Remove(placed);

            if (placed.Condition == Condition.None) return;

            // another placement may still be holding the same condition on it
            bool held = _placed.Any(p => ReferenceEquals(p.Target, placed.Target) &&
                                         p.Condition == placed.Condition);

            if (held) return;

            placed.Target.Remove(placed.Condition);
            fight?.Changed(placed.Target, placed.Condition, false);
        }

        // the effects a spell has for the moment it ends on a creature
        void Ending(Caster caster, string spellId, Actor target, Encounter fight)
        {
            // Banishment, Maze: it reappears where it left, or on the nearest free square
            if (target != null) fight?.Recall(target, spellId);

            if (caster == null || target == null) return;

            Spell spell = caster.Find(spellId);

            if (spell == null) return;

            List<SpellEffect> last = spell.Effects.Where(e => e.OnEnd).ToList();

            if (last.Count == 0) return;

            var saves = new Dictionary<Actor, Attempt>();

            foreach (SpellEffect effect in last)
                Apply(caster, spell, effect, Aim.At(target), target, spell.Level, fight, null, saves);
        }


        // --- breaking free, and saving again ------------------------------------------------------

        // SRD's "a creature restrained by the webs can take an action to make a Strength
        // (Athletics) check against your spell save DC". the action is spent whatever the check
        // says; null when nothing it is under can be broken this way
        public Attempt BreakFree(Encounter fight, Turn turn, Condition condition)
        {
            if (turn == null || turn.Ended) return null;

            Placement hold = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, turn.Actor) &&
                                                         p.Condition == condition &&
                                                         p.Escape.HasValue);

            return Escape(fight, turn, hold);
        }

        // SRD 5.2.1 Maze: a Study action and a DC 20 Intelligence (Investigation) check; a success
        // escapes, and the spell ends. null when nothing it is under can be escaped this way
        public Attempt Study(Encounter fight, Turn turn)
        {
            if (turn == null || turn.Ended) return null;

            Placement hold = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, turn.Actor) &&
                                                         p.Condition == Condition.None &&
                                                         p.Escape.HasValue);

            return Escape(fight, turn, hold);
        }

        public bool CanStudy(Actor creature) =>
            _placed.Any(p => ReferenceEquals(p.Target, creature) && p.Condition == Condition.None &&
                             p.Escape.HasValue);

        Attempt Escape(Encounter fight, Turn turn, Placement hold)
        {
            if (hold == null || !turn.Take(Spend.Action)) return null;

            Skill skill = hold.EscapeSkill != Skill.None
                ? hold.EscapeSkill
                : hold.Escape == Ability.Dexterity ? Skill.Acrobatics : Skill.Athletics;

            Attempt attempt = Checks.Check(_resolver, turn.Actor, skill,
                                           hold.EscapeDc > 0 ? hold.EscapeDc : hold.Dc);

            if (!attempt.Succeeded) return attempt;

            if (hold.Condition == Condition.None)
            {
                // out of the maze: the spell ends on it, and it comes back
                Lift(turn.Actor, hold.Spell, fight);

                if (hold.Caster != null && hold.Caster.Actor.Concentrating == hold.Spell &&
                    !_placed.Any(p => p.Spell == hold.Spell && ReferenceEquals(p.Caster, hold.Caster)))
                    Release(hold.Caster.Actor);
            }
            else
            {
                turn.Actor.Remove(hold.Condition);
                _placed.Remove(hold);

                fight?.Observer.ConditionChanged(turn.Actor, hold.Condition, false);
            }

            return attempt;
        }

        // SRD 5.2.1 Sleep and Hypnotic Pattern: someone within 5 feet takes an action to shake
        // the creature out of it. true when something was shaken off
        public bool Shake(Encounter fight, Turn turn, Actor sleeper)
        {
            if (turn == null || turn.Ended || sleeper == null || fight == null) return false;

            if (fight.Field.Distance(turn.Actor, sleeper) > 1) return false;

            List<Placement> shaken = _placed.Where(p => ReferenceEquals(p.Target, sleeper) &&
                                                        p.Shakeable).ToList();

            if (shaken.Count == 0 || !turn.Take(Spend.Action)) return false;

            foreach (string spell in shaken.Select(p => p.Spell).Distinct().ToList())
                Lift(sleeper, spell, fight);

            return true;
        }

        // whether anything on this creature could be shaken off - what a "wake them" button asks
        public bool CanBeShaken(Actor creature) =>
            _placed.Any(p => ReferenceEquals(p.Target, creature) && p.Shakeable);

        readonly HashSet<Encounter> _watching = new();

        void Watch(Encounter fight)
        {
            if (fight == null || !_watching.Add(fight)) return;

            fight.TurnStarting += turn => TurnStarts(fight, turn);
            fight.TurnEnding += turn => TurnEnds(fight, turn.Actor);
            fight.Damaged += (attacker, target, amount) => Hurt(fight, attacker, target);
            fight.Damaged += (attacker, target, amount) => Unveil(attacker, fight);
            fight.AttackRolled += attacker => Unveil(attacker, fight);
            fight.Moved += _ => Refresh(fight);
            fight.Stepped += _ => Refresh(fight);
        }

        // somebody's turn is starting: a thing that lasted until then ends; a thing that lasts
        // until the end of this turn is armed; a burning bites its bearer
        void TurnStarts(Encounter fight, Turn turn)
        {
            Actor whose = turn.Actor;

            foreach (Placement placed in _placed.ToList())
            {
                if (!_placed.Contains(placed)) continue;

                // Command: the turn is the word's, and then the word is spent
                if (placed.Command != Command.None && ReferenceEquals(placed.Target, whose))
                {
                    _placed.Remove(placed);

                    if (!turn.Ended && whose.CanAct) Obey(fight, turn, placed);

                    continue;
                }

                // Fear: a Dash away from the caster before anything else
                if (placed.Flees && ReferenceEquals(placed.Target, whose) && !turn.Ended &&
                    placed.Caster != null)
                {
                    if (fight.Dash(turn)) RunFrom(fight, turn, placed.Caster.Actor);
                }

                // SRD 5.2.1 Banishment: held for the full minute, a creature of another plane
                // doesn't come back, and one of this plane does - the spell is over either way
                if (placed.GoneAfterRounds > 0 && ReferenceEquals(placed.Target, whose) &&
                    fight.Round - placed.Since >= placed.GoneAfterRounds)
                {
                    if (placed.GoneTags.Any(whose.Is))
                    {
                        _placed.RemoveAll(p => ReferenceEquals(p.Target, whose) && p.Spell == placed.Spell);
                        whose.Boons.EndFrom(placed.Spell);
                        fight.Dismiss(whose);
                    }
                    else
                    {
                        Lift(whose, placed.Spell, fight);
                    }

                    if (placed.Caster != null && placed.Caster.Actor.Concentrating == placed.Spell &&
                        !_placed.Any(p => p.Spell == placed.Spell && ReferenceEquals(p.Caster, placed.Caster)))
                        Release(placed.Caster.Actor);

                    continue;
                }

                // SRD 5.2.1 Flesh to Stone: held the full minute, the Petrified stays until Greater
                // Restoration ends it - the spell is over and dropping it no longer lifts it
                if (placed.PermanentAfterRounds > 0 && placed.Caster != null &&
                    ReferenceEquals(placed.Caster.Actor, whose) &&
                    fight.Round - placed.Since >= placed.PermanentAfterRounds &&
                    placed.Condition == Condition.Petrified && placed.Target.Has(Condition.Petrified))
                {
                    Actor stone = placed.Target;

                    _placed.RemoveAll(p => ReferenceEquals(p.Target, stone) && p.Spell == placed.Spell);
                    Forget(placed.Caster.Actor, stone, placed.Spell);

                    if (placed.Caster.Actor.Concentrating == placed.Spell) Release(placed.Caster.Actor);

                    continue;
                }

                // Finger of Death: the start of the caster's next turn, and the dead rise
                if (placed.RaisesAs.Length > 0 && placed.Caster != null &&
                    ReferenceEquals(placed.Caster.Actor, whose))
                {
                    _placed.Remove(placed);
                    fight.Raise(placed.Target, placed.RaisesAs, whose);
                    continue;
                }

                bool mine = ReferenceEquals(placed.Owner ?? placed.Target, whose);

                if (placed.Duration == Duration.NextTurn && mine)
                {
                    Drop(placed, fight);
                    continue;
                }

                if (placed.Duration == Duration.NextTurnEnd && mine) placed.Armed = true;

                if (!placed.Burns.IsNothing && ReferenceEquals(placed.Target, whose))
                {
                    int suffered = whose.Suffer(Math.Max(0, _resolver.Roll(placed.Burns, placed.Caster?.Actor)),
                                                placed.DamageType);

                    fight.Hurt(placed.Caster?.Actor, whose, suffered);

                    // SRD 5.2.1 Searing Smite: "takes 1d6 Fire damage and then makes a
                    // Constitution saving throw" - a success ends it
                    if (placed.EndSave.HasValue && _placed.Contains(placed) && !whose.IsDown &&
                        Checks.Save(_resolver, whose, placed.EndSave.Value, placed.Dc).Succeeded)
                        Lift(whose, placed.Spell, fight);
                }
            }

            Drift(fight, whose);
            Refresh(fight);

            Check(fight.Actors);
        }

        // the end of a creature's turn: a delayed splash lands, timed things end, and every spell
        // holding it that allows a save at the end of its turn gets one
        void TurnEnds(Encounter fight, Actor creature)
        {
            foreach (Placement placed in _placed.ToList())
            {
                if (!_placed.Contains(placed)) continue;

                bool mine = ReferenceEquals(placed.Owner ?? placed.Target, creature);

                if (placed.Duration == Duration.TurnEnd ||
                    placed.Duration == Duration.NextTurnEnd && placed.Armed && mine)
                {
                    if (!placed.Later.IsNothing)
                    {
                        int suffered = placed.Target.Suffer(
                            Math.Max(0, _resolver.Roll(placed.Later, placed.Caster?.Actor)), placed.DamageType);

                        fight.Hurt(placed.Caster?.Actor, placed.Target, suffered);
                    }

                    Drop(placed, fight);
                }
            }

            SaveAgain(fight, creature);

            Check(fight.Actors);
        }

        // SRD 5.2.1 Command's five words, on the creature's turn
        void Obey(Encounter fight, Turn turn, Placement word)
        {
            Actor me = turn.Actor;
            Actor caster = word.Caster?.Actor;

            switch (word.Command)
            {
                // toward the caster by the shortest route, the turn over once within 5 feet
                case Command.Approach:
                {
                    if (caster == null || !(fight.Field.Where(caster) is Cell them)) break;

                    Cell? best = fight.Field.Reachable(me, turn.SquaresLeft)
                                      .OrderBy(p => Battlefield.Distance(p.Key, them))
                                      .ThenBy(p => p.Value)
                                      .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                                      .Select(p => (Cell?)p.Key)
                                      .FirstOrDefault();

                    if (best.HasValue &&
                        Battlefield.Distance(best.Value, them) < fight.Field.Distance(me, caster))
                        fight.Walk(turn, best.Value);

                    if (fight.Field.Distance(me, caster) <= 1) fight.Forfeit(turn);
                    break;
                }

                // what it holds falls, and the turn is over
                case Command.Drop:
                    me.Disarm(fight.Field.Where(me));
                    fight.Forfeit(turn);
                    break;

                // the whole turn spent getting away by the fastest means: a Dash and a run
                case Command.Flee:
                    if (caster != null)
                    {
                        fight.Dash(turn);
                        RunFrom(fight, turn, caster);
                    }

                    fight.Forfeit(turn);
                    break;

                // Prone, and the turn is over
                case Command.Grovel:
                    if (me.Apply(Condition.Prone)) fight.Changed(me, Condition.Prone, true);
                    fight.Forfeit(turn);
                    break;

                // no move, no action, no bonus action
                case Command.Halt:
                    fight.Forfeit(turn);
                    break;
            }
        }

        // as far from the caster as the turn's movement goes, by the safest route: the square
        // with the fewest enemies beside it among the furthest
        static void RunFrom(Encounter fight, Turn turn, Actor from)
        {
            Actor me = turn.Actor;

            if (!(fight.Field.Where(from) is Cell them)) return;

            int now = fight.Field.Distance(me, from);

            Cell? away = fight.Field.Reachable(me, turn.SquaresLeft)
                              .Where(p => Battlefield.Distance(p.Key, them) > now)
                              .OrderByDescending(p => Battlefield.Distance(p.Key, them))
                              .ThenBy(p => fight.Field.Adjacent(p.Key)
                                                .Count(a => a.Side != me.Side && !a.IsDown))
                              .ThenBy(p => p.Value)
                              .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                              .Select(p => (Cell?)p.Key)
                              .FirstOrDefault();

            if (away.HasValue) fight.Walk(turn, away.Value);
        }

        // hit points came off a creature: a Sleep ends, a Charm Person ends if it was the
        // caster's side that did it, a Hideous Laughter saves again with advantage
        void Hurt(Encounter fight, Actor attacker, Actor target)
        {
            foreach (Placement placed in _placed.Where(p => ReferenceEquals(p.Target, target))
                                                .ToList())
            {
                if (!_placed.Contains(placed)) continue;

                bool ends = placed.EndsOnDamage == DamageEnds.Any ||
                            placed.EndsOnDamage == DamageEnds.CasterSide && attacker != null &&
                            placed.Caster != null && attacker.Side == placed.Caster.Actor.Side ||
                            // Gaseous Form: it ends on a target that drops to 0 hit points
                            placed.EndsAtZero && target.IsDown;

                if (ends)
                {
                    Lift(target, placed.Spell, fight);
                    continue;
                }

                if (placed.SaveOnDamage && placed.RepeatSave.HasValue)
                    SaveOnce(fight, placed, Advantage.Advantage);
            }

            Check(fight.Actors);
        }

        // the end of a creature's turn: every spell holding it that allows a repeat save gets one
        void SaveAgain(Encounter fight, Actor creature)
        {
            foreach (Placement hold in _placed.Where(p => ReferenceEquals(p.Target, creature) &&
                                                          p.RepeatSave.HasValue)
                                              .ToList())
            {
                if (!_placed.Contains(hold)) continue;

                // Fear: only a creature that ends its turn where it cannot see the caster
                if (hold.RepeatUnseen && hold.Caster != null &&
                    fight.Field.CanSee(creature, hold.Caster.Actor)) continue;

                SaveOnce(fight, hold, Advantage.Flat);
            }
        }

        void SaveOnce(Encounter fight, Placement hold, Advantage extra)
        {
            Actor creature = hold.Target;

            // the same advantage on a save to end it
            if (hold.Condition != Condition.None && creature.HasAdvantage("save_vs:" + hold.Condition.Id()))
                extra = extra.And(Advantage.Advantage);

            Attempt save = Checks.Save(_resolver, creature, hold.RepeatSave.Value, hold.Dc, extra);

            if (save.Succeeded)
            {
                hold.Successes++;

                if (hold.Successes >= hold.EndsAfter) Lift(creature, hold.Spell, fight);

                return;
            }

            hold.Fails++;

            // Sleep's second failure, Flesh to Stone's third: the condition gets worse and the
            // saving stops
            if (hold.Worsens != Condition.None && hold.Fails >= hold.WorsensAfter)
            {
                creature.Remove(hold.Condition);
                fight.Changed(creature, hold.Condition, false);

                creature.Apply(hold.Worsens, hold.Caster?.Actor);
                fight.Changed(creature, hold.Worsens, true);

                ReplaceThread(hold, hold.Worsens);

                hold.Condition = hold.Worsens;
                hold.Worsens = Condition.None;
                hold.RepeatSave = null;
                hold.SaveOnDamage = false;
            }
        }

        // the concentration thread follows a worsened condition, so letting go ends the new one
        void ReplaceThread(Placement hold, Condition now)
        {
            if (hold.Caster == null || !_held.TryGetValue(hold.Caster.Actor, out List<Thread> threads))
                return;

            for (int i = 0; i < threads.Count; i++)
                if (ReferenceEquals(threads[i].Target, hold.Target) &&
                    threads[i].SpellId == hold.Spell && threads[i].Condition == hold.Condition)
                    threads[i] = new Thread(hold.Target, hold.Spell, now);
        }

        // SRD 5.2.1 Remove Curse: every curse on the creature ends, whatever its level
        int Uncurse(Actor target)
        {
            List<string> curses = _placed.Where(p => ReferenceEquals(p.Target, target) &&
                                                     p.Caster != null &&
                                                     p.Caster.Find(p.Spell)?.Curse == true)
                                         .Select(p => p.Spell)
                                         .Distinct()
                                         .ToList();

            foreach (string curse in curses) Lift(target, curse);

            return curses.Count;
        }

        // a Globe of Invulnerability between the caster and the target: the target stands in a
        // spell-blocking zone the caster is outside, and the spell is of a level it stops
        static bool Shielded(Encounter fight, Actor caster, Actor target, int castAt)
        {
            Cell? inside = fight.Field.Where(target);
            Cell? from = fight.Field.Where(caster);

            if (!inside.HasValue || !from.HasValue) return false;

            return fight.Zones.Any(z => z.BlocksSpellsUpTo > 0 && castAt <= z.BlocksSpellsUpTo &&
                                        z.Covers(fight.Field, inside.Value) &&
                                        !z.Covers(fight.Field, from.Value));
        }


        // --- zones --------------------------------------------------------------------------------

        readonly List<(Encounter fight, SpellZone zone)> _zones = new();

        // what a zone's pulse did, for whoever is showing it - the fight log, the table
        public event Action<SpellZone, Actor, Pulse, IReadOnlyList<Landing>> Pulsed;

        void MakeZone(Caster caster, Spell spell, SpellEffect area, Aim aim, int castAt,
                      Encounter fight)
        {
            bool carried = area.Reach == Reach.Caster || area.Reach == Reach.Around;

            Cell? centre = carried ? null
                         : area.OnCaster ? fight.Field.Where(caster.Actor)
                         : aim.Square ?? fight.Field.Where(caster.Actor);

            if (!carried && !centre.HasValue) return;

            var zone = new SpellZone(this, caster, spell, area, castAt, centre)
            {
                Facing = aim.Facing ?? Facing.East,
                Side = aim.Side,
                Ring = aim.Mode == "ring",
            };

            _zones.Add((fight, zone));

            // Forcecage: the outline goes up as walls, and comes down with the zone
            if (area.Encloses != Edge.None)
                zone.Enclose(fight.Field);

            fight.AddZone(zone, area.Duration);

            if (area.Drifts > 0) Watch(fight);

            if (zone.Auras.Any())
            {
                Watch(fight);
                Refresh(fight);
            }
        }

        static string AuraId(string spell) => spell + "_aura";

        // the Forcecage the creature is in, if going to that square would take it out
        SpellZone Caged(Encounter fight, Actor creature, Cell to)
        {
            if (!(fight.Field.Where(creature) is Cell here)) return null;

            return _zones.Where(z => z.fight == fight && z.zone.Area.Encloses != Edge.None)
                         .Select(z => z.zone)
                         .FirstOrDefault(z => z.Within(here) && !z.Within(to));
        }

        // who is inside each aura now: the aura's sways go on whoever stepped in and come off
        // whoever stepped out
        void Refresh(Encounter fight)
        {
            // SRD 5.2.1 Web: "Restrained ... while in the webs"
            foreach (Placement held in _placed.Where(p => p.WhileInZone).ToList())
            {
                SpellZone zone = _zones.Where(z => z.fight == fight && z.zone.Source == held.Spell)
                                       .Select(z => z.zone).FirstOrDefault();

                if (zone == null || !(fight.Field.Where(held.Target) is Cell at) ||
                    !zone.Covers(fight.Field, at))
                    Drop(held, fight);
            }

            // Moonbeam: out of the beam, it can shape-shift again
            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight).ToList())
            {
                if (!zone.Acts.Any(e => e.RevertsShape)) continue;

                string reverted = RevertedId(zone.Source);

                foreach (Actor actor in fight.Actors.Where(a => a.Boons.All.Any(b => b.Id == reverted)))
                    if (!(fight.Field.Where(actor) is Cell at) || !zone.Covers(fight.Field, at))
                        actor.Boons.EndId(reverted);
            }

            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight).ToList())
            {
                List<SpellEffect> auras = zone.Auras.ToList();

                if (auras.Count == 0) continue;

                string id = AuraId(zone.Source);
                var inside = new HashSet<Actor>(fight.CaughtIn(zone));

                foreach (Actor actor in fight.Actors)
                {
                    bool has = actor.Boons.All.Any(b => b.Id == id);

                    if (inside.Contains(actor) && !has)
                        foreach (SpellEffect aura in auras)
                            actor.Boons.Add(Sway(zone.Spell, aura, zone.Caster, Aim.Nothing, 0, id));
                    else if (!inside.Contains(actor) && has)
                        actor.Boons.EndId(id);
                }
            }
        }

        // Cloudkill: at the start of its caster's turn the zone moves this many squares straight
        // away from the caster, washing over whoever it now covers
        void Drift(Encounter fight, Actor whose)
        {
            foreach ((Encounter where, SpellZone zone) in _zones.Where(z => z.fight == fight &&
                                                                           ReferenceEquals(z.zone.Owner, whose) &&
                                                                           z.zone.Area.Drifts > 0 &&
                                                                           z.zone.Centre.HasValue)
                                                               .ToList())
            {
                Cell? from = fight.Field.Where(whose);

                if (!from.HasValue) continue;

                Cell centre = zone.Centre.Value;
                int dx = Math.Sign(centre.X - from.Value.X);
                int dy = Math.Sign(centre.Y - from.Value.Y);

                if (dx == 0 && dy == 0) continue;

                Cell to = centre;

                for (int i = 0; i < zone.Area.Drifts; i++)
                {
                    var next = new Cell(to.X + dx, to.Y + dy);

                    if (!fight.Field.Map.Contains(next)) break;

                    to = next;
                }

                if (to == centre) continue;

                List<Actor> wereIn = fight.CaughtIn(zone).ToList();

                zone.MoveTo(to);
                fight.ZoneMoved(zone, wereIn);
            }
        }

        public IEnumerable<SpellZone> ZonesOf(Actor caster) =>
            _zones.Where(z => ReferenceEquals(z.zone.Owner, caster)).Select(z => z.zone);

        // the zone acting on one creature at one moment: the spell's zone effects whose pulses
        // include it, each resolved against that creature exactly as a cast would, at the level
        // the spell was cast at and with the caster's numbers as they are now
        internal void Pulse(SpellZone zone, Encounter fight, Actor creature, Pulse pulse)
        {
            var landings = new List<Landing>();
            var saves = new Dictionary<Actor, Attempt>();
            Aim aim = Aim.At(creature);

            // Spirit Guardians: radiant or necrotic, as picked at the cast
            if (_chosenType.TryGetValue((zone.Caster.Actor, zone.Spell.Id), out DamageType picked))
                aim = aim.Choosing(picked);

            bool previousLanded = true;

            Cell? standing = fight.Field.Where(creature);

            foreach (SpellEffect effect in zone.Acts.Where(e => e.Pulses.Has(pulse)))
            {
                // Wall of Fire: entering is entering the wall, not the ground beside it
                if (effect.CoreOnly && (!standing.HasValue || !zone.Core(fight.Field, standing.Value)))
                    continue;

                // an effect that follows the one before only comes with it: Stinking Cloud's
                // lost actions come with its Poisoned, and a creature that cannot be poisoned
                // keeps them
                if (effect.Follows && !previousLanded) continue;

                Landing landing = Apply(zone.Caster, zone.Spell, effect, aim, creature, zone.CastAt,
                                        fight, null, saves);

                previousLanded = landing.Landed;
                landings.Add(landing);
            }

            if (landings.Count > 0) Pulsed?.Invoke(zone, creature, pulse, landings);
        }

        void EndZones(Actor caster, string spell)
        {
            // an aura's sways go with it, from everybody it was on
            foreach ((Encounter fight, SpellZone zone) in
                     _zones.Where(z => ReferenceEquals(z.zone.Owner, caster) &&
                                       z.zone.Source == spell && z.zone.Auras.Any()).ToList())
                foreach (Actor actor in fight.Actors)
                    actor.Boons.EndId(AuraId(spell));

            foreach ((Encounter fight, SpellZone zone) in
                     _zones.Where(z => ReferenceEquals(z.zone.Owner, caster) &&
                                       z.zone.Source == spell).ToList())
            {
                fight.EndZones(spell, caster);
                _zones.Remove((fight, zone));
            }
        }

        // SRD 5.2.1 Dispel Magic, per spell on the target: at or below the dispel's level it ends;
        // above it, a check with the caster's spellcasting ability against 10 + that level
        int Dispel(Caster caster, Actor target, int castAt)
        {
            int ended = 0;

            foreach (IGrouping<string, Placement> held in
                     _placed.Where(p => ReferenceEquals(p.Target, target))
                            .GroupBy(p => p.Spell)
                            .ToList())
            {
                int level = held.Max(p => p.Level);

                bool ends = level <= castAt ||
                            Checks.Check(_resolver, caster.Actor, caster.Ability, 10 + level)
                                  .Succeeded;

                if (!ends) continue;

                Lift(target, held.Key);

                ended++;
            }

            return ended;
        }


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
            // whatever it left lying on the board comes up first - a zone is held even when it
            // has not yet put anything on anybody
            EndZones(caster, spellId);

            if (!_held.TryGetValue(caster, out List<Thread> threads)) return;

            // who it was on, and the fight and caster it was cast with - for what it does as it
            // ends (Haste's lethargy)
            var ending = new List<(Caster caster, Actor target, Encounter fight)>();

            foreach (Thread thread in threads.Where(t => t.SpellId == spellId).ToList())
            {
                Placement any = _placed.FirstOrDefault(p => ReferenceEquals(p.Target, thread.Target) &&
                                                            p.Spell == spellId);

                if (any?.Caster != null &&
                    !ending.Any(e => ReferenceEquals(e.target, thread.Target)))
                    ending.Add((any.Caster, thread.Target, any.Fight));
                else if (any?.Caster == null)
                    any?.Fight?.Recall(thread.Target, spellId);

                thread.Target.Boons.EndFrom(spellId);

                if (thread.Condition != Condition.None)
                {
                    thread.Target.Remove(thread.Condition);
                    any?.Fight?.Changed(thread.Target, thread.Condition, false);
                }

                _placed.RemoveAll(p => ReferenceEquals(p.Target, thread.Target) &&
                                       p.Spell == spellId);
            }

            threads.RemoveAll(t => t.SpellId == spellId);

            foreach ((Caster who, Actor target, Encounter fight) in ending)
                Ending(who, spellId, target, fight);
        }

        // the caster went down, or let go on purpose
        public void Release(Actor caster)
        {
            if (caster == null) return;

            string dropped = caster.EndConcentration();

            if (!string.IsNullOrEmpty(dropped)) Unwind(caster, dropped);
        }

        // called every time anything goes down: SRD drops concentration when the caster does,
        // and SRD 5.2.1 when the caster is Incapacitated at all
        public void Check(IEnumerable<Actor> actors)
        {
            foreach (Actor actor in (actors ?? Enumerable.Empty<Actor>()).ToList())
                if ((actor.IsDown || actor.IsIncapacitated) && actor.IsConcentrating)
                    Release(actor);
        }

        public bool IsHolding(Actor caster) => caster != null && caster.IsConcentrating;
    }
}
