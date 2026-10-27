using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Screens;
using Core.Words;

namespace Content.Workshop.Story
{
    // THE BLOCKS' WORDS (cc_task_p): every block's name in the palette, its form's labels, the outcomes a command answers
    // with, and an "Only if"'s sentences - every one a key (ui.workshop.*; English in game/locale/game.csv). And what a
    // command does in a sentence: a block's summary, and Preview's "(the GM would ...)" (BlockWords.Said)
    public static class BlockWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        // --- rolls ----------------------------------------------------------------------------------------------------

        public static readonly string SkillKey = K("which_skill");
        public static readonly string MoreKey = K("ability_alone");
        public static readonly string HowHardKey = K("how_hard_dc");
        public static readonly string ANumberKey = K("a_number");
        public static readonly string IfPassKey = K("if_pass");
        public static readonly string IfFailKey = K("if_fail");
        public static readonly string NaturalKey = K("natural_line");
        public static readonly string TwoKeysKey = K("two_keys_band");
        public static readonly string AnotherWayKey = K("another_way");
        public static readonly string WithoutRollKey = K("without_roll");
        public static readonly string TryItKey = K("option_try_it");
        public static readonly string FindAnotherKey = K("option_another_way");
        public static readonly string GoRoundKey = K("option_go_round");
        public static readonly string FailEmptyKey = K("fail_empty");
        public static readonly string SaveAbilityKey = K("save_ability");
        public static readonly string DiceKey = K("the_dice");
        public static readonly string HowManyDiceKey = K("how_many_dice");
        public static readonly string HearKey = K("hear_it");
        public static readonly string AtLeastThenKey = K("roll_at_least");
        public static readonly string OtherwiseKey = K("otherwise");
        public static readonly string AddAtLeastKey = K("add_roll_branch");

        public static string Starter(string starter) => K("starter_" + starter);

        public static string StarterLine(string starter) => K("starter_" + starter + "_line");

        // --- fights ---------------------------------------------------------------------------------------------------

        public static readonly string PickFightKey = K("pick_fight");
        public static readonly string PickTableKey = K("pick_table");
        public static readonly string PickLootKey = K("pick_loot");
        public static readonly string AfterwardsKey = K("afterwards");
        public static readonly string AddOutcomeKey = K("add_outcome");
        public static readonly string DiesKey = K("hero_dies_line");
        public static readonly string BrokeKey = K("only_if_broke");
        public static readonly string FightCameKey = K("fight_came_up");
        public static readonly string MomentCameKey = K("moment_came_up");
        public static readonly string NothingCameKey = K("nothing_came_up");
        public static readonly string FoundCoinsKey = K("found_coins");
        public static readonly string FoundNothingKey = K("found_nothing");

        public static readonly string[] Outcomes = { "won", "fled", "lost", "yielded" };

        public static string Outcome(string outcome) => K("outcome_" + outcome);

        // --- rewards and places ------------------------------------------------------------------------------------

        public static readonly string CoinKey = K("which_coin");
        public static readonly string HowMuchKey = K("how_much");
        public static readonly string GetsKey = K("hero_gets");
        public static readonly string PaysKey = K("hero_pays");
        public static readonly string ItemKey = K("which_item");
        public static readonly string LevelToKey = K("level_to");
        public static readonly string XpAmountKey = K("xp_amount");
        public static readonly string OtherLevelingKey = K("other_leveling");
        public static readonly string RestKey = K("rest_kind");
        public static readonly string RestSavesKey = K("rest_saves");
        public static readonly string PickShopKey = K("pick_shop");
        public static readonly string PickArmoryKey = K("pick_armory");
        public static readonly string PoolKey = K("bark_pool");

        public static string Rest(string kind) => K("rest_" + kind);

        public static string Pool(Bark pool) => K("bark_pool_" + pool.Id());

        // --- remember and only if --------------------------------------------------------------------------------------

