using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;

namespace Content.Sheet
{
    // THE CHARACTER SHEET, AS KEYS AND NUMBERS - character_sheet_decisions.md, section by section.
    // a snapshot: built from a Hero (and, in a fight, the fight and the turn), read by the sheet
    // scene, thrown away and built again when anything changes. nothing here draws, and nothing
    // here decides - every number is the rules' own, so the sheet and the roll cannot disagree.
    //
    // the Godot layout is Kathleen's; this is what each label on it binds to.
    public sealed class SheetView
    {
        SheetView() { }

        // --- identity ------------------------------------------------------------------------

        // typed by the player, so the one string here that is not a key
        public string Name { get; private set; }

        public string ClassKey { get; private set; }

        public string SubclassKey { get; private set; }

        public int Level { get; private set; }

        public string SpeciesKey { get; private set; }

        // the lineage when there is one (a High Elf), else null
        public string LineageKey { get; private set; }

        public string AlignmentKey { get; private set; }

        public string BackgroundKey { get; private set; }

        // ability score improvements the level has given and the player has not spent: the sheet
        // shows "you have an improvement to spend" while this is above 0
        public int PendingImprovements { get; private set; }

        // --- basics --------------------------------------------------------------------------

        public int ArmorClass { get; private set; }

        public int Initiative { get; private set; }

        // feet
        public int Speed { get; private set; }

        public int HitPoints { get; private set; }

        public int MaxHitPoints { get; private set; }

        public int TemporaryHitPoints { get; private set; }

        public int HitDice { get; private set; }

        public int MaxHitDice { get; private set; }

        public Die HitDie { get; private set; }

        public int ProficiencyBonus { get; private set; }

        public IReadOnlyList<string> ConditionKeys { get; private set; }

        // --- abilities and skills ------------------------------------------------------------

        public IReadOnlyList<AbilityRow> Abilities { get; private set; }

        public IReadOnlyList<SkillRow> Skills { get; private set; }

        // --- the action economy --------------------------------------------------------------

        // what is left this turn when it is the hero's turn, what a turn holds when it is not
        public int Actions { get; private set; }

        public int BonusActions { get; private set; }

        public int Reactions { get; private set; }

        // feet left to move this turn, or the whole speed outside a turn
        public int Movement { get; private set; }

        public bool InAFight { get; private set; }

        // Action Surge and anything else banked per rest
        public int ExtraActionsBanked { get; private set; }

        // --- weapons -------------------------------------------------------------------------

        public IReadOnlyList<WeaponRow> Weapons { get; private set; }

        // SWAPPING A WEAPON costs an action in a fight and nothing outside one
        // (character_sheet_decisions.md, weapons)
        public int SwapCostActions => InAFight ? 1 : 0;

        // --- spells --------------------------------------------------------------------------

        public bool Casts { get; private set; }

        // the spell held, or null. while it is set the other concentration spells are greyed
        public string ConcentratingKey { get; private set; }

        public bool CanEndConcentration => ConcentratingKey != null;

        public SpellResourceMode? Resource { get; private set; }

        // slots mode: left and most, by level, index 0 is 1st level. points mode: empty
        public IReadOnlyList<(int Level, int Left, int Most)> Slots { get; private set; }

        // points mode: left and most. slots mode: zero
        public int PointsLeft { get; private set; }

        public int PointsMost { get; private set; }

        public IReadOnlyList<SpellCard> Spells { get; private set; }

        // --- special -------------------------------------------------------------------------

        public IReadOnlyList<SpecialRow> Special { get; private set; }


        public static SheetView Of(Hero hero, Encounter fight = null, Turn turn = null)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            Actor actor = hero.Actor;

            var view = new SheetView
            {
                Name = hero.Name,
                ClassKey = hero.Class.NameKey,
                SubclassKey = hero.Class.Subclass.Length > 0 ? hero.Class.SubclassNameKey : null,
                Level = hero.Level,
                SpeciesKey = hero.Species.NameKey,
                LineageKey = hero.Lineage?.NameKey,
                AlignmentKey = hero.Alignment.NameKey(),
                BackgroundKey = hero.Background?.NameKey,
                PendingImprovements = hero.PendingImprovements,

                ArmorClass = actor.ArmorClass,
                Initiative = actor.AbilityModifier(Ability.Dexterity),
                Speed = actor.Speed,
                HitPoints = actor.Health.Current,
                MaxHitPoints = actor.Health.Maximum,
                TemporaryHitPoints = actor.Health.Temporary,
                HitDice = actor.Health.HitDice,
                MaxHitDice = actor.Health.HitDiceMax,
                HitDie = actor.Health.HitDie,
                ProficiencyBonus = actor.ProficiencyBonus,
                ConditionKeys = actor.Conditions.Select(c => c.NameKey()).ToList(),

                Abilities = Core.Characters.Abilities.All.Select(a => new AbilityRow(actor, a)).ToList(),
                Skills = Core.Characters.Skills.All.Select(s => new SkillRow(actor, s)).ToList(),
            };

            view.ReadEconomy(hero, fight, turn);
            view.ReadWeapons(hero);
            view.ReadSpells(hero);
            view.ReadSpecial(hero);

            return view;
        }

