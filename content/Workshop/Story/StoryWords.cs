using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Screens;

namespace Content.Workshop.Story
{
    // THE STORY EDITOR'S WORDS (cc_task_o): the board, the cards, the blocks and their palette, every one a key
    // (ui.workshop.*; English in game/locale/game.csv). Walked by WorkshopWords.Keys. And the few Yarn words the editor
    // writes for the author, who never sees them outside Advanced
    public static class StoryWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        // --- Yarn's words the editor writes ------------------------------------------------------------------------

        public const string LineTag = "line";

        public const string SpeakerTag = DialogueBook.SpeakerHeader;

        public const string SpeakerHeader = DialogueBook.SpeakerHeader;

        // the GM, and whichever companion the hero has (DialogueLine's alias)
        public const string Gm = "dm";

        public const string Companion = "companion";

        // the hero's name in a line, shown as a chip in the editor
        public const string HeroName = "{" + HeroVariables.Name + "}";

        // --- the board ----------------------------------------------------------------------------------------------

        public static readonly string StoryHintKey = K("story_hint");
        public static readonly string NewCardKey = K("new_card");
        public static readonly string CardNameKey = K("card_name");
        public static readonly string FitAllKey = K("fit_all");
        public static readonly string ZoomInKey = K("zoom_in");
        public static readonly string ZoomOutKey = K("zoom_out");
        public static readonly string SearchStoryKey = K("search_story");
        public static readonly string GoToCardKey = K("go_to_card");
        public static readonly string ChapterStartKey = K("chapter_start");
        public static readonly string EndsStoryKey = K("ends_story");
        public static readonly string BlocksOneKey = K("blocks_one");
        public static readonly string BlocksManyKey = K("blocks_many");
        public static readonly string TextCardKey = K("text_card");
        public static readonly string NoStartKey = K("no_start_card");
        public static readonly string MakeItKey = K("make_it");
        public static readonly string FoundKey = K("found");
        public static readonly string ColourKey = K("colour");
        public static readonly string BackToBoardKey = K("back_to_board");
        public static readonly string PreviewKey = K("preview");
        public static readonly string DuplicateKey = K("duplicate_card");
        public static readonly string RenameCardKey = K("rename_card");
        public static readonly string DeleteCardKey = K("delete_card_question");

        // --- hints, not walls ---------------------------------------------------------------------------------------

        public static readonly string NoWayOutKey = K("no_way_out");
        public static readonly string NothingLeadsHereKey = K("nothing_leads_here");

        // --- inside a card ------------------------------------------------------------------------------------------

        public static readonly string AddBlockKey = K("add_block");
        public static readonly string MoveUpKey = K("move_up");
        public static readonly string MoveDownKey = K("move_down");
        public static readonly string DuplicateBlockKey = K("duplicate_block");
        public static readonly string DeleteBlockKey = K("delete_block");
        public static readonly string AndThenKey = K("and_then");
        public static readonly string TextCardHintKey = K("text_card_hint");
        public static readonly string TheirYarnKey = K("their_yarn");

        // the palette's groups and blocks (BlockKinds; their keys walked by BlockWords)
        public static string Group(string group) => K("palette_" + group);

        public static string BlockName(string block) => K("block_" + block);

        // the words a new line, a new option and a new remembering start with
        public static readonly string NewLineKey = K("new_line");
        public static readonly string NewOptionKey = K("new_option");
        public static readonly string NewMemoryKey = K("new_memory");
        public static readonly string NewNumberKey = K("new_number");

        // someone says
        public static readonly string WhoKey = K("say_who");
        public static readonly string WhatKey = K("say_what");
        public static readonly string GmKey = K("speaker_gm");
        public static readonly string CompanionKey = K("speaker_companion");
        public static readonly string CompanionHintKey = K("speaker_companion_hint");
        public static readonly string HeroNameKey = K("hero_name");
        public static readonly string LineIdKey = K("line_id");

        // the note
        public static readonly string NoteIntroKey = K("note_intro");
        public static readonly string AddOptionKey = K("add_option");
        public static readonly string OptionKey = K("option");
        public static readonly string ThenGoKey = K("then_go");
        public static readonly string NowhereKey = K("nowhere");

        // go to card
        public static readonly string GoToKey = K("go_to");
        public static readonly string ANewCardKey = K("a_new_card");
        public static readonly string StoryEndsKey = K("story_ends");


        public static IEnumerable<string> Keys() =>
            new[]
            {
                StoryHintKey, NewCardKey, CardNameKey, FitAllKey, ZoomInKey, ZoomOutKey, SearchStoryKey, GoToCardKey,
                ChapterStartKey, EndsStoryKey, BlocksOneKey, BlocksManyKey, TextCardKey, NoStartKey, MakeItKey, FoundKey,
                ColourKey, BackToBoardKey, PreviewKey, DuplicateKey, RenameCardKey, DeleteCardKey, NoWayOutKey,
                NothingLeadsHereKey, AddBlockKey, MoveUpKey, MoveDownKey, DuplicateBlockKey, DeleteBlockKey, AndThenKey,
                TextCardHintKey, TheirYarnKey, WhoKey, WhatKey, GmKey, CompanionKey, CompanionHintKey, HeroNameKey,
                LineIdKey, NoteIntroKey, AddOptionKey, OptionKey, ThenGoKey, NowhereKey, GoToKey, ANewCardKey,
                StoryEndsKey, NewLineKey, NewOptionKey, NewMemoryKey, NewNumberKey,
            };
    }
}
