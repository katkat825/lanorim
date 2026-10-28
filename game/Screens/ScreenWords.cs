using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Core.Localization;
using Core.Words;

namespace Game.Screens
{
    // THE GODOT SCREENS' OWN WORDS - what the launch screen, creation and the table's menus say that
    // the view models in content/Screens don't. ui.<screen>.<thing>, audited through GameKeys.
    public static class ScreenWords
    {
        static string K(string screen, string thing) => KeyConventions.Key(KeyConventions.UiNs, screen, thing);

        public static readonly string GameTitle = K("launch", "title");
        public static readonly string PressToBegin = K("launch", "press_to_begin");
        public static readonly string Back = K("launch", "back");
        public static readonly string Next = K("launch", "next");
        public static readonly string Begin = K("launch", "begin");
        public static readonly string Saves = K("launch", "saves");
        public static readonly string NoSaves = K("launch", "no_saves");
        public static readonly string Character = K("launch", "character");
        public static readonly string Retire = K("launch", "retire");

        public static string StepKey(Step step) => K("create", "step_" + EnumWords.Name(step));

        public static readonly string CreateTitle = K("create", "title");
        public static readonly string PointsLeft = K("create", "points_left");
        public static readonly string PicksLeft = K("create", "picks_left");
        public static readonly string NameHint = K("create", "name_hint");
        public static readonly string Suggested = K("create", "take_suggested");
        public static readonly string ChosenOf = K("create", "chosen_of");

        // the class page's choice of two figures for the mini (cc_task_h Part 2): named by the figure, nothing else
        public static readonly string Figure = K("create", "figure");

        public static string FigureKey(int figure) => K("create", "figure_" + (figure + 1));

        // the mini's own page (Step.Look)
        public static readonly string LookBodyKey = K("create", "look_body");
        public static readonly string LookSkinKey = K("create", "look_skin");
        public static readonly string LookHairKey = K("create", "look_hair");
        public static readonly string LookOwnKey = K("create", "look_own");
        public static readonly string LookKindKey = K("create", "look_kind");

        // the Traits page's three sections (cc_task_e-shop-species-and-ui-notes.md 1.3)
        public static readonly string TraitSkill = K("create", "trait_skill");
        public static readonly string TraitSpellAbility = K("create", "trait_spell_ability");
        public static readonly string TraitSize = K("create", "trait_size");

        // the Equipment page (1.4)
        public static readonly string KitFromClass = K("create", "kit_from_class");
        public static readonly string KitFromBackground = K("create", "kit_from_background");
        public static readonly string KitGear = K("create", "kit_gear");
        public static readonly string KitGold = K("create", "kit_gold");
        public static readonly string KitStartsWith = K("create", "kit_starts_with");
        public static readonly string KitShopFirst = K("create", "kit_shop_first");
        public static readonly string KitList = K("create", "kit_list");
        public static readonly string KitNoGear = K("create", "kit_no_gear");

        public static readonly string PauseTitle = K("pause", "title");
        public static readonly string Resume = K("pause", "resume");
        public static readonly string Save = K("pause", "save");
        public static readonly string Saved = K("pause", "saved");
        public static readonly string Load = K("pause", "load");
        public static readonly string Sheet = K("pause", "sheet");
        public static readonly string Pack = K("pause", "pack");
        public static readonly string ToTheBook = K("pause", "to_the_book");

        public static readonly string TheEnd = K("end", "title");
        public static readonly string TheEndBlurb = K("end", "blurb");
        public static readonly string DemoEnd = K("end", "demo");

