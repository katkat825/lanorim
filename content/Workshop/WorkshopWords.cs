using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Screens;
using Core.Combat;
using Core.Words;

namespace Content.Workshop
{
    // THE WORKSHOP'S WORDS (cc_task_m): the hub, the new-campaign wizard, the campaign page and its problems panel, every
    // one a key (ui.workshop.*; English in game/locale/game.csv). The locale audit walks Keys
    public static class WorkshopWords
    {
        static readonly Func<string, string> K = ScreenKeys.For("workshop");

        // the book's line for it, where "Map builder" was (BookMenu)
        public static readonly string BookKey = ScreenKeys.Key("book", "workshop");

        // --- the hub ------------------------------------------------------------------------------------------------

        public static readonly string TitleKey = K("title");
        public static readonly string NewCampaignKey = K("new_campaign");
        public static readonly string MyCampaignsKey = K("my_campaigns");
        public static readonly string NoCampaignsKey = K("no_campaigns");
        public static readonly string ChaptersOneKey = K("chapters_one");
        public static readonly string ChaptersManyKey = K("chapters_many");
        public static readonly string EditedKey = K("edited");
        public static readonly string OpenKey = K("open");
        public static readonly string DuplicateKey = K("duplicate");
        public static readonly string DeleteKey = K("delete");
        public static readonly string KeepKey = K("keep");
        public static readonly string RenameKey = K("rename");
        public static readonly string CopyOfKey = K("copy_of");
        public static readonly string UnnamedKey = K("unnamed");
        public static readonly string SameIdKey = K("same_id");
        public static readonly string WontLoadOneKey = K("wont_load_one");
        public static readonly string WontLoadManyKey = K("wont_load_many");
        public static readonly string DeleteQuestionKey = K("delete_question");
        public static readonly string RecentlyDeletedKey = K("recently_deleted");
        public static readonly string DaysLeftOneKey = K("days_left_one");
        public static readonly string DaysLeftManyKey = K("days_left_many");
        public static readonly string RestoreKey = K("restore");
        public static readonly string DeleteForeverKey = K("delete_forever");
        public static readonly string ForeverQuestionKey = K("forever_question");
        public static readonly string NewMapKey = K("new_map");
        public static readonly string NoMyMapsKey = K("no_my_maps");
        public static readonly string SubscribedKey = K("subscribed");
        public static readonly string SubscribedEmptyKey = K("subscribed_empty");
        public static readonly string GameCampaignsKey = K("game_campaigns");
        public static readonly string GameCampaignsHintKey = K("game_campaigns_hint");
        public static readonly string CouldNotKey = K("could_not");

        // the game's own campaigns, in every build (cc_task_m2 Part 6): copied to edit, and opened as they are only in the
        // editor build; a copy says what it's based on
        public static readonly string MakeCopyKey = K("make_copy");
        public static readonly string OpenOriginalKey = K("open_original");
        public static readonly string BasedOnKey = K("based_on");

        // --- the wizard -----------------------------------------------------------------------------------------------

        public static readonly string WizardTitleKey = K("wizard_title");
        public static readonly string StepKey = K("wizard_step");
        public static readonly string NextKey = K("next");
        public static readonly string BackKey = K("back");
        public static readonly string FinishKey = K("finish");
        public static readonly string CancelKey = K("cancel");
        public static readonly string AdvancedKey = K("advanced");
        public static readonly string NameKey = K("name");
        public static readonly string NameHintKey = K("name_hint");
        public static readonly string DescriptionKey = K("description");
        public static readonly string DescriptionHintKey = K("description_hint");
        public static readonly string AuthorKey = K("author");
        public static readonly string AuthorHintKey = K("author_hint");
        public static readonly string TagsKey = K("tags");
        public static readonly string TagsHintKey = K("tags_hint");
        public static readonly string OwnTagKey = K("own_tag");
        public static readonly string AddTagKey = K("add_tag");
        public static readonly string MatureKey = K("mature");
        public static readonly string MatureHintKey = K("mature_hint");
        public static readonly string IdKey = K("id");
        public static readonly string IdHintKey = K("id_hint");
        public static readonly string DefeatKey = K("defeat");
        public static readonly string DefeatHintKey = K("defeat_hint");
        public static readonly string LevelingKey = K("leveling");
        public static readonly string LevelingHintKey = K("leveling_hint");
        public static readonly string CharactersKey = K("characters");
        public static readonly string CharactersHintKey = K("characters_hint");
        public static readonly string StartingShopKey = K("starting_shop");
        public static readonly string StartingShopHintKey = K("starting_shop_hint");
        public static readonly string ConsequencesKey = K("consequences");
        public static readonly string ConsequencesHintKey = K("consequences_hint");
        public static readonly string GmScreenKey = K("gm_screen");
        public static readonly string GmScreenHintKey = K("gm_screen_hint");
        public static readonly string ChapterTitleKey = K("chapter_title");
        public static readonly string ChapterTitleHintKey = K("chapter_title_hint");
        public static readonly string FirstMapKey = K("first_map");
        public static readonly string FirstMapHintKey = K("first_map_hint");
        public static readonly string TemplateHintKey = K("template_hint");
        public static readonly string StoryStartsKey = K("story_starts");

