using System;
using System.Collections.Generic;
using System.Linq;
using Content.Screens;
using Core.Tables;
using Core.Words;

namespace Content.Workshop.Tables
{
    // THE TABLE EDITORS' WORDS (cc_task_n): the Fights, Random encounters, Loot, Shops and Lucky rolls tabs and the pickers
    // they share, every one a key (ui.workshop.*; English in game/locale/game.csv). Walked by WorkshopWords.Keys
    public static class TableWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        // --- shared: the list of things on a tab, and the pickers ---------------------------------------------------------

        public static readonly string EditKey = K("edit");
        public static readonly string DoneKey = K("done");
        public static readonly string NameKey = K("thing_name");
        public static readonly string NameHintKey = K("thing_name_hint");
        public static readonly string UsedInKey = K("used_in");
        public static readonly string UsedNowhereKey = K("used_nowhere");
        public static readonly string CardsOneKey = K("used_cards_one");
        public static readonly string CardsManyKey = K("used_cards_many");
        public static readonly string FightsOneKey = K("used_fights_one");
        public static readonly string FightsManyKey = K("used_fights_many");
        public static readonly string TablesOneKey = K("used_tables_one");
        public static readonly string TablesManyKey = K("used_tables_many");
        public static readonly string DeleteUsedKey = K("delete_used_question");
        public static readonly string DeleteThingKey = K("delete_thing_question");
        public static readonly string LikelyKey = K("likely");
        public static readonly string LikelyShareKey = K("likely_share");
        public static readonly string DiceHintKey = K("dice_hint");
        public static readonly string DiceKey = K("dice");
        public static readonly string TimesKey = K("times");
        public static readonly string SearchKey = K("search");
        public static readonly string AnyChallengeKey = K("any_challenge");
        public static readonly string AnyTypeKey = K("any_type");
        public static readonly string MonsterLineKey = K("monster_line");
        public static readonly string ItemLineKey = K("item_line");
        public static readonly string NoMapKey = K("no_map");
        public static readonly string KeptKeysKey = K("kept_keys");
        public static readonly string RollItKey = K("roll_it");
        public static readonly string RolledKey = K("rolled_twenty");
        public static readonly string NothingHappenedKey = K("nothing_happened");
        public static readonly string ResultKey = K("result_line");
        public static readonly string LineKey = K("gm_line");
        public static readonly string LineHintKey = K("gm_line_hint");
        public static readonly string InFileKey = K("in_file");

        // --- fights ---------------------------------------------------------------------------------------------------

        public static readonly string FightsHintKey = K("fights_hint");
        public static readonly string NewFightKey = K("new_fight");
        public static readonly string NoFightsKey = K("no_fights");
        public static readonly string WhereKey = K("where");
        public static readonly string WhereHintKey = K("where_hint");
        public static readonly string MakeMapKey = K("make_new_map");
        public static readonly string WhoKey = K("who");
        public static readonly string WhoHintKey = K("who_hint");
        public static readonly string AddMonsterKey = K("add_monster");
        public static readonly string HowManyKey = K("how_many");
        public static readonly string LeaderKey = K("leader");
        public static readonly string ForLevelKey = K("for_level");
        public static readonly string HowHardKey = K("how_hard");
        public static readonly string MeterRightKey = K("meter_right");
        public static readonly string MeterOverKey = K("meter_over");
        public static readonly string MeterFarOverKey = K("meter_far_over");
        public static readonly string MeterCapKey = K("meter_cap");
        public static readonly string MeterXpKey = K("meter_xp");
        public static readonly string MeterEnemiesKey = K("meter_enemies");
        public static readonly string MoraleKey = K("morale");
        public static readonly string DefeatKey = K("if_hero_loses");
        public static readonly string LeavesKey = K("leaves");
        public static readonly string LeavesNothingKey = K("leaves_nothing");
        public static readonly string MakeLootKey = K("make_loot");
        public static readonly string SpawnsOkKey = K("spawns_ok");
        public static readonly string SpawnsShortKey = K("spawns_short");
        public static readonly string OpenMapKey = K("open_map");
        public static readonly string TryFightKey = K("try_fight");

        public static string Difficulty(EncounterDifficulty difficulty) => K("difficulty_" + difficulty.Id());

        public static string Morale(Core.Combat.Morale morale) => K("morale_" + morale.Id());

        // Unsaid is the campaign's own setting ("Use the campaign's setting"); its word is empty
        public static string Defeat(Core.Combat.Defeat defeat) =>
            K("fight_defeat_" + (defeat == Core.Combat.Defeat.Unsaid ? "campaign" : defeat.Id()));

        // --- random encounters -----------------------------------------------------------------------------------------

        public static readonly string RandomHintKey = K("random_hint");
        public static readonly string NewTableKey = K("new_random_table");
        public static readonly string NoTablesKey = K("no_random_tables");
        public static readonly string HowOftenKey = K("how_often");
        public static readonly string OtherOftenKey = K("often_other_numbers");
        public static readonly string AtLeastKey = K("at_least");
        public static readonly string HeardKey = K("hear_the_roll");
        public static readonly string EntriesKey = K("what_can_happen");
        public static readonly string AddFightEntryKey = K("add_fight_entry");
        public static readonly string AddMomentEntryKey = K("add_moment_entry");
        public static readonly string AddNothingEntryKey = K("add_nothing_entry");

        public static string Often(HowOften how) => K("often_" + how.Id());

        public static string Rolled(Visibility visibility) => K("rolled_" + visibility.Id());

        public static string Entry(EntryKind kind) => K("entry_" + kind.Id());

        // --- loot -------------------------------------------------------------------------------------------------

