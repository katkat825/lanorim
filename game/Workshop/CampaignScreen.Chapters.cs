using System.Linq;
using System.Text.Json;
using Content.Maps;
using Content.Screens;
using Content.Workshop;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE CHAPTERS AND MAPS TAB (cc_task_m 4.2): the chapters in order - add, rename, drag (or Up and Down), delete with a
    // question, and a warning when the story uses it - each one's maps under it (add from My maps, make a new one, open
    // in the builder, reorder, remove), and which chapter starts the campaign. A chapter's title is the locale's
    // (Manifest.ChapterTitle); its id is made from its first title and never changes
    public partial class CampaignScreen
    {
        void Chapters(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(WorkshopWords.ChaptersHintKey));

            for (int i = 0; i < Draft.Chapters.Count; i++) page.AddChild(Chapter(i, Draft.Chapters[i]));

            LineEdit title = Forms.Line("NewChapter", "", _ => { }, WorkshopWords.ChapterPlaceholderKey);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            void Add()
            {
                if (string.IsNullOrWhiteSpace(title.Text)) return;
                string made = Editor.AddChapter(title.Text.Trim());
                GD.Print($"workshop chapter {made} added to {Editor.Id}");
                Redraw();
            }

            title.TextSubmitted += _ => Add();
            Button add = Ui.Button(WorkshopWords.AddChapterKey, Add);
            add.Name = "AddChapter";
            page.AddChild(Ui.Row(8, title, add));

            DeletedMaps(page);

            Json(page, JsonSerializer.Serialize(new
            {
                chapters = Draft.Chapters.Select(c => new { id = c.Id, maps = c.Maps }),
                start = Draft.StartsAt,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        // THE CAMPAIGN'S DELETED MAPS (Kathleen, 2026-10-27): thirty days in its maps/.deleted, Restore puts one back among
        // its maps (a chapter takes it again by Add), Delete forever asks first. Past their days, they go as the tab opens
        void DeletedMaps(VBoxContainer page)
        {
            Content.Maps.MapFiles.Expire(Editor.Maps, System.DateTime.Today);
            var items = Editor.DeletedMaps;

            if (items.Count == 0) return;

            page.AddChild(Ui.Label(WorkshopWords.RecentlyDeletedKey));

            foreach (Content.Workshop.RecentlyDeleted.Item item in items)
            {
                Content.Workshop.RecentlyDeleted.Item one = item;

                page.AddChild(Forms.Deleted("DeletedMap_" + one.Id, Content.Maps.MapName.Of(one), one.DaysLeft(System.DateTime.Today),
                    () =>
                    {
                        if (Editor.RestoreMap(one, out string problem) == null) GD.PushWarning($"workshop: {one.Id} could not be restored - {problem}");
                        Redraw();
                    },
                    () =>
                    {
                        string problem = Content.Workshop.RecentlyDeleted.Forget(one);
                        if (problem != null) GD.PushWarning($"workshop: {one.Id} could not be deleted - {problem}");
                        Redraw();
                    }));
            }
        }

        Control Chapter(int at, ChapterDraft chapter)
        {
            string id = chapter.Id;

            LineEdit title = Forms.Line("ChapterTitle_" + id, chapter.Title,
                                        t => Edit($"chapter.{id}.title", d => { if (d.Chapter(id) is { } c) c.Title = t; }, true));
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var top = Ui.Row(8, title);
            top.Name = "Chapter_" + id;

            // a label added to a row after it's made keeps its wrap, and wraps a letter to a line (Ui.Row)
            Label starts = Ui.Label(WorkshopWords.StartsHereKey);
            starts.AutowrapMode = TextServer.AutowrapMode.Off;

            top.AddChild(Draft.StartsAt == id
                ? starts
                : Ui.Button(WorkshopWords.StartHereKey, () => Change(() => Editor.StartAt(id))));

            top.AddChild(Ui.Button(WorkshopWords.UpKey, () => Change(() => Editor.MoveChapter(at, at - 1))).Greyed(at == 0, null));
            top.AddChild(Ui.Button(WorkshopWords.DownKey, () => Change(() => Editor.MoveChapter(at, at + 1))).Greyed(at == Draft.Chapters.Count - 1, null));
            top.AddChild(Ui.Button(WorkshopWords.DeleteKey, () => AskToDelete(top, id)));

            var column = Ui.Column(6, top, Ui.Label(WorkshopWords.MapsKey));

            if (chapter.Maps.Count == 0) column.AddChild(Forms.Hint(WorkshopWords.NoMapsInChapterKey));

            for (int j = 0; j < chapter.Maps.Count; j++) column.AddChild(Map(id, j, chapter.Maps[j], chapter.Maps.Count));

            column.AddChild(FromMyMaps(id));
            column.AddChild(NewMap(id));

            var margin = new MarginContainer();
            foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 8);
            margin.AddChild(column);

            var row = new ChapterRow { Index = at, Title = chapter.Title, Name = "ChapterRow_" + id };
            row.Moved += (from, to) => Change(() => Editor.MoveChapter(from, to));
            row.AddChild(margin);
            return row;
        }

        // one of a chapter's maps: its name, open it, move it, take it off the chapter (its file stays)
        HBoxContainer Map(string chapter, int at, string map, int count)
        {
            Label name = Ui.Plain(MapName.Of(Editor.Maps, map));
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var row = Ui.Row(8, name,
                Ui.Button(WorkshopWords.OpenInBuilderKey, () => OpenMap(map)).Greyed(!MapFiles.Has(Editor.Maps, map), null),
                Ui.Button(WorkshopWords.UpKey, () => Change(() => Editor.MoveMap(chapter, at, at - 1))).Greyed(at == 0, null),
                Ui.Button(WorkshopWords.DownKey, () => Change(() => Editor.MoveMap(chapter, at, at + 1))).Greyed(at == count - 1, null),
                Ui.Button(WorkshopWords.RemoveKey, () => Change(() => Editor.RemoveMap(chapter, at))),
                Forms.Removal($"DeleteMap_{chapter}_{at}", () =>
                {
                    string problem = Editor.DeleteMap(map);
                    if (problem != null) GD.PushWarning($"workshop: {map} could not be deleted - {problem}");
                    Redraw();
                }, WorkshopWords.DeleteMapQuestionKey, WorkshopWords.DeleteMapKey, MapName.Of(Editor.Maps, map)));
            row.Name = $"Map_{chapter}_{at}";
            return row;
        }

        // ONE OF MY MAPS, COPIED IN and put on the chapter
        HBoxContainer FromMyMaps(string chapter)
        {
            MapPlace library = MapBuilderPage.Library;
            var mine = MapFiles.Ids(library);

            var list = new OptionButton { Name = "MyMaps_" + chapter, ClipText = true, FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (string id in mine) list.AddItem(MapName.Of(library, id));
            if (mine.Count == 0) list.AddItem(Ui.Say(WorkshopWords.NoMyMapsKey));
            list.Disabled = mine.Count == 0;

            Button add = Ui.Button(WorkshopWords.AddFromMyMapsKey, () =>
            {
                if (mine.Count == 0 || list.Selected < 0) return;

                string copied = Editor.CopyIn(library, mine[list.Selected], out string problem);

                if (copied == null)
                {
                    GD.PushWarning($"workshop: {mine[list.Selected]} could not be copied in - {problem}");
                    return;
                }

                GD.Print($"workshop map {copied} copied into {Editor.Id} for {chapter}");
                Change(() => Editor.AddMap(chapter, copied));
            }).Greyed(mine.Count == 0, null);
            add.Name = "AddFromMyMaps_" + chapter;

            return Ui.Row(8, list, add);
        }

        // A NEW MAP: named, from a template, written into maps/, put on the chapter, and opened in the builder
        VBoxContainer NewMap(string chapter)
        {
            LineEdit name = Forms.Line("NewMapName_" + chapter, "", _ => { }, WorkshopWords.MapNameKey);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Label rule = Forms.NameRule(name);

            string template = MapTemplates.Standard;
            OptionButton templates = Forms.Choice("NewMapTemplate_" + chapter, MapTemplates.Ids, template, MapTemplates.NameKey, v => template = v);
            templates.CustomMinimumSize = new Vector2(Ui.Px(150), 0);

            int columns = MapBuilderView.StartColumns, rows = MapBuilderView.StartRows;
            HBoxContainer size = Forms.MapSize("NewMapSize_" + chapter, columns, rows, (c, r) => { columns = c; rows = r; });

            Button make = Ui.Button(WorkshopWords.MakeMapKey, () =>
            {
                if (!MapName.Allowed(name.Text))
                {
                    rule.Visible = name.Text.Length > 0;
                    return;
                }

                string made = Editor.MakeMap(name.Text, template, out string problem, columns, rows);

                if (made == null)
                {
                    GD.PushWarning($"workshop: the map could not be made - {problem}");
                    return;
                }

                Editor.AddMap(chapter, made);
                OpenMap(made);
            });
            make.Name = "MakeMap_" + chapter;

            return Ui.Column(2, Ui.Row(8, name, templates, make), size, rule);
        }

        // the builder, on one of the campaign's maps; leaving it comes back to this page as it was (GameState.Editing)
        void OpenMap(string map)
        {
            KeepDraft();
            GameState.Editing = Editor;
            MapBuilderPage.OpenMap(_launch, Editor.Maps, map);
        }

        // DELETE A CHAPTER, asked first, and said more strongly when the story uses it
        void AskToDelete(HBoxContainer top, string chapter)
        {
            Ui.Clear(top);

            Label question = Ui.Label(Editor.StoryUses(chapter) ? WorkshopWords.DeleteUsedChapterKey : WorkshopWords.DeleteChapterKey);
            question.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Button keep = Ui.Button(WorkshopWords.KeepKey, Redraw);

            top.AddChild(question);
            top.AddChild(Ui.Button(WorkshopWords.DeleteKey, () => Change(() => Editor.DeleteChapter(chapter))));
            top.AddChild(keep);
            Ui.FocusLater(keep);
        }
    }
}