        void ReadEconomy(Hero hero, Encounter fight, Turn turn)
        {
            InAFight = fight != null && !fight.Over;
            ExtraActionsBanked = hero.Budget.ExtraActionsLeft;

            bool mine = turn != null && ReferenceEquals(turn.Actor, hero.Actor) && !turn.Ended;

            // round 2 is "a round like any other": the first-round extras are not shown as if
            // they were always there
            Actions = mine ? turn.Actions : hero.Budget.ActionsFor(2);
            BonusActions = mine ? turn.BonusActions : hero.Budget.BonusActionsFor(2);
            Movement = mine ? turn.Movement : hero.Actor.Speed;

            Reactions = InAFight ? fight.ReactionsLeft(hero.Actor) : hero.Budget.ReactionsFor(2);
        }

        void ReadWeapons(Hero hero)
        {
            Weapons = hero.Attacks.Select(a => new WeaponRow(hero.Actor, a)).ToList();
        }

        void ReadSpells(Hero hero)
        {
            Casts = hero.Casts;
            Slots = Array.Empty<(int, int, int)>();
            Spells = Array.Empty<SpellCard>();

            if (!hero.Casts) return;

            Caster caster = hero.Caster;

            ConcentratingKey = hero.Actor.IsConcentrating
                ? KeyConventions.SpellName(hero.Actor.Concentrating)
                : null;

            Resource = caster.Mode;

            switch (caster.Resource)
            {
                case SpellSlots slots:
                    Slots = Enumerable.Range(SpellLevels.Lowest, SpellLevels.Highest)
                                      .Where(l => slots.Maximum(l) > 0)
                                      .Select(l => (l, slots.Remaining(l), slots.Maximum(l)))
                                      .ToList();
                    break;

                case SpellPoints points:
                    PointsLeft = points.Remaining;
                    PointsMost = points.Maximum;
                    break;
            }

            // cantrips first, then by level, then by name - the order a player looks for them in
            Spells = caster.Known.OrderBy(s => s.Level)
                           .ThenBy(s => s.Id, StringComparer.Ordinal)
                           .Select(s => SpellCard.Of(s, caster))
                           .ToList();
        }

        void ReadSpecial(Hero hero)
        {
            Special = hero.Activatable
                          .Select(f => new SpecialRow(f, hero.UsesLeft(f)))
                          .ToList();
        }

        public override string ToString() =>
            $"{Name}, level {Level}: ac {ArmorClass}, hp {HitPoints}/{MaxHitPoints}, " +
            $"{Actions} actions {BonusActions} bonus {Reactions} reactions, " +
            $"{Weapons.Count} weapons, {Spells.Count} spells, {Special.Count} special";
    }

    public sealed class AbilityRow
    {
        public AbilityRow(Actor actor, Ability ability)
        {
            Ability = ability;
            NameKey = ability.NameKey();
            ShortKey = ability.ShortKey();
            Score = actor.Scores.Score(ability);
            Modifier = actor.AbilityModifier(ability);
            Save = actor.SaveModifier(ability);
            SaveProficient = actor.SavesWith(ability);
        }

        public Ability Ability { get; }

        public string NameKey { get; }

        public string ShortKey { get; }

        public int Score { get; }

        public int Modifier { get; }

        public int Save { get; }

        public bool SaveProficient { get; }
    }

    public sealed class SkillRow
    {
        public SkillRow(Actor actor, Skill skill)
        {
            Skill = skill;
            NameKey = skill.NameKey();
            AbilityShortKey = skill.Governs().ShortKey();
            Modifier = actor.CheckModifier(skill);
            Training = actor.TrainingIn(skill);
            Passive = Core.Rules.Checks.Passive(actor, skill);
        }

        public Skill Skill { get; }

        public string NameKey { get; }

        public string AbilityShortKey { get; }

        public int Modifier { get; }

        public Training Training { get; }

        public int Passive { get; }
    }

    public sealed class WeaponRow
    {
        public WeaponRow(Actor actor, Attack attack)
        {
            Attack = attack;
            NameKey = attack.NameKey;
            AttackBonus = attack.Modifier(actor);
            Damage = attack.DamageFor(actor);
            CriticalDamage = attack.DamageFor(actor, critical: true);
            DamageTypeKey = attack.DamageType.NameKey();
            Hand = attack.Hand;
            RangeFeet = attack.IsRanged ? attack.Range * 5 : 0;
            LongRangeFeet = attack.IsRanged ? attack.LongRange * 5 : 0;
            ReachFeet = attack.IsRanged ? 0 : attack.Reach * 5;
        }

        public Attack Attack { get; }

        public string NameKey { get; }

        public int AttackBonus { get; }

        // the dice and the flat bonus together: "1d8+3"
        public DiceRoll Damage { get; }

        // SRD crit: the dice doubled, the modifier once - "2d8+3"
        public DiceRoll CriticalDamage { get; }

        // v1 crits on a natural 20 and nothing else - no class widens it
        public int CriticalOn => 20;

        public string DamageTypeKey { get; }

        public Hand Hand { get; }

        public int ReachFeet { get; }

        public int RangeFeet { get; }

        public int LongRangeFeet { get; }
    }

    public sealed class SpecialRow
    {
        public SpecialRow(Feature feature, int usesLeft)
        {
            Feature = feature;
            NameKey = feature.NameKey;
            DescriptionKey = feature.DescriptionKey;
            UsesMost = feature.Uses;
            UsesLeft = feature.Uses > 0 ? Math.Max(0, usesLeft) : 0;
        }

        public Feature Feature { get; }

        public string NameKey { get; }

        public string DescriptionKey { get; }

        // zero is "no limit" - a stance you can switch on whenever
        public int UsesMost { get; }

        public int UsesLeft { get; }

        public bool Available => UsesMost == 0 || UsesLeft > 0;
    }
}
