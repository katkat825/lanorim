using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;

namespace Core.Characters
{
    // how long something that was done to an actor stays done. v1 has no round timer and no
    // concentration save on damage (decisions_checklist.md section 6), so a duration is one of a
    // handful of words. the two turn-shaped ones are the only ones the fight ticks, and it ticks
    // them at a turn's two edges rather than counting anything.
    public enum Duration
    {
        Instant,

        // held until the caster ends it or is downed
        Concentration,

        // to the end of the fight
        Encounter,

        // until the next short or long rest
        Rest,

        // until the start of its owner's next turn: Shield
        NextTurn,

        // until the end of its owner's next turn: Guiding Bolt's glimmer (the caster's turn),
        // Vicious Mockery's stumble (the target's). "next" is the next turn to *begin* - a boon
        // put on during its owner's own turn does not end when that turn does
        NextTurnEnd,

        // until the end of the turn it was put on during: Stinking Cloud's poisoned, which comes
        // at the start of a turn and lasts only that turn
        TurnEnd,

        // until the next long rest: SRD's 8-hour and 24-hour spells, which a short rest does not
        // end - Aid, Death Ward, Mage Armor
        LongRest,
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
                    bool advantageOnAttacks = false, bool disadvantageOnAttacks = false,
                    bool advantageAgainst = false, bool disadvantageAgainst = false)
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
            AdvantageAgainst = advantageAgainst;
            DisadvantageAgainst = disadvantageAgainst;
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

        // the other side of the same coin: what attacks *at* the bearer roll with. Guiding Bolt's
        // glimmer is advantage against; being unseen is disadvantage against
        public bool AdvantageAgainst { get; }

        public bool DisadvantageAgainst { get; }

        // WHOSE TURN ENDS IT, for the two turn-shaped durations. null is the bearer's own - a
        // Shield on yourself. a Guiding Bolt's glimmer sits on the target and ends on the caster's
        // turn, which is why it is a separate thing from who is wearing it
        public Actor Owner { get; init; }

        // spent on the first roll it touches: Guiding Bolt's "the next attack roll against it",
        // Vicious Mockery's "its next attack roll"
        public bool Once { get; init; }

        // ends the moment its bearer attacks or casts. SRD's Invisible, from the Invisibility
        // spell or a successful Hide, is broken by exactly that
        public bool EndsOnAttack { get; init; }

        // extra damage whenever the Owner hits the bearer with an attack roll: Hex, Hunter's Mark.
        // it rides on the target rather than on the caster because it is about *this* creature
        public DiceRoll Mark { get; init; }

        public DamageType MarkType { get; init; }

        // narrows a check advantage or disadvantage to one ability: Hex's "checks made with the
        // ability you chose"
        public Ability? CheckAbility { get; init; }

        // an unarmored armor class to use instead of 10: Mage Armor's 13 + Dex. only ever read
        // when no armor is worn, and only if it beats what the actor already has
        public int UnarmoredBase { get; init; }

        // advantage or disadvantage on saving throws - Foresight, Enlarge's Strength saves. the
        // Save filter above narrows it to one ability, as it narrows a flat bonus
        public bool AdvantageOnSaves { get; init; }

        public bool DisadvantageOnSaves { get; init; }

        // damage types it halves while it lasts - Stoneskin's three
        public IReadOnlyList<DamageType> Resists { get; init; } = Array.Empty<DamageType>();

        // feet of speed, more or less - Ray of Frost's -10
        public int Speed { get; init; }

        // cannot make opportunity attacks - Shocking Grasp
        public bool NoOpportunityAttacks { get; init; }

        // outlined: whatever would make attacks against it harder does not - Faerie Fire's "can't
        // benefit from the Invisible condition"
        public bool Exposed { get; init; }

        // Truesight: sees the Invisible (and, on a board, through magical darkness) - True Seeing
        public bool Truesight { get; init; }

        // SRD 5.2.1 speed changes that are not a number of feet: Haste doubles, Slow halves, and
        // Hypnotic Pattern or Power Word Stun's fallback make it 0
        public bool SpeedDoubled { get; init; }

