using System.Collections.Generic;
using System.Linq;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Dice;

namespace Content.Screens
{
    // THE CHARACTER SHEET AS SENTENCES (cc_task_cs 1.4): each section's lines in the sheet's order, every value with its
    // label ("Armor Class 15"), as keyed Said lines the way F2's are (Whereabouts). The sheet page shows these words and
    // the screen reader says them, so what is heard is what is on the paper. A table's row is its cells, each a Said
    // too, and its line is the cells said together.
    //
    // An argument may be a key (said in the player's words) or a Said of its own (said whole, then put in): the game
    // renders both (Game.Access.AccessDesk.Words). Every number is SheetView's or the card's; nothing is worked out here
    public static class SheetReading
    {
        public enum Section { Identity, Basics, Abilities, Skills, Turn, Weapons, Spells, Special }

        public static string HeadingKey(Section section, SheetView sheet) => section switch
        {
            Section.Identity => SheetWords.IdentityKey,
            Section.Basics => SheetWords.BasicsKey,
            Section.Abilities => SheetWords.AbilitiesKey,
            Section.Skills => SheetWords.SkillsKey,
            Section.Turn => sheet.InAFight ? SheetWords.TurnInAFightKey : SheetWords.TurnKey,
            Section.Weapons => SheetWords.WeaponsKey,
            Section.Spells => SheetWords.SpellsKey,
            _ => SheetWords.SpecialKey,
        };

        // the front page and the back, in the decisions doc's order. A hero who casts nothing has no Spells section
        public static IReadOnlyList<Section> Front { get; } =
            new[] { Section.Identity, Section.Basics, Section.Abilities, Section.Skills, Section.Turn, Section.Weapons };

        public static IReadOnlyList<Section> Back(SheetView sheet) =>
            sheet.Casts ? new[] { Section.Spells, Section.Special } : new[] { Section.Special };

        public static IReadOnlyList<Said> Lines(Section section, SheetView sheet) => section switch
        {
            Section.Identity => Identity(sheet),
            Section.Basics => Basics(sheet),
            Section.Abilities => sheet.Abilities.SelectMany(a => new[] { Ability(a), Save(a) }).ToList(),
            Section.Skills => sheet.Skills.Select(Skill).Append(Passive(sheet)).ToList(),
            Section.Turn => Turn(sheet),
            Section.Weapons => Weapons(sheet),
            Section.Spells => Spells(sheet),
            _ => Special(sheet),
        };

        // --- identity and the basics ---------------------------------------------------------------------

        static List<Said> Identity(SheetView sheet)
        {
            var lines = new List<Said>
            {
                sheet.SubclassKey != null
                    ? new Said(SheetWords.ClassWithSubclassKey, sheet.ClassKey, sheet.SubclassKey)
                    : new Said(SheetWords.ClassKey, sheet.ClassKey),
                new Said(LevelKey, sheet.Level),
            };

            if (sheet.Experience is int xp)
                lines.Add(sheet.NextLevelAt > 0 ? new Said(XpKey, xp, sheet.NextLevelAt) : new Said(XpTopKey, xp));

            lines.Add(sheet.LineageKey != null
                ? new Said(SheetWords.SpeciesWithLineageKey, sheet.SpeciesKey, sheet.LineageKey)
                : new Said(SheetWords.SpeciesKey, sheet.SpeciesKey));
            lines.Add(new Said(SheetWords.SizeLineKey, sheet.SizeKey));

            if (sheet.SpeciesSpellAbilityKey != null) lines.Add(new Said(SpeciesSpellsKey, sheet.SpeciesSpellAbilityKey));

            lines.Add(new Said(SheetWords.AlignmentKey, sheet.AlignmentKey));
            lines.Add(sheet.BackgroundKey != null
                ? new Said(SheetWords.BackgroundKey, sheet.BackgroundKey)
                : new Said(SheetWords.NoBackgroundKey));

            if (sheet.PendingImprovements > 0) lines.Add(new Said(SheetWords.ImprovementKey, sheet.PendingImprovements));

            return lines;
        }

