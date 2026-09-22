using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;

namespace Core.Characters
{
    // how long something that was done to an actor stays done. v1 has no round timer and no
    // concentration save on damage (decisions_checklist.md section 6), so a duration is one of
    // four words and nothing has to be ticked down each round.
    public enum Duration
    {
        Instant,

        // held until the caster ends it or is downed
        Concentration,

        // to the end of the fight
        Encounter,

        // until the next short or long rest
        Rest,
    }

    // a timed modifier riding on an actor: Bless's +1d4, Shield's +5 AC, a Bane, Guidance, a
    // potion of heroism. spells, items and class features all make these, which is why it lives
    // here and not in Core.Magic.
    public sealed class Boon
    {
        public Boon(string id, string source = null, Duration duration = Duration.Encounter,
                    int flat = 0, DiceRoll dice = default,
                    bool attacks = false, bool saves = false, bool checks = false,
                    bool damage = false, int armorClass = 0,
                    Skill skill = Skill.None, Ability? save = null,
                    bool advantageOnChecks = false, bool disadvantageOnChecks = false,
                    bool advantageOnAttacks = false, bool disadvantageOnAttacks = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? id;
            Duration = duration;
            Flat = flat;
            Dice = dice;
            Attacks = attacks;
            Saves = saves;
            Checks = checks;
            Damage = damage;
            ArmorClass = armorClass;
            Skill = skill;
            Save = save;
            AdvantageOnChecks = advantageOnChecks;
            DisadvantageOnChecks = disadvantageOnChecks;
            AdvantageOnAttacks = advantageOnAttacks;
            DisadvantageOnAttacks = disadvantageOnAttacks;
        }

        public string Id { get; }

        // the spell or feature that put it there; ending concentration takes every boon with the
        // same source off at once
        public string Source { get; }

        public Duration Duration { get; }

        public int Flat { get; }

        // Bless is +1d4, not +2 - a boon that rolls is rolled fresh each time it applies
        public DiceRoll Dice { get; }

        public bool Attacks { get; }

        public bool Saves { get; }

        public bool Checks { get; }

        public bool Damage { get; }

        public int ArmorClass { get; }

        // when set, the boon only touches that one skill
        public Skill Skill { get; }

        // when set, the boon only touches saves made with that one ability
        public Ability? Save { get; }

        public bool AdvantageOnChecks { get; }

        public bool DisadvantageOnChecks { get; }

        public bool AdvantageOnAttacks { get; }

        public bool DisadvantageOnAttacks { get; }

        public string NameKey => KeyConventions.FeatureName(Id);

        public bool TouchesCheck(Skill skill) =>
            Checks && (Skill == Skill.None || Skill == skill);

        public bool TouchesSave(Ability ability) =>
            Saves && (!Save.HasValue || Save.Value == ability);

        public override string ToString() =>
            Id + (Flat != 0 ? $" {Flat:+0;-0}" : "") +
            (Dice.IsNothing ? "" : $" +{Dice}") +
            (ArmorClass != 0 ? $" ac {ArmorClass:+0;-0}" : "") +
            $" ({Duration.ToString().ToLowerInvariant()})";
    }

    // every boon on one actor, and the arithmetic of adding them up. kept apart from Actor's own
    // numbers so that ending a spell takes exactly its own boons off and nothing else.
    public sealed class Boons
    {
        readonly List<Boon> _boons = new List<Boon>();

        public IReadOnlyList<Boon> All => _boons;

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
        public void FightOver() => End(Duration.Encounter);

        public void Rested()
        {
            End(Duration.Rest);
            End(Duration.Encounter);
        }

        public int FlatOnAttacks => _boons.Where(b => b.Attacks).Sum(b => b.Flat);

        public int FlatOnDamage => _boons.Where(b => b.Damage).Sum(b => b.Flat);

        public int ArmorClass => _boons.Sum(b => b.ArmorClass);

        public int FlatOnCheck(Skill skill) =>
            _boons.Where(b => b.TouchesCheck(skill)).Sum(b => b.Flat);

        public int FlatOnSave(Ability ability) =>
            _boons.Where(b => b.TouchesSave(ability)).Sum(b => b.Flat);

        public IEnumerable<DiceRoll> DiceOnAttacks =>
            _boons.Where(b => b.Attacks && !b.Dice.IsNothing).Select(b => b.Dice);

        public IEnumerable<DiceRoll> DiceOnCheck(Skill skill) =>
            _boons.Where(b => b.TouchesCheck(skill) && !b.Dice.IsNothing).Select(b => b.Dice);

        public IEnumerable<DiceRoll> DiceOnSave(Ability ability) =>
            _boons.Where(b => b.TouchesSave(ability) && !b.Dice.IsNothing).Select(b => b.Dice);

        public bool AnyAdvantageOnChecks => _boons.Any(b => b.AdvantageOnChecks);

        public bool AnyDisadvantageOnChecks => _boons.Any(b => b.DisadvantageOnChecks);

        public bool AnyAdvantageOnAttacks => _boons.Any(b => b.AdvantageOnAttacks);

        public bool AnyDisadvantageOnAttacks => _boons.Any(b => b.DisadvantageOnAttacks);

        public override string ToString() =>
            _boons.Count == 0 ? "no boons" : string.Join(", ", _boons);
    }
}
