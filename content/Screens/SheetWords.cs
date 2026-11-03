using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Localization;

namespace Content.Screens
{
    // THE FULL CHARACTER SHEET'S WORDS (cc_task_cs Part 1): its section headings, the label on each of its numbers and
    // its two pages. ui.sheet.<thing>. The short form's own keys (ui.sheet.title, level, xp, armor_class and the rest)
    // are game/Screens/ScreenWords; this is what the full sheet added, read by SheetReading and the sheet page alike
    public static class SheetWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("sheet");

        // the two pages: who and how (identity, the numbers, the skills, the turn, the weapons), and the magic and the
        // features. A 1280 x 720 window holds one at a time
        public static readonly string FrontKey = K("front");
        public static readonly string BackKey = K("back");
        public static readonly string ReadAloudKey = K("read_aloud");

        // the section headings, in character_sheet_decisions.md's order
        public static readonly string IdentityKey = K("identity");
        public static readonly string BasicsKey = K("basics");
        public static readonly string AbilitiesKey = K("abilities");
        public static readonly string SkillsKey = K("skills");
        public static readonly string TurnKey = K("turn");
        public static readonly string TurnInAFightKey = K("turn_in_a_fight");
        public static readonly string WeaponsKey = K("weapons");
        public static readonly string SpellsKey = K("spells");
        public static readonly string SpecialKey = K("special");

        // identity
        public static readonly string ClassKey = K("class");
        public static readonly string ClassWithSubclassKey = K("class_with_subclass");
        public static readonly string SpeciesKey = K("species");
        public static readonly string SpeciesWithLineageKey = K("species_with_lineage");
        public static readonly string SizeLineKey = K("size_line");
        public static readonly string AlignmentKey = K("alignment");
        public static readonly string BackgroundKey = K("background");
        public static readonly string NoBackgroundKey = K("no_background");
        public static readonly string ImprovementKey = K("improvement");

        // the basics
        public static readonly string InitiativeKey = K("initiative");
        public static readonly string SpeedKey = K("speed");
        public static readonly string TemporaryKey = K("temporary");
        public static readonly string HitDiceKey = K("hit_dice");
        public static readonly string ProficiencyKey = K("proficiency");
        public static readonly string ConditionsKey = K("conditions");
        public static readonly string NoConditionsKey = K("no_conditions");

        // an ability, its modifier and its save; a skill's line
        public static readonly string AbilityKey = K("ability");
        public static readonly string SaveKey = K("save");
        public static readonly string SaveProficientKey = K("save_proficient");
        public static readonly string BoxSaveKey = K("box_save");
        public static readonly string BoxSaveProficientKey = K("box_save_proficient");
        public static readonly string SkillKey = K("skill");
        public static readonly string SkillProficientKey = K("skill_proficient");
        public static readonly string SkillExpertKey = K("skill_expert");
        public static readonly string PassiveKey = K("passive");

        // the turn
        public static readonly string ActionsKey = K("actions");
        public static readonly string BonusActionsKey = K("bonus_actions");
        public static readonly string ReactionsKey = K("reactions");
        public static readonly string MovementKey = K("movement");
        public static readonly string BankedKey = K("banked");

        // a weapon's line, and Swap's cost
        public static readonly string WeaponKey = K("weapon");
        public static readonly string ReachKey = K("reach");
        public static readonly string RangeKey = K("range");
        public static readonly string SwapInAFightKey = K("swap_in_a_fight");
        public static readonly string SwapFreeKey = K("swap_free");
        public static readonly string NoSpareKey = K("no_spare");

        public static string HandKey(Hand hand) => hand.UiNameKey("hand");

        // the spells
        public static readonly string ConcentratingKey = K("concentrating");
        public static readonly string NotConcentratingKey = K("not_concentrating");
        public static readonly string SlotsKey = K("slots");
        public static readonly string PointsKey = K("points");
        public static readonly string SpellLineKey = K("spell_line");
        public static readonly string ToHitKey = K("to_hit");
        public static readonly string SaveDcKey = K("save_dc");
        public static readonly string SaveAbilityKey = K("save_ability");
        public static readonly string AttackRollKey = K("attack_roll");
        public static readonly string NoRollKey = K("no_roll");
        public static readonly string NoEffectKey = K("no_effect");
        public static readonly string NeedsConcentrationKey = K("needs_concentration");
        public static readonly string NoConcentrationKey = K("no_concentration");

