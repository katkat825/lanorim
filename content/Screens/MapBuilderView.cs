using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Core.Space;

namespace Content.Screens
{
    // THE MAP BUILDER'S SCREEN, AS LOGIC (cc_task_f, Part 2; cc_task_l): a view over MapEditor the Godot screen binds to,
    // the way PackView is the pack's. The map, the tools, what they lay, the problems and where the file lives (the
    // "My maps" library or a campaign: MapPlace); every word a key
    public sealed class MapBuilderView
    {
        public MapBuilderView(MapEditor editor, MapPlace place, string id)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Place = place ?? throw new ArgumentNullException(nameof(place));
            Id = id ?? "";
        }

        public MapEditor Editor { get; }

        public MapDraft Draft => Editor.Draft;

        // where the map lives, and its id there (its file name). Save as changes both
        public MapPlace Place { get; private set; }

        public string Id { get; private set; }

        // a new map, sized, from a starting template (MapTemplates): the dungeon's walled room unless another is named,
        // so the first thing an author sees is a place and not a black square
        public static MapEditor Fresh(int columns, int rows, string template = MapTemplates.Standard) =>
            new(MapTemplates.Make(template, columns, rows) ?? MapTemplates.Make(MapTemplates.Standard, columns, rows));

        public const int MinSide = 3;

        // what the paint tool lays: a ground (its look and its rule), or a rule on its own over the look there
        public static readonly IReadOnlyList<Ground> Looks = Enum.GetValues<Ground>();

        public static readonly IReadOnlyList<Tile> Rules = new[] { Tile.Floor, Tile.Rough, Tile.Void, Tile.Chasm };

        // and the wall tool: a wall or a door, in a style
        public static readonly IReadOnlyList<Edge> Lines = new[] { Edge.Wall, Edge.Door };

        public static readonly IReadOnlyList<WallStyle> Styles = Enum.GetValues<WallStyle>();

        public static IEnumerable<int> SpawnSlots => Enumerable.Range(MapDraft.FirstSpawn, MapDraft.LastSpawn);

        public IReadOnlyList<MapProblem> Problems => Draft.Findings();

        // SAVE, problems or not: a map with problems saves (an author mid-way through has a map that doesn't play yet),
        // and says so, since its campaign won't load it until they're fixed. null when it saved, or why not (the
        // system's own reason for a disk that refused, which is not the game's to translate)
        public Said Save() => Write(Place, Id, replace: true, keep: true);

        // a map of that id is in that place already, and it isn't this one: the screen asks before writing over it
        public bool Taken(MapPlace place, string id) =>
            MapFiles.Has(place, id) && !(place.Same(Place) && id == Id);

        // SAVE AS: this map written there, and from now on it is that file. `replace` says the author was asked
        public Said SaveAs(MapPlace place, string id, bool replace = false)
        {
            Said refused = Write(place, id, replace, keep: true);

            if (refused != null) return refused;

            Place = place;
            Id = id;
            return null;
        }

        // COPY TO A CAMPAIGN (or anywhere): this map written there too; the one open stays the one open
        public Said CopyTo(MapPlace place, string id, bool replace = false) => Write(place, id, replace, keep: false);

        Said Write(MapPlace place, string id, bool replace, bool keep)
        {
            if (!ContentId.IsLocal(id)) return new Said(IdRuleKey);

            if (!replace && Taken(place, id)) return new Said(TakenKey, id);

            string problem = MapFiles.Write(place, id, Draft);

            if (problem != null) return new Said(CouldNotSaveKey, problem);

            if (keep) Editor.MarkSaved();
            return null;
        }

        // what the save said, when it saved: plainly saved, or saved with problems
        public string SavedKey => Problems.Count == 0 ? SavedCleanKey : SavedWithProblemsKey;

        public static string RuleKey(Tile rule) => K("rule_" + Core.Words.EnumWords.Name(rule));

        public static string GroundKey(Ground ground) => K("ground_" + Core.Words.EnumWords.Name(ground));

