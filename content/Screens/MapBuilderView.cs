using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Core.Space;

namespace Content.Screens
{
    // THE MAP BUILDER'S SCREEN, AS LOGIC (cc_task_f, Part 2): a view over MapEditor the Godot screen binds to, the way
    // PackView is the pack's. The map, the tools, what they lay, the problems and the file; every word a key
    public sealed class MapBuilderView
    {
        public MapBuilderView(MapEditor editor, string campaign, string id)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Campaign = campaign ?? "";
            Id = id ?? "";
        }

        public MapEditor Editor { get; }

        public MapDraft Draft => Editor.Draft;

        // the campaign's folder on disk, and the map's id there (its file name)
        public string Campaign { get; }

        public string Id { get; }

        // a new map, sized: solid rock, walled round, a floor to stand on and the start on it, so the first thing an
        // author sees is a room and not a black square
        public static MapEditor Fresh(int columns, int rows)
        {
            var draft = new MapDraft(Math.Clamp(columns, MinSide, MapDraft.MaxSide), Math.Clamp(rows, MinSide, MapDraft.MaxSide));

            draft.Paint(new Cell(0, 0), new Cell(draft.Columns - 1, draft.Rows - 1), Tile.Floor);
            draft.Enclose();

            return new MapEditor(draft);
        }

        public const int MinSide = 3;

        // the ground the paint tool lays, and the line the wall tool does
        public static readonly IReadOnlyList<Tile> Grounds = new[] { Tile.Floor, Tile.Rough, Tile.Void };

        public static readonly IReadOnlyList<Edge> Lines = new[] { Edge.Wall, Edge.Door };

        public static IEnumerable<int> SpawnSlots => Enumerable.Range(MapDraft.FirstSpawn, MapDraft.LastSpawn);

        public IReadOnlyList<MapProblem> Problems => Draft.Findings();

        // SAVE, problems or not: a map with problems saves (an author mid-way through has a map that doesn't play yet),
        // and says so, since its campaign won't load it until they're fixed. null when it saved, or why not (the
        // system's own reason for a disk that refused, which is not the game's to translate)
        public Said Save()
        {
            if (!ContentId.IsLocal(Id)) return new Said(IdRuleKey);

            string problem = MapFiles.Write(Campaign, Id, Draft);

            if (problem != null) return new Said(CouldNotSaveKey, problem);

            Editor.MarkSaved();
            return null;
        }

        // what the save said, when it saved: plainly saved, or saved with problems
        public string SavedKey => Problems.Count == 0 ? SavedCleanKey : SavedWithProblemsKey;

        public static string GroundKey(Tile ground) => K("ground_" + Core.Words.EnumWords.Name(ground));

        public static string LineKey(Edge line) => K("line_" + Core.Words.EnumWords.Name(line));

        static string K(string thing) => ScreenKeys.Key("map", thing);

        public static readonly string TitleKey = K("title");
        public static readonly string NewKey = K("new");
        public static readonly string OpenKey = K("open");
        public static readonly string CampaignKey = K("campaign");
        public static readonly string IdKey = K("id");
        public static readonly string IdRuleKey = K("id_rule");
        public static readonly string ColumnsKey = K("columns");
        public static readonly string RowsKey = K("rows");
        public static readonly string NoCampaignsKey = K("no_campaigns");
        public static readonly string NoMapsKey = K("no_maps");
        public static readonly string SaveKey = K("save");
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

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, NewKey, OpenKey, CampaignKey, IdKey, IdRuleKey, ColumnsKey, RowsKey, NoCampaignsKey, NoMapsKey,
                SaveKey, SavedCleanKey, SavedWithProblemsKey, CouldNotSaveKey, UndoKey, RedoKey, RotateKey, TopDownKey,
                ProblemsKey, NoProblemsKey, SpawnKey, LeaveKey, UnsavedKey, LeaveAnywayKey, StayKey, AllPropsKey,
            }
            .Concat(Grounds.Select(GroundKey))
            .Concat(Lines.Select(LineKey))
            .Concat(MapProblem.Names.Select(MapProblem.KeyFor));
    }
}