        static List<Said> Basics(SheetView sheet)
        {
            var lines = new List<Said>
            {
                new Said(ArmorClassKey, sheet.ArmorClass),
                new Said(SheetWords.InitiativeKey, sheet.Initiative),
                new Said(SheetWords.SpeedKey, sheet.Speed),
                new Said(HitPointsKey, sheet.HitPoints, sheet.MaxHitPoints),
            };

            if (sheet.TemporaryHitPoints > 0) lines.Add(new Said(SheetWords.TemporaryKey, sheet.TemporaryHitPoints));

            lines.Add(new Said(SheetWords.HitDiceKey, sheet.HitDice, sheet.MaxHitDice, DieName(sheet.HitDie)));
            lines.Add(new Said(SheetWords.ProficiencyKey, sheet.ProficiencyBonus));

            if (sheet.ConditionKeys.Count == 0) lines.Add(new Said(SheetWords.NoConditionsKey));
            else lines.AddRange(sheet.ConditionKeys.Select(c => new Said(SheetWords.ConditionsKey, c)));

            return lines;
        }

        // "d10": a die's name is the same in every language the game will ship
        public static string DieName(Die die) => "d" + die.Sides();

        // --- abilities and skills ----------------------------------------------------------------------------

        public static Said Ability(AbilityRow row) => new Said(SheetWords.AbilityKey, row.NameKey, row.Score, row.Modifier);

        public static Said Save(AbilityRow row) =>
            new Said(row.SaveProficient ? SheetWords.SaveProficientKey : SheetWords.SaveKey, row.NameKey, row.Save);

        // the same save under its ability's box, where the box says whose it is: "Save +5, proficient"
        public static Said BoxSave(AbilityRow row) =>
            new Said(row.SaveProficient ? SheetWords.BoxSaveProficientKey : SheetWords.BoxSaveKey, row.Save);

        public static Said Skill(SkillRow row) =>
            new Said(row.Training switch
            {
                Training.Expert => SheetWords.SkillExpertKey,
                Training.Proficient => SheetWords.SkillProficientKey,
                _ => SheetWords.SkillKey,
            }, row.NameKey, row.AbilityShortKey, row.Modifier);

        // Passive Perception, the one a solo hero is checked on without rolling
        public static Said Passive(SheetView sheet)
        {
            SkillRow perception = sheet.Skills.First(s => s.Skill == Core.Characters.Skill.Perception);
            return new Said(SheetWords.PassiveKey, perception.NameKey, perception.Passive);
        }

        // --- the turn and the weapons ------------------------------------------------------------------------

        static List<Said> Turn(SheetView sheet)
        {
            var lines = new List<Said>
            {
                new Said(SheetWords.ActionsKey, sheet.Actions),
                new Said(SheetWords.BonusActionsKey, sheet.BonusActions),
                new Said(SheetWords.ReactionsKey, sheet.Reactions),
                new Said(SheetWords.MovementKey, sheet.Movement),
            };

            if (sheet.ExtraActionsBanked > 0) lines.Add(new Said(SheetWords.BankedKey, sheet.ExtraActionsBanked));

            return lines;
        }

        static List<Said> Weapons(SheetView sheet)
        {
            List<Said> lines = sheet.Weapons.Select(w => new Said(SheetWords.WeaponKey, WeaponCells(w).Cast<object>().ToArray()))
                                    .ToList();

            lines.Add(new Said(sheet.SwapCostActions > 0 ? SheetWords.SwapInAFightKey : SheetWords.SwapFreeKey));

            if (sheet.SpareWeapons.Count == 0) lines.Add(new Said(SheetWords.NoSpareKey));

            return lines;
        }

        // a weapon's row: name, to hit, damage, critical, hand, reach or range
        public static IReadOnlyList<Said> WeaponCells(WeaponRow w) => new[]
        {
            new Said(w.NameKey),
            new Said(SheetWords.ToHitKey, w.AttackBonus),
            new Said(SpellCardEffect.DamageKey, w.Damage.ToString(), w.DamageTypeKey),
            new Said(SpellCardEffect.DamageKey, w.CriticalDamage.ToString(), w.DamageTypeKey),
            new Said(SheetWords.HandKey(w.Hand)),
            w.RangeFeet > 0 ? new Said(SheetWords.RangeKey, w.RangeFeet, w.LongRangeFeet) : new Said(SheetWords.ReachKey, w.ReachFeet),
        };