        public static string LineKey(Edge line) => K("line_" + Core.Words.EnumWords.Name(line));

        public static string StyleKey(WallStyle style) => K("style_" + Core.Words.EnumWords.Name(style));

        static readonly Func<string, string> K = ScreenKeys.For("map");

        public static readonly string TitleKey = K("title");
        public static readonly string NewKey = K("new");
        public static readonly string MakeKey = K("make");
        public static readonly string OpenKey = K("open");
        public static readonly string CampaignKey = K("campaign");
        public static readonly string CampaignMapsKey = K("campaign_maps");
        public static readonly string IdKey = K("id");
        public static readonly string IdRuleKey = K("id_rule");
        public static readonly string ColumnsKey = K("columns");
        public static readonly string RowsKey = K("rows");
        public static readonly string TemplateKey = K("template");
        public static readonly string LivesKey = K("lives");
        public static readonly string NoCampaignsKey = K("no_campaigns");
        public static readonly string NoMapsKey = K("no_maps");
        public static readonly string SaveKey = K("save");
        public static readonly string SaveAsKey = K("save_as");
        public static readonly string CopyToKey = K("copy_to");
        public static readonly string TakenKey = K("taken");
        public static readonly string RenameKey = K("rename");
        public static readonly string ReplaceKey = K("replace");
        public static readonly string CancelKey = K("cancel");
        public static readonly string CopiedKey = K("copied");
        public static readonly string SavedCleanKey = K("saved");
        public static readonly string SavedWithProblemsKey = K("saved_with_problems");
        public static readonly string CouldNotSaveKey = K("could_not_save");
        public static readonly string UndoKey = K("undo");
        public static readonly string RedoKey = K("redo");
        public static readonly string RotateKey = K("rotate");
        public static readonly string TopDownKey = K("top_down");
        public static readonly string ProblemsKey = K("problems");
        public static readonly string NoProblemsKey = K("no_problems");
        public static readonly string SpawnKey = K("spawn");
        public static readonly string LeaveKey = K("leave");
        public static readonly string UnsavedKey = K("unsaved");
        public static readonly string LeaveAnywayKey = K("leave_anyway");
        public static readonly string StayKey = K("stay");
        public static readonly string AllPropsKey = K("all_props");
        public static readonly string SearchKey = K("search");
        public static readonly string LooksKey = K("looks");
        public static readonly string RulesKey = K("rules");
        public static readonly string StylesKey = K("styles");
        public static readonly string CopyKey = K("copy");
        public static readonly string PasteKey = K("paste");
        public static readonly string DeleteKey = K("delete");
        public static readonly string ScatterHintKey = K("scatter_hint");
        public static readonly string SelectHintKey = K("select_hint");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, NewKey, MakeKey, OpenKey, CampaignKey, CampaignMapsKey, IdKey, IdRuleKey, ColumnsKey, RowsKey, TemplateKey,
                LivesKey, NoCampaignsKey, NoMapsKey, SaveKey, SaveAsKey, CopyToKey, TakenKey, RenameKey, ReplaceKey,
                CancelKey, CopiedKey, SavedCleanKey, SavedWithProblemsKey, CouldNotSaveKey, UndoKey, RedoKey, RotateKey,
                TopDownKey, ProblemsKey, NoProblemsKey, SpawnKey, LeaveKey, UnsavedKey, LeaveAnywayKey, StayKey,
                AllPropsKey, SearchKey, LooksKey, RulesKey, StylesKey, CopyKey, PasteKey, DeleteKey, ScatterHintKey, SelectHintKey,
                MapPlace.LibraryKey,
            }
            .Concat(Looks.Select(GroundKey))
            .Concat(Rules.Select(RuleKey))
            .Concat(Lines.Select(LineKey))
            .Concat(Styles.Select(StyleKey))
            .Concat(MapTemplates.Keys())
            .Concat(MapProblem.Names.Select(MapProblem.KeyFor));
    }
}