        public bool SpeedHalved { get; init; }

        public bool SpeedZero { get; init; }

        // hit point maximum raised (and current hit points with it) while it lasts: Aid
        public int MaxHitPoints { get; init; }

        // the first time the bearer would drop to 0 hit points it drops to 1 instead, and an
        // instant kill that deals no damage is negated; either uses it up. Death Ward
        public bool DeathWard { get; init; }

        // can't take reactions: Slow
        public bool NoReactions { get; init; }

        // on its turns, an action or a bonus action, not both; one attack when it attacks: Slow
        public bool ActionOrBonus { get; init; }

        // can't take an action or a bonus action at all: Stinking Cloud's poisoned
        public bool NoActions { get; init; }

        // one extra action each turn that only buys a single weapon attack, Dash, Disengage or
        // Hide: Haste
        public bool LimitedAction { get; init; }

        // extra dice on weapon attacks only (a spell attack is not a weapon): Enlarge's +1d4. a
        // negative count takes the dice off instead (Reduce), never below 1 damage
        public DiceRoll WeaponDice { get; init; }

        // the weapon dice come off instead: Reduce
        public bool WeaponDiceLess { get; init; }

        // illusory duplicates that may take a hit instead of the bearer: Mirror Image. counted
        // down as they are destroyed, so it is the one boon that changes after it is put on
        public int Decoys { get; set; }

        // a penalty that eases by this much each long rest instead of ending: Raise Dead's -4
        public int EasesPerLongRest { get; init; }

        // size categories up or down while it lasts: Enlarge, Reduce
        public int SizeStep { get; init; }

        // an attack-and-damage rewrite for the named weapons: Shillelagh's club or quarterstaff
        public WeaponRewrite Rewrite { get; init; }

        // the fight has seen the start of the owner's next turn, so the owner's next turn end
        // is the one that counts
        internal bool Armed { get; set; }

        public string NameKey => KeyConventions.FeatureName(Id);

        public bool TouchesCheck(Skill skill) =>
            Checks && (Skill == Skill.None || Skill == skill);

        public bool TouchesSave(Ability ability) =>
            Saves && (!Save.HasValue || Save.Value == ability);

        // an advantage or disadvantage on a check, narrowed by the boon's skill and ability
        public bool LeansOnCheck(Ability ability, Skill skill) =>
            (Skill == Skill.None || Skill == skill) &&
            (!CheckAbility.HasValue || CheckAbility.Value == ability);

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
            var easing = _boons.Where(b => b.EasesPerLongRest > 0).ToList();

            End(Duration.LongRest);
            Rested();

            foreach (Boon old in easing)
            {
                int flat = old.Flat > 0
                    ? Math.Max(0, old.Flat - old.EasesPerLongRest)
                    : Math.Min(0, old.Flat + old.EasesPerLongRest);

                if (flat == 0) continue;

                _boons.Add(new Boon(old.Id, old.Source, old.Duration, flat, old.Dice,
                                    old.Attacks, old.Saves, old.Checks, old.Damage)
                           { EasesPerLongRest = old.EasesPerLongRest });
            }
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

        public bool AnyAdvantageAgainst => _boons.Any(b => b.AdvantageAgainst);

        public bool AnyDisadvantageAgainst => _boons.Any(b => b.DisadvantageAgainst);

        public bool AdvantageOnCheck(Ability ability, Skill skill) =>
            _boons.Any(b => b.AdvantageOnChecks && b.LeansOnCheck(ability, skill));

        public bool DisadvantageOnCheck(Ability ability, Skill skill) =>
            _boons.Any(b => b.DisadvantageOnChecks && b.LeansOnCheck(ability, skill));

        public int UnarmoredBase => _boons.Count == 0 ? 0 : _boons.Max(b => b.UnarmoredBase);

        public bool AdvantageOnSave(Ability ability) =>
            _boons.Any(b => b.AdvantageOnSaves && (!b.Save.HasValue || b.Save.Value == ability));