        public static readonly string PhraseKey = K("phrase");
        public static readonly string PhraseHintKey = K("phrase_hint");
        public static readonly string AddOneKey = K("add_one");
        public static readonly string TakeOneKey = K("take_one");
        public static readonly string SetToKey = K("set_to");
        public static readonly string MemoryKey = K("memory_title");
        public static readonly string NoMemoryKey = K("no_memory");
        public static readonly string SetInKey = K("set_in");
        public static readonly string CheckedInKey = K("checked_in");
        public static readonly string NeverCheckedKey = K("never_checked");
        public static readonly string NeverSetKey = K("never_set");
        public static readonly string RenameKey = K("rename_memory");
        public static readonly string PublishedKey = K("rename_published");
        public static readonly string ThenKey = K("then");
        public static readonly string OrElseIfKey = K("or_else_if");
        public static readonly string AddElseIfKey = K("add_else_if");
        public static readonly string AddOtherwiseKey = K("add_otherwise");
        public static readonly string AllOfKey = K("all_of");
        public static readonly string AnyOfKey = K("any_of");
        public static readonly string AddClauseKey = K("add_clause");
        public static readonly string OnlyShownIfKey = K("only_shown_if");
        public static readonly string AlwaysShownKey = K("always_shown");
        public static readonly string MakeConditionalKey = K("make_conditional");
        public static readonly string OnlyIfSaidKey = K("only_if_said");
        public static readonly string AndKey = K("word_and");
        public static readonly string OrKey = K("word_or");

        public static string Clause(Term term) => K("clause_" + term.Id());

        // --- what a command does, in a sentence ---------------------------------------------------------------------

        public static readonly string WouldKey = K("gm_would");

        public static string Does(string verb) => K("does_" + verb);

        static readonly string[] Verbs = Request.Verbs.Keys.ToArray();

        // "Check Athletics, Medium" - the key and its words for one command (names are keys the page says first)
        public static Said Said(Do act) => new(Does(act.Verb), act.Args.Cast<object>().ToArray());

        public static IEnumerable<string> Keys() =>
            new[]
            {
                SkillKey, MoreKey, HowHardKey, ANumberKey, IfPassKey, IfFailKey, NaturalKey, TwoKeysKey, AnotherWayKey,
                WithoutRollKey, TryItKey, FindAnotherKey, GoRoundKey, FailEmptyKey, SaveAbilityKey, DiceKey, HowManyDiceKey,
                HearKey, AtLeastThenKey, OtherwiseKey, AddAtLeastKey,
                PickFightKey, PickTableKey, PickLootKey, AfterwardsKey, AddOutcomeKey, DiesKey, BrokeKey, FightCameKey,
                MomentCameKey, NothingCameKey, FoundCoinsKey, FoundNothingKey,
                CoinKey, HowMuchKey, GetsKey, PaysKey, ItemKey, LevelToKey, XpAmountKey, OtherLevelingKey, RestKey,
                RestSavesKey, PickShopKey, PickArmoryKey, PoolKey,
                PhraseKey, PhraseHintKey, AddOneKey, TakeOneKey, SetToKey, MemoryKey, NoMemoryKey, SetInKey, CheckedInKey,
                NeverCheckedKey, NeverSetKey, RenameKey, PublishedKey, ThenKey, OrElseIfKey, AddElseIfKey, AddOtherwiseKey,
                AllOfKey, AnyOfKey, AddClauseKey, OnlyShownIfKey, AlwaysShownKey, MakeConditionalKey, OnlyIfSaidKey, AndKey, OrKey,
                WouldKey,
            }
            .Concat(Rolls.Starters.SelectMany(s => new[] { Starter(s), StarterLine(s) }))
            .Concat(Outcomes.Select(Outcome))
            .Concat(new[] { "short", "long" }.Select(Rest))
            .Concat(BarkPools.Spoken.Select(Pool))
            .Concat(Enum.GetValues<Term>().Select(Clause))
            .Concat(Verbs.Select(Does))
            .Concat(BlockKinds.Palette.Select(p => StoryWords.Group(p.Group)))
            .Concat(BlockKinds.All.Select(StoryWords.BlockName));
    }
}