        public static readonly string LootHintKey = K("loot_hint");
        public static readonly string NewLootKey = K("new_loot");
        public static readonly string NoLootKey = K("no_loot");
        public static readonly string ItemsKey = K("items");
        public static readonly string AddItemKey = K("add_item");
        public static readonly string CoinsKey = K("coins");
        public static readonly string RollsTableKey = K("rolls_table");
        public static readonly string FindLineKey = K("find_line");
        public static readonly string AddFindKey = K("add_find");
        public static readonly string AddEmptyKey = K("add_empty");
        public static readonly string AddRollTableKey = K("add_roll_table");
        public static readonly string LootTotalsKey = K("loot_totals");

        public static string Coin(string coin) => K("coin_" + coin);

        public static string LootEntry(LootKind kind) => K("loot_" + kind.Id());

        // --- shops ------------------------------------------------------------------------------------------------

        public static readonly string ShopsHintKey = K("shops_hint");
        public static readonly string NewShopKey = K("new_shop");
        public static readonly string NoShopsKey = K("no_shops");
        public static readonly string SellsKey = K("sells");
        public static readonly string LendsKey = K("lends");
        public static readonly string PaysKey = K("pays");
        public static readonly string PaysHalfKey = K("pays_half");
        public static readonly string PaysPercentKey = K("pays_percent");
        public static readonly string PaysGameKey = K("pays_game");
        public static readonly string StockKey = K("stock");
        public static readonly string ArmoryRefusesKey = K("armory_refuses");
        public static readonly string OpenShopKey = K("open_shop");

        // --- lucky and unlucky rolls ---------------------------------------------------------------------------------

        public static readonly string LuckHintKey = K("luck_hint");
        public static readonly string LuckAddsKey = K("luck_adds");
        public static readonly string LuckReplacesKey = K("luck_replaces");
        public static readonly string NewLuckyKey = K("new_lucky");
        public static readonly string NewUnluckyKey = K("new_unlucky");
        public static readonly string LuckyKey = K("lucky");
        public static readonly string UnluckyKey = K("unlucky");
        public static readonly string WhatHappensKey = K("what_happens");
        public static readonly string AmountKey = K("amount");
        public static readonly string AbilityKey = K("which_ability");
        public static readonly string ConditionKey = K("which_condition");
        public static readonly string ScalesKey = K("scales_with_level");
        public static readonly string MomentLineKey = K("moment_line");
        public static readonly string NeedsBothKey = K("needs_both");

        public static readonly string[] MomentKinds = { "health", "gold", "ability_shift", "condition", "flavour" };

        public static string MomentKind(string kind) => K("moment_" + kind);

        // a count said one way or many: "1 story card", "3 story cards"
        public static Said Counted(int count, string one, string many) => count == 1 ? new Said(one) : new Said(many, count);

        public static IEnumerable<string> Keys() =>
            new[]
            {
                EditKey, DoneKey, NameKey, NameHintKey, UsedInKey, UsedNowhereKey, CardsOneKey, CardsManyKey, FightsOneKey,
                FightsManyKey, TablesOneKey, TablesManyKey, DeleteUsedKey, DeleteThingKey, LikelyKey, LikelyShareKey,
                DiceHintKey, DiceKey, TimesKey, SearchKey, AnyChallengeKey, AnyTypeKey, MonsterLineKey, ItemLineKey, NoMapKey,
                KeptKeysKey, RollItKey, RolledKey, NothingHappenedKey, ResultKey, LineKey, LineHintKey, InFileKey,
                FightsHintKey, NewFightKey, NoFightsKey, WhereKey, WhereHintKey, MakeMapKey, WhoKey, WhoHintKey, AddMonsterKey,
                HowManyKey, LeaderKey, ForLevelKey, HowHardKey, MeterRightKey, MeterOverKey, MeterFarOverKey, MeterCapKey,
                MeterXpKey, MeterEnemiesKey, MoraleKey, DefeatKey, LeavesKey, LeavesNothingKey, MakeLootKey, SpawnsOkKey,
                SpawnsShortKey, OpenMapKey, TryFightKey,
                RandomHintKey, NewTableKey, NoTablesKey, HowOftenKey, OtherOftenKey, AtLeastKey, HeardKey, EntriesKey,
                AddFightEntryKey, AddMomentEntryKey, AddNothingEntryKey,
                LootHintKey, NewLootKey, NoLootKey, ItemsKey, AddItemKey, CoinsKey, RollsTableKey, FindLineKey, AddFindKey,
                AddEmptyKey, AddRollTableKey, LootTotalsKey,
                ShopsHintKey, NewShopKey, NoShopsKey, SellsKey, LendsKey, PaysKey, PaysHalfKey, PaysPercentKey, PaysGameKey,
                StockKey, ArmoryRefusesKey, OpenShopKey,
                LuckHintKey, LuckAddsKey, LuckReplacesKey, NewLuckyKey, NewUnluckyKey, LuckyKey, UnluckyKey, WhatHappensKey,
                AmountKey, AbilityKey, ConditionKey, ScalesKey, MomentLineKey, NeedsBothKey,
            }
            .Concat(Enum.GetValues<EncounterDifficulty>().Select(Difficulty))
            .Concat(Enum.GetValues<Core.Combat.Morale>().Select(Morale))
            .Concat(Enum.GetValues<Core.Combat.Defeat>().Select(Defeat))
            .Concat(Enum.GetValues<HowOften>().Select(Often))
            .Concat(Enum.GetValues<Visibility>().Select(Rolled))
            .Concat(Enum.GetValues<EntryKind>().Select(Entry))
            .Concat(new[] { "gold", "silver", "copper" }.Select(Coin))
            .Concat(Enum.GetValues<LootKind>().Select(LootEntry))
            .Concat(MomentKinds.Select(MomentKind));
    }
}