        public static string PageKey(WizardPage page) => K("page_" + page.Id());

        public static string DefeatChoiceKey(Defeat defeat) => K("defeat_" + defeat.Id());

        public static string LevelingChoiceKey(Leveling leveling) => K("leveling_" + leveling.Id());

        public static string ConsequencesChoiceKey(Layering layering) => K("consequences_" + layering.Id());

        public static string FirstMapChoiceKey(FirstMap first) => K("first_map_" + first.Id());

        // a GM screen's name: its skin, with the hyphen a key can't hold
        public static string ScreenNameKey(string skin) => K("gm_" + skin.Replace('-', '_'));

        public static readonly IReadOnlyList<Defeat> Defeats = new[] { Defeat.Death, Defeat.Continues };

        // --- the campaign page --------------------------------------------------------------------------------------

        public static string TabKey(WorkshopTab tab) => K("tab_" + tab.Id());

        public static readonly string SavedKey = K("saved");
        public static readonly string SavedWithProblemsKey = K("saved_with_problems");
        public static readonly string UnsavedKey = K("unsaved");
        public static readonly string DraftQuestionKey = K("draft_question");
        public static readonly string KeepThemKey = K("keep_them");
        public static readonly string ThrowAwayKey = K("throw_away");
        public static readonly string CoverKey = K("cover");
        public static readonly string CoverHintKey = K("cover_hint");
        public static readonly string PickPictureKey = K("pick_picture");
        public static readonly string CoverDefaultKey = K("cover_default");
        public static readonly string JsonKey = K("json");
        public static readonly string StartsHereKey = K("starts_here");
        public static readonly string StartHereKey = K("start_here");
        public static readonly string UpKey = K("up");
        public static readonly string DownKey = K("down");
        public static readonly string DeleteChapterKey = K("delete_chapter_question");
        public static readonly string DeleteUsedChapterKey = K("delete_used_chapter_question");
        public static readonly string MapsKey = K("maps");
        public static readonly string NoMapsInChapterKey = K("no_maps_in_chapter");
        public static readonly string AddFromMyMapsKey = K("add_from_my_maps");
        public static readonly string AddKey = K("add");
        public static readonly string MakeMapKey = K("make_map");
        public static readonly string MapNameKey = K("map_name");
        public static readonly string OpenInBuilderKey = K("open_in_builder");
        public static readonly string RemoveKey = K("remove");
        public static readonly string AddChapterKey = K("add_chapter");
        public static readonly string ChapterPlaceholderKey = K("chapter_placeholder");
        public static readonly string ChaptersHintKey = K("chapters_hint");
        public static readonly string PeopleHintKey = K("people_hint");
        public static readonly string PersonNameKey = K("person_name");
        public static readonly string PersonNoteKey = K("person_note");
        public static readonly string PersonNoteHintKey = K("person_note_hint");
        public static readonly string PortraitKey = K("portrait");
        public static readonly string NoPortraitKey = K("no_portrait");
        public static readonly string AddPersonKey = K("add_person");
        public static readonly string PersonPlaceholderKey = K("person_placeholder");
        public static readonly string DeletePersonKey = K("delete_person_question");
        public static readonly string AlwaysSpeakKey = K("always_speak");

