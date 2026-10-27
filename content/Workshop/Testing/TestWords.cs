using System;
using System.Collections.Generic;
using System.Linq;
using Content.Screens;

namespace Content.Workshop.Testing
{
    // PLAY-TESTING'S WORDS (cc_task_q): where a test starts, the test hero panel, the Workshop bar along the top of the
    // table, the fight-skip choice, the live memory, problems seen, and Run it for me - every one a key (ui.workshop.*;
    // English in game/locale/game.csv). Walked by WorkshopWords.Keys
    public static class TestWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        // --- where a test starts ------------------------------------------------------------------------------------

        public static readonly string PlayCampaignKey = K("play_campaign");
        public static readonly string QuickHeroKey = K("quick_hero");
        public static readonly string PlayFromHereKey = K("play_from_here");

        // --- the test hero ------------------------------------------------------------------------------------------

        public static readonly string HeroTitleKey = K("test_hero_title");
        public static readonly string ClassKey = K("test_class");
        public static readonly string SpeciesKey = K("test_species");
        public static readonly string LevelKey = K("test_level");
        public static readonly string GearKey = K("test_gear");
        public static readonly string EverySpellKey = K("every_spell");
        public static readonly string ExtrasKey = K("test_extras");
        public static readonly string GoldKey = K("test_gold");
        public static readonly string PresetKey = K("test_memory");
        public static readonly string PresetHintKey = K("test_memory_hint");
        public static readonly string RealKey = K("use_real");
        public static readonly string NoRealKey = K("no_real");
        public static readonly string StartKey = K("start_test");

        // --- the Workshop bar ---------------------------------------------------------------------------------------

        public static readonly string BackToEditorKey = K("back_to_editor");
        public static readonly string RestartCardKey = K("restart_card");
        public static readonly string RestartTestKey = K("restart_test");
        public static readonly string RollsKey = K("test_rolls");
        public static readonly string NextPassKey = K("next_pass");
        public static readonly string NextFailKey = K("next_fail");
        public static readonly string WhereKey = K("where_am_i");
        public static readonly string VisitedKey = K("visited");
        public static readonly string MemoryKey = K("memory_live");
        public static readonly string EditKey = K("memory_edit");
        public static readonly string HeroLineKey = K("test_hero_line");
        public static readonly string ProblemsKey = K("problems_seen");
        public static readonly string NoProblemsKey = K("no_problems_seen");
        public static readonly string TestingKey = K("testing");

        public static string Rolls(RollRule rule) => K("rolls_" + Core.Words.EnumWords.Id(rule));

        // --- a fight starts -----------------------------------------------------------------------------------------

        public static readonly string FightStartsKey = K("fight_starts");

        public static string Skip(FightSkip skip) => K("fight_" + Core.Words.EnumWords.Id(skip));

        // --- run it for me ------------------------------------------------------------------------------------------

        public static readonly string RunForMeKey = K("run_for_me");
        public static readonly string TryEveryKey = K("try_every");
        public static readonly string ReportKey = K("run_report");
        public static readonly string ReachedKey = K("cards_reached");
        public static readonly string NeverReachedKey = K("cards_never_reached");
        public static readonly string BehindKey = K("behind");
        public static readonly string RunsKey = K("runs");
        public static readonly string EndedKey = K("run_ended");

        // what went wrong in a test, in a sentence
        public static string Problem(TestProblemKind kind) => K("test_problem_" + Core.Words.EnumWords.Id(kind));

        public static IEnumerable<string> Keys() =>
            new[]
            {
                PlayCampaignKey, QuickHeroKey, PlayFromHereKey, HeroTitleKey, ClassKey, SpeciesKey, LevelKey, GearKey,
                EverySpellKey, ExtrasKey, GoldKey, PresetKey, PresetHintKey, RealKey, NoRealKey, StartKey,
                BackToEditorKey, RestartCardKey, RestartTestKey, RollsKey, NextPassKey, NextFailKey, WhereKey, VisitedKey,
                MemoryKey, EditKey, HeroLineKey, ProblemsKey, NoProblemsKey, TestingKey, FightStartsKey,
                RunForMeKey, TryEveryKey, ReportKey, ReachedKey, NeverReachedKey, BehindKey, RunsKey, EndedKey,
            }
            .Concat(Enum.GetValues<RollRule>().Select(Rolls))
            .Concat(Enum.GetValues<FightSkip>().Select(Skip))
            .Concat(Enum.GetValues<TestProblemKind>().Select(Problem));
    }

    // "Roll for real" (the default), "Always pass", "Always fail": the hero's checks and saves only, never the GM's
    public enum RollRule
    {
        Real,
        Pass,
        Fail,
    }

    // when a fight starts in a test: play it, or skip to how it ended
    public enum FightSkip
    {
        Play,
        Win,
        Lose,
        Flee,
    }

    public enum TestProblemKind
    {
        // a line with no locale row
        NoWords,

        // a fight whose map isn't there
        NoMap,

        // a go-to to a card that isn't there
        NoCard,

        // anything else the run said went wrong
        Other,
    }
}