        public bool DisadvantageOnSave(Ability ability) =>
            _boons.Any(b => b.DisadvantageOnSaves && (!b.Save.HasValue || b.Save.Value == ability));

        public bool Resist(DamageType type) => _boons.Any(b => b.Resists.Contains(type));

        public int Speed => _boons.Sum(b => b.Speed);

        public bool SpeedDoubled => _boons.Any(b => b.SpeedDoubled);

        public bool SpeedHalved => _boons.Any(b => b.SpeedHalved);

        public bool SpeedZero => _boons.Any(b => b.SpeedZero);

        public bool Truesight => _boons.Any(b => b.Truesight);

        public int SizeStep => _boons.Sum(b => b.SizeStep);

        public int MaxHitPoints => _boons.Sum(b => b.MaxHitPoints);

        public bool NoReactions => _boons.Any(b => b.NoReactions);

        public bool ActionOrBonus => _boons.Any(b => b.ActionOrBonus);

        public bool NoActions => _boons.Any(b => b.NoActions);

        public bool LimitedAction => _boons.Any(b => b.LimitedAction);

        public IEnumerable<Boon> WeaponDice => _boons.Where(b => !b.WeaponDice.IsNothing);

        public Boon DeathWard => _boons.FirstOrDefault(b => b.DeathWard);

        public Boon Decoy => _boons.FirstOrDefault(b => b.Decoys > 0);

        // the rewrite that applies to this weapon, if one does
        public WeaponRewrite RewriteFor(string weaponId) =>
            _boons.Select(b => b.Rewrite)
                  .FirstOrDefault(r => r != null && r.Covers(weaponId));

        public bool Remove(Boon boon) => boon != null && _boons.Remove(boon);

        // every boon with this id, whatever put it there - an aura stepping off someone
        public int EndId(string id) => _boons.RemoveAll(b => b.Id == id);

        public bool NoOpportunityAttacks => _boons.Any(b => b.NoOpportunityAttacks);

        public bool Exposed => _boons.Any(b => b.Exposed);

        // the marks an attacker has on this creature - what its hit adds
        public IEnumerable<Boon> MarksFrom(Actor attacker) =>
            _boons.Where(b => !b.Mark.IsNothing && ReferenceEquals(b.Owner, attacker));


        // --- the fight's two ticks, and the two ways a boon is used up ---------------------------

        // the bearer just made an attack roll: what that roll leaned on is spent, and being unseen
        // is over
        public void Attacked() =>
            _boons.RemoveAll(b => b.EndsOnAttack ||
                                  b.Once && (b.Attacks || b.AdvantageOnAttacks ||
                                             b.DisadvantageOnAttacks));

        // the bearer cast a spell. SRD's Invisible ends on that too
        public void Cast() => _boons.RemoveAll(b => b.EndsOnAttack);

        // an attack roll was just made *at* the bearer: a glimmer that gave it advantage is spent
        public void AttackedAt() =>
            _boons.RemoveAll(b => b.Once && (b.AdvantageAgainst || b.DisadvantageAgainst));

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

namespace Core.Characters
{
    // what a Shillelagh does to a weapon while it lasts: which weapons, which ability they swing
    // with, what die they roll, and a damage type the wielder may use instead of the weapon's.
    // the die grows with the caster's level the way a cantrip's does
    public sealed class WeaponRewrite
    {
        public WeaponRewrite(IReadOnlyList<string> weapons, Ability? ability = null,
                             DiceRoll die = default, DamageType damageType = DamageType.None)
        {
            Weapons = weapons ?? Array.Empty<string>();
            Ability = ability;
            Die = die;
            DamageType = damageType;
        }

        // weapon ids it applies to. empty is any weapon
        public IReadOnlyList<string> Weapons { get; }

        public Ability? Ability { get; }

        public DiceRoll Die { get; }

        // None leaves the weapon's own type
        public DamageType DamageType { get; }

        public bool Covers(string weaponId) =>
            Weapons.Count == 0 || Weapons.Contains(weaponId, StringComparer.OrdinalIgnoreCase);
    }
}