        // --- the problems panel -----------------------------------------------------------------------------------

        public static readonly string ProblemsKey = K("problems");
        public static readonly string NoProblemsKey = K("no_problems");
        public static readonly string GoThereKey = K("go_there");
        public static readonly string ErrorKey = K("error");
        public static readonly string WarningKey = K("warning");

        // the chapters on a card, and a card's problems, one or many
        public static Said Chapters(int count) => count == 1 ? new Said(ChaptersOneKey) : new Said(ChaptersManyKey, count);

        public static Said WontLoad(int errors) => errors == 1 ? new Said(WontLoadOneKey) : new Said(WontLoadManyKey, errors);

        public static Said DaysLeft(int days) => days == 1 ? new Said(DaysLeftOneKey) : new Said(DaysLeftManyKey, days);

        public static IEnumerable<string> Keys() =>
            new[]
            {
                BookKey, TitleKey, NewCampaignKey, MyCampaignsKey, NoCampaignsKey, ChaptersOneKey, ChaptersManyKey, EditedKey,
                OpenKey, DuplicateKey, DeleteKey, KeepKey, RenameKey, CopyOfKey, UnnamedKey, SameIdKey, WontLoadOneKey,
                WontLoadManyKey, DeleteQuestionKey, RecentlyDeletedKey, DaysLeftOneKey, DaysLeftManyKey, RestoreKey,
                DeleteForeverKey, ForeverQuestionKey, NewMapKey, NoMyMapsKey, SubscribedKey, SubscribedEmptyKey,
                GameCampaignsKey, GameCampaignsHintKey, CouldNotKey, MakeCopyKey, OpenOriginalKey, BasedOnKey,
                WizardTitleKey, StepKey, NextKey, BackKey, FinishKey, CancelKey, AdvancedKey, NameKey, NameHintKey,
                DescriptionKey, DescriptionHintKey, AuthorKey, AuthorHintKey, TagsKey, TagsHintKey, OwnTagKey, AddTagKey,
                MatureKey, MatureHintKey, IdKey, IdHintKey, DefeatKey, DefeatHintKey, LevelingKey, LevelingHintKey,
                CharactersKey, CharactersHintKey, StartingShopKey, StartingShopHintKey, ConsequencesKey, ConsequencesHintKey,
                GmScreenKey, GmScreenHintKey, ChapterTitleKey, ChapterTitleHintKey, FirstMapKey, FirstMapHintKey,
                TemplateHintKey, StoryStartsKey,
                SavedKey, SavedWithProblemsKey, UnsavedKey, DraftQuestionKey, KeepThemKey, ThrowAwayKey, CoverKey,
                CoverHintKey, PickPictureKey, CoverDefaultKey, JsonKey, StartsHereKey, StartHereKey, UpKey, DownKey,
                DeleteChapterKey, DeleteUsedChapterKey, MapsKey, NoMapsInChapterKey, AddFromMyMapsKey, AddKey, MakeMapKey,
                MapNameKey, OpenInBuilderKey, RemoveKey, AddChapterKey, ChapterPlaceholderKey, ChaptersHintKey,
                PeopleHintKey, PersonNameKey, PersonNoteKey, PersonNoteHintKey, PortraitKey, NoPortraitKey, AddPersonKey,
                PersonPlaceholderKey, DeletePersonKey, AlwaysSpeakKey,
                ProblemsKey, NoProblemsKey, GoThereKey, ErrorKey, WarningKey,
            }
            .Concat(Enum.GetValues<WizardPage>().Select(PageKey))
            .Concat(Defeats.Select(DefeatChoiceKey))
            .Concat(Enum.GetValues<Leveling>().Select(LevelingChoiceKey))
            .Concat(Enum.GetValues<Layering>().Select(ConsequencesChoiceKey))
            .Concat(Enum.GetValues<FirstMap>().Select(FirstMapChoiceKey))
            .Concat(Manifest.GmScreens.Select(ScreenNameKey))
            .Concat(Enum.GetValues<WorkshopTab>().Select(TabKey))
            .Concat(CampaignWizard.Keys())
            .Concat(ProblemWords.Keys());
    }
}