        public static readonly string ThrowPrompt = K("combat", "throw_prompt");
        public static readonly string VerdictHit = K("combat", "verdict_hit");
        public static readonly string VerdictCritical = K("combat", "verdict_critical");
        public static readonly string VerdictMiss = K("combat", "verdict_miss");
        public static readonly string VerdictSaved = K("combat", "verdict_saved");
        public static readonly string VerdictFailed = K("combat", "verdict_failed");
        public static readonly string TurnHint = K("table", "turn_hint");
        public static readonly string AimHint = K("combat", "aim_hint");
        public static readonly string CameRound = K("table", "came_round");
        public static readonly string YourTurn = K("combat", "your_turn");
        public static readonly string FightWon = K("combat", "won");
        public static readonly string FightFled = K("combat", "fled");
        public static readonly string OnYourself = K("combat", "on_yourself");
        public static readonly string BeforeFightTitle = K("combat", "before_title");
        public static readonly string BeforeFightLine = K("combat", "before_line");
        public static readonly string BeforeFightCast = K("combat", "before_cast");
        public static readonly string ToTheFight = K("combat", "to_the_fight");
        public static readonly string CaptionCast = K("combat", "caption_cast");
        public static readonly string CaptionCastOn = K("combat", "caption_cast_on");
        public static readonly string CaptionChanged = K("combat", "caption_changed");

        // what a spell changed, said to the hero ("your Armor Class is now 14") or about someone else
        public static string YouChanged(Core.Combat.Stat stat) => K("combat", "you_" + stat.Id());

        public static string TheyChanged(Core.Combat.Stat stat) => K("combat", "they_" + stat.Id());

        public static readonly string SheetTitle = K("sheet", "title");
        public static readonly string SheetLevel = K("sheet", "level");

        // "XP 350 of 900" (cc_task_i 3.4), and at level 20 just the total
        public static readonly string SheetXp = K("sheet", "xp");

        public static readonly string SheetXpTop = K("sheet", "xp_top");
        public static readonly string SheetHp = K("sheet", "hit_points");
        public static readonly string SheetSize = K("sheet", "size");
        public static readonly string SheetSpeciesSpells = K("sheet", "species_spells");
        public static readonly string SheetAc = K("sheet", "armor_class");
        public static readonly string SheetSpells = K("sheet", "spells");
        public static readonly string SheetFeatures = K("sheet", "features");

        // the solo deltas on the sheet (cc_task_h 3.1, 3.2)
        public static readonly string SheetSoloSlot = K("sheet", "solo_slot");

        public static readonly string SheetKeepsakeReady = K("sheet", "keepsake_ready");

        public static readonly string SheetKeepsakeSpent = K("sheet", "keepsake_spent");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                GameTitle, PressToBegin, Back, Next, Begin, Saves, NoSaves, Character, Retire,
                CreateTitle, PointsLeft, PicksLeft, NameHint, Suggested, ChosenOf, Figure, FigureKey(0), FigureKey(1),
                LookBodyKey, LookSkinKey, LookHairKey, LookOwnKey, LookKindKey,
                PauseTitle, Resume, Save, Saved, Load, Sheet, Pack, ToTheBook,
                TheEnd, TheEndBlurb, DemoEnd,
                ThrowPrompt, VerdictHit, VerdictCritical, VerdictMiss, VerdictSaved, VerdictFailed, TurnHint, AimHint, CameRound, YourTurn, FightWon, FightFled,
                OnYourself, CaptionCast, CaptionCastOn, CaptionChanged,
                BeforeFightTitle, BeforeFightLine, BeforeFightCast, ToTheFight,
                SheetTitle, SheetLevel, SheetXp, SheetXpTop, SheetHp, SheetAc, SheetSpells, SheetFeatures, SheetSoloSlot, SheetKeepsakeReady, SheetKeepsakeSpent, SheetSize, SheetSpeciesSpells,
                TraitSkill, TraitSpellAbility, TraitSize,
                KitFromClass, KitFromBackground, KitGear, KitGold, KitStartsWith, KitShopFirst, KitList, KitNoGear,
                AskCard.TurnsTheHitKey,
            }
            .Concat(new[]
            {
                Step.Class, Step.Species, Step.Lineage, Step.Background, Step.Traits, Step.Abilities, Step.Improvements,
                Step.Skills, Step.Cantrips, Step.Spells, Step.SpellResource, Step.Equipment, Step.Alignment, Step.Name,
                Step.Look,
            }.Select(StepKey))
            .Concat(System.Enum.GetValues<Core.Combat.Stat>().SelectMany(s => new[] { YouChanged(s), TheyChanged(s) }));
    }
}