        // --- spells and the special features --------------------------------------------------------------

        static List<Said> Spells(SheetView sheet)
        {
            var lines = new List<Said>
            {
                sheet.ConcentratingKey != null
                    ? new Said(SheetWords.ConcentratingKey, sheet.ConcentratingKey)
                    : new Said(SheetWords.NotConcentratingKey),
            };

            lines.AddRange(sheet.Slots.Select(s => new Said(SheetWords.SlotsKey, s.Level, s.Left, s.Most)));

            if (sheet.Resource == Core.Magic.SpellResourceMode.Points)
                lines.Add(new Said(SheetWords.PointsKey, sheet.PointsLeft, sheet.PointsMost));

            if (sheet.SoloSlots > 0) lines.Add(new Said(SoloSlotKey));

            lines.AddRange(sheet.Spells.Select(c => new Said(SheetWords.SpellLineKey, SpellCells(c).Cast<object>().ToArray())));

            return lines;
        }

        // a spell's row: name, level, attack or save, effect, concentration
        public static IReadOnlyList<Said> SpellCells(SpellCard card) => new[]
        {
            new Said(card.NameKey),
            new Said(card.LevelKey, card.Level),
            Roll(card),
            Effect(card),
            new Said(card.Concentration ? SheetWords.NeedsConcentrationKey : SheetWords.NoConcentrationKey),
        };

        // "+5 to hit", "DC 13 Dexterity", or nothing to roll. Without a caster the card has no numbers: just the save's
        // ability
        public static Said Roll(SpellCard card) =>
            card.RollsToHit ? (card.AttackBonus is int bonus ? new Said(SheetWords.ToHitKey, bonus) : new Said(SheetWords.AttackRollKey))
            : card.Save is Core.Characters.Ability save
                ? (card.SaveDc is int dc ? new Said(SheetWords.SaveDcKey, dc, save.NameKey()) : new Said(SheetWords.SaveAbilityKey, save.NameKey()))
            : new Said(SheetWords.NoRollKey);

        public static Said Effect(SpellCard card) =>
            card.EffectKey == null
                ? new Said(SheetWords.NoEffectKey)
                : new Said(card.EffectKey, card.EffectDice.IsNothing ? "" : card.EffectDice.ToString(), card.EffectWordKey ?? "");

        static List<Said> Special(SheetView sheet)
        {
            List<Said> lines = sheet.Special
                                    .Select(r => new Said(SheetWords.FeatureLineKey, FeatureCells(r).Cast<object>().ToArray()))
                                    .ToList();

            if (sheet.Keepsake is bool ready) lines.Add(new Said(ready ? KeepsakeReadyKey : KeepsakeSpentKey));

            return lines;
        }

        // a feature's row: name, what using it takes, uses left and what brings them back
        public static IReadOnlyList<Said> FeatureCells(SpecialRow row) => new[]
        {
            new Said(row.NameKey),
            new Said(row.UseTimeKey),
            Uses(row),
        };

        public static Said Uses(SpecialRow row) =>
            row.UsesMost > 0 ? new Said(SheetWords.UsesKey, row.UsesLeft, row.UsesMost) : new Said(SheetWords.NoLimitKey);

        // --- the short form's words, which the full sheet says too (ScreenWords in game/) ------------------------

        static string Sheet(string thing) => ScreenKeys.Key("sheet", thing);

        static readonly string LevelKey = Sheet("level");
        static readonly string XpKey = Sheet("xp");
        static readonly string XpTopKey = Sheet("xp_top");
        static readonly string ArmorClassKey = Sheet("armor_class");
        static readonly string HitPointsKey = Sheet("hit_points");
        static readonly string SpeciesSpellsKey = Sheet("species_spells");
        static readonly string SoloSlotKey = Sheet("solo_slot");
        static readonly string KeepsakeReadyKey = Sheet("keepsake_ready");
        static readonly string KeepsakeSpentKey = Sheet("keepsake_spent");
    }
}
