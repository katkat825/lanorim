using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Characters
{
    // every boon on one actor, and the arithmetic of adding them up. kept apart from Actor's own
    // numbers so that ending a spell takes exactly its own boons off and nothing else.
    public sealed class Boons
    {
        readonly List<Boon> _boons = new List<Boon>();

        public IReadOnlyList<Boon> All => _boons;

        // whose boons these are: a boon that holds only while its bearer can act asks it
        internal Actor Bearer { get; set; }

        // a boon counts unless it holds only while the bearer can act and the bearer can't: Danger
        // Sense (the same gate as Evasion's, which is still written in code, DamageHandler)
        bool Active(Boon b) => !(b.Spec.UnlessIncapacitated && Bearer != null && Bearer.IsIncapacitated);

        public void Add(Boon boon)
        {
            if (boon != null && boon.Duration != Duration.Instant) _boons.Add(boon);
        }

        public bool Has(string id) => _boons.Any(b => b.Id == id);

        public int EndFrom(string source) => _boons.RemoveAll(b => b.Source == source);

        public int End(Duration duration) => _boons.RemoveAll(b => b.Duration == duration);

        public void Clear() => _boons.Clear();

        // a fight ended: encounter-long boons go, concentration goes with the caster's own
        // bookkeeping, rest-long boons stay
        public void FightOver()
        {
            End(Duration.Encounter);
            End(Duration.NextTurn);
            End(Duration.NextTurnEnd);
            End(Duration.TurnEnd);
        }

        public void Rested()
        {
            End(Duration.Rest);
            FightOver();
        }

        // a long rest: what eases per long rest eases (Raise Dead's penalty shrinks by one and is
        // gone at zero); everything that ends on a rest ends
        public void LongRested()
        {
            var easing = _boons.Where(b => b.Spec.EasesPerLongRest > 0).ToList();

            End(Duration.LongRest);
            Rested();

            foreach (Boon old in easing)
            {
                BoonSpec spec = old.Spec;

                int flat = spec.Flat > 0
                    ? Math.Max(0, spec.Flat - spec.EasesPerLongRest)
                    : Math.Min(0, spec.Flat + spec.EasesPerLongRest);

                if (flat == 0) continue;

                _boons.Add(Boon.Of(spec with { Flat = flat }, old.Id, old.Source));
            }
        }

        public int FlatOnAttacks => _boons.Where(b => b.Spec.Attacks).Sum(b => b.Spec.Flat);

        // the damage every attack gets: a boon narrowed to one ability (Rage's Strength) is not in it
        public int FlatOnDamage =>
            _boons.Where(b => b.Spec.Damage && !b.Spec.Ability.HasValue).Sum(b => b.Spec.Flat);

        // the same, for an attack made with this ability: Rage's bonus is a Strength attack's
        public int FlatOnDamageFor(Ability used) =>
            _boons.Where(b => b.Spec.Damage && b.With(used)).Sum(b => b.Spec.Flat);

        // advantage on an attack made with this ability, less the boon it is forgoing: Brutal
        // Strike gives up Reckless Attack's advantage for one attack
        public bool AdvantageOnAttackWith(Ability? used, string forgoing) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.AdvantageOnAttacks) && b.Id != forgoing && b.With(used));

        // Uncanny Dodge, spent on the hit it answers
        public bool TakeHalving()
        {
            Boon halving = _boons.FirstOrDefault(b => b.Spec.HalvesNextHit);

            if (halving == null) return false;

            _boons.Remove(halving);
            return true;
        }

        public int ArmorClass => _boons.Sum(b => b.Spec.ArmorClass);

        public int FlatOnCheck(Skill skill) =>
            _boons.Where(b => b.TouchesCheck(skill)).Sum(b => b.Spec.Flat);

        public int FlatOnSave(Ability ability) =>
            _boons.Where(b => b.TouchesSave(ability)).Sum(b => b.Spec.Flat);

        public IEnumerable<DiceRoll> DiceOnAttacks =>
            _boons.Where(b => b.Spec.Attacks && !b.Spec.Dice.IsNothing).Select(b => b.Spec.Dice);

        public IEnumerable<DiceRoll> DiceOnCheck(Skill skill) =>
            _boons.Where(b => b.TouchesCheck(skill) && !b.Spec.Dice.IsNothing).Select(b => b.Spec.Dice);

        public IEnumerable<DiceRoll> DiceOnSave(Ability ability) =>
            _boons.Where(b => b.TouchesSave(ability) && !b.Spec.Dice.IsNothing).Select(b => b.Spec.Dice);

        bool Any(Leans lean) => _boons.Any(b => Active(b) && b.Spec.Has(lean));

        // Remarkable Athlete: advantage on initiative
        public bool AdvantageOnInitiative => Any(Leans.AdvantageOnInitiative);

        public bool AnyAdvantageOnChecks => Any(Leans.AdvantageOnChecks);

        public bool AnyDisadvantageOnChecks => Any(Leans.DisadvantageOnChecks);

        public bool AnyAdvantageOnAttacks => Any(Leans.AdvantageOnAttacks);

        public bool AnyDisadvantageOnAttacks => Any(Leans.DisadvantageOnAttacks);

        public bool AnyAdvantageAgainst => Any(Leans.AdvantageAgainst);

        public bool AnyDisadvantageAgainst => Any(Leans.DisadvantageAgainst);

        // the same, for one attacker: a Faerie Fire outline helps only an attacker that sees it,
        // and a Blur doesn't fool one with Truesight
        public bool AdvantageAgainstFrom(bool attackerSees) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.AdvantageAgainst) && (!b.Spec.IfSeen || attackerSees));

        public bool DisadvantageAgainstFrom(Actor attacker) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.DisadvantageAgainst) &&
                            !(b.Spec.NotVsTruesight && attacker != null && attacker.Boons.Truesight));

        public bool Wards(string spell) =>
            !string.IsNullOrEmpty(spell) && _boons.Any(b => b.Spec.WardsSpell == spell);

        public bool AdvantageOnCheck(Ability ability, Skill skill) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.AdvantageOnChecks) && b.LeansOnCheck(ability, skill));

        public bool DisadvantageOnCheck(Ability ability, Skill skill) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.DisadvantageOnChecks) && b.LeansOnCheck(ability, skill));

        public int UnarmoredBase => _boons.Count == 0 ? 0 : _boons.Max(b => b.Spec.UnarmoredBase);

        // a save lean narrowed to saves against a condition is not a lean on every save
        public bool AdvantageOnSave(Ability ability) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.AdvantageOnSaves) && b.Spec.Against == Condition.None &&
                            b.With(ability));

        public bool DisadvantageOnSave(Ability ability) =>
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.DisadvantageOnSaves) && b.Spec.Against == Condition.None &&
                            b.With(ability));

        // Fey Ancestry, Brave, Dwarven Resilience: advantage on a save against this condition
        public bool AdvantageOnSaveAgainst(Condition condition) =>
            condition != Condition.None &&
            _boons.Any(b => Active(b) && b.Spec.Has(Leans.AdvantageOnSaves) && b.Spec.Against == condition);

        // every defense the boons give against this type: a resistance lent by Stoneskin, a
        // dwarf's own, a statblock's immunity
        public IEnumerable<Defense> DefensesAgainst(DamageType type) =>
            _boons.Select(b => b.Spec.Defenses.TryGetValue(type, out Defense d) ? d : Defense.Normal)
                  .Where(d => d != Defense.Normal);

        public int ExtraSpeed => _boons.Sum(b => b.Spec.ExtraSpeed);

        // three questions, not one: a Haste and a Slow together are neither (Actor.Moves)
        public bool SpeedDoubled => _boons.Any(b => b.Spec.SpeedChange == SpeedChange.Double);

        public bool SpeedHalved => _boons.Any(b => b.Spec.SpeedChange == SpeedChange.Half);

        public bool SpeedZero => _boons.Any(b => b.Spec.SpeedChange == SpeedChange.Zero);

        public bool Truesight => _boons.Any(b => b.Spec.Truesight);

        public int SizeStep => _boons.Sum(b => b.Spec.SizeStep);

        public int MaxHitPoints => _boons.Sum(b => b.MaxHitPoints);

        // what any boon on it forbids: Slow's reactions, Gaseous Form's casting
        public bool Forbids(Forbid what) => _boons.Any(b => b.Spec.Has(what));

        public bool ActionOrBonus => _boons.Any(b => b.Spec.ActionOrBonus);

        public bool LimitedAction => _boons.Any(b => b.Spec.LimitedAction);

        // the slowest Fly Speed wins: Gaseous Form's 10 is the target's only way of moving, so a
        // Fly on top of it does not lift it to 60. 0 is not flying
        public int FlySpeed => _boons.Where(b => b.Spec.FlySpeed > 0).Select(b => b.Spec.FlySpeed)
                                     .DefaultIfEmpty(0).Min();

        public bool Immune(Condition condition) => _boons.Any(b => b.Spec.ImmuneTo.Contains(condition));

        // Enlarge's +1d4 and Reduce's -1d4 on a weapon hit
        public IEnumerable<SignedDice> WeaponDice =>
            _boons.Where(b => !b.Spec.WeaponDice.IsNothing).Select(b => b.Spec.WeaponDice);

        public Boon DeathWard => _boons.FirstOrDefault(b => b.Spec.DeathWard);

        public Boon Decoy => _boons.FirstOrDefault(b => b.Decoys > 0);

        // the rewrite that applies to this weapon, if one does
        public WeaponRewrite RewriteFor(string weaponId) =>
            _boons.Select(b => b.Spec.Rewrite)
                  .FirstOrDefault(r => r != null && r.Covers(weaponId));

        public bool Remove(Boon boon) => boon != null && _boons.Remove(boon);

        // every boon with this id, whatever put it there - an aura stepping off someone
        public int EndId(string id) => _boons.RemoveAll(b => b.Id == id);

        public bool Exposed => _boons.Any(b => b.Spec.Exposed);

        // the marks an attacker has on this creature - what its hit adds
        public IEnumerable<Boon> MarksFrom(Actor attacker) =>
            _boons.Where(b => b.Spec.Mark != null && ReferenceEquals(b.Owner, attacker));


        // --- the fight's two ticks, and the one way a boon is used up ---------------------------

        // the bearer just made an attack roll: what that roll leaned on is spent. (being unseen
        // ends through the fight's Reveal and the Invisibility spell's own placement)
        public void Attacked() =>
            _boons.RemoveAll(b => b.Spec.Once && (b.Spec.Attacks || b.Spec.Has(Leans.AdvantageOnAttacks) ||
                                                  b.Spec.Has(Leans.DisadvantageOnAttacks)));

        // an attack roll was just made *at* the bearer: a glimmer that gave it advantage is spent
        public void AttackedAt() =>
            _boons.RemoveAll(b => b.Spec.Once && (b.Spec.Has(Leans.AdvantageAgainst) ||
                                                  b.Spec.Has(Leans.DisadvantageAgainst)));

        // somebody's turn is starting. a NextTurn boon they own ends; a NextTurnEnd boon they own
        // is armed, so the end of this turn - not the end of some earlier one - is what ends it
        public void TurnStarting(Actor whose, Actor bearer)
        {
            _boons.RemoveAll(b => b.Duration == Duration.NextTurn &&
                                  ReferenceEquals(b.Owner ?? bearer, whose));

            foreach (Boon boon in _boons)
                if (boon.Duration == Duration.NextTurnEnd &&
                    ReferenceEquals(boon.Owner ?? bearer, whose))
                    boon.Armed = true;
        }

        public void TurnEnding(Actor whose, Actor bearer) =>
            _boons.RemoveAll(b => b.Duration == Duration.TurnEnd ||
                                  b.Duration == Duration.NextTurnEnd && b.Armed &&
                                  ReferenceEquals(b.Owner ?? bearer, whose));

        public override string ToString() =>
            _boons.Count == 0 ? "no boons" : string.Join(", ", _boons);
    }
}
