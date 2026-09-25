using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Core.Localization;

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

        public static string StepKey(Step step) => K("create", "step_" + step.ToString().ToLowerInvariant());

        public static readonly string CreateTitle = K("create", "title");
        public static readonly string PointsLeft = K("create", "points_left");
        public static readonly string PicksLeft = K("create", "picks_left");
        public static readonly string NameHint = K("create", "name_hint");
        public static readonly string Suggested = K("create", "take_suggested");

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
        public static readonly string YourTurn = K("combat", "your_turn");
        public static readonly string FightWon = K("combat", "won");
        public static readonly string FightFled = K("combat", "fled");

        public static readonly string SheetTitle = K("sheet", "title");
        public static readonly string SheetLevel = K("sheet", "level");
        public static readonly string SheetHp = K("sheet", "hit_points");
        public static readonly string SheetAc = K("sheet", "armor_class");
        public static readonly string SheetSpells = K("sheet", "spells");
        public static readonly string SheetFeatures = K("sheet", "features");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                GameTitle, PressToBegin, Back, Next, Begin, Saves, NoSaves, Character, Retire,
                CreateTitle, PointsLeft, PicksLeft, NameHint, Suggested,
                PauseTitle, Resume, Save, Saved, Load, Sheet, Pack, ToTheBook,
                TheEnd, TheEndBlurb, DemoEnd,
                ThrowPrompt, YourTurn, FightWon, FightFled,
                SheetTitle, SheetLevel, SheetHp, SheetAc, SheetSpells, SheetFeatures,
                AskCard.TurnsTheHitKey,
            }
            .Concat(new[]
            {
                Step.Class, Step.Species, Step.Lineage, Step.Background, Step.Abilities, Step.Improvements,
                Step.Skills, Step.Spells, Step.SpellResource, Step.Alignment, Step.Name,
            }.Select(StepKey));
    }
}