        // the special features
        public static readonly string FeatureLineKey = K("feature_line");
        public static readonly string UsesKey = K("uses");
        public static readonly string NoLimitKey = K("no_limit");

        // the column heads of the tables on the sheet
        public static readonly string ColumnNameKey = K("column_name");
        public static readonly string ColumnAttackKey = K("column_attack");
        public static readonly string ColumnDamageKey = K("column_damage");
        public static readonly string ColumnCriticalKey = K("column_critical");
        public static readonly string ColumnHandKey = K("column_hand");
        public static readonly string ColumnReachKey = K("column_reach");
        public static readonly string ColumnLevelKey = K("column_level");
        public static readonly string ColumnRollKey = K("column_roll");
        public static readonly string ColumnEffectKey = K("column_effect");
        public static readonly string ColumnConcentrationKey = K("column_concentration");
        public static readonly string ColumnUseKey = K("column_use");
        public static readonly string ColumnUsesKey = K("column_uses");

        // a card's labels (the spell's and the feature's), and its Close
        public static readonly string CardCastingTimeKey = K("card_casting_time");
        public static readonly string CardRangeKey = K("card_range");
        public static readonly string CardComponentsKey = K("card_components");
        public static readonly string CardDurationKey = K("card_duration");
        public static readonly string CardRollKey = K("card_roll");
        public static readonly string CardEffectKey = K("card_effect");
        public static readonly string CardCostKey = K("card_cost");
        public static readonly string CardRitualKey = K("card_ritual");
        public static readonly string CardUseKey = K("card_use");
        public static readonly string CardUsesKey = K("card_uses");
        public static readonly string CardRechargeKey = K("card_recharge");
        public static readonly string CardKey = K("card");
        public static readonly string CloseKey = K("close");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                FrontKey, BackKey, ReadAloudKey,
                IdentityKey, BasicsKey, AbilitiesKey, SkillsKey, TurnKey, TurnInAFightKey, WeaponsKey, SpellsKey, SpecialKey,
                ClassKey, ClassWithSubclassKey, SpeciesKey, SpeciesWithLineageKey, SizeLineKey, AlignmentKey, BackgroundKey,
                NoBackgroundKey, ImprovementKey,
                InitiativeKey, SpeedKey, TemporaryKey, HitDiceKey, ProficiencyKey, ConditionsKey, NoConditionsKey,
                AbilityKey, SaveKey, SaveProficientKey, BoxSaveKey, BoxSaveProficientKey, SkillKey, SkillProficientKey, SkillExpertKey, PassiveKey,
                ActionsKey, BonusActionsKey, ReactionsKey, MovementKey, BankedKey,
                WeaponKey, ReachKey, RangeKey, SwapInAFightKey, SwapFreeKey, NoSpareKey,
                ConcentratingKey, NotConcentratingKey, SlotsKey, PointsKey, SpellLineKey, ToHitKey, SaveDcKey, SaveAbilityKey, AttackRollKey, NoRollKey,
                NoEffectKey, NeedsConcentrationKey, NoConcentrationKey,
                FeatureLineKey, UsesKey, NoLimitKey,
                ColumnNameKey, ColumnAttackKey, ColumnDamageKey, ColumnCriticalKey, ColumnHandKey, ColumnReachKey,
                ColumnLevelKey, ColumnRollKey, ColumnEffectKey, ColumnConcentrationKey, ColumnUseKey, ColumnUsesKey,
                CardCastingTimeKey, CardRangeKey, CardComponentsKey, CardDurationKey, CardRollKey, CardEffectKey,
                CardCostKey, CardRitualKey, CardUseKey, CardUsesKey, CardRechargeKey, CardKey, CloseKey,
            }
            .Concat(Enum.GetValues<Hand>().Select(HandKey));
    }
}
