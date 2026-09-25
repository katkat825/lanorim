using System.IO;
using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    // Tier 2.10 of the 2026-09-24 run: the map builder's palette (props by category, from data) and
    // its tool modes - paint, wall, prop, spawn, start, erase - on MapDraft
    public class MapEditorTests
    {
        static MapEditor Editor()
        {
            var draft = new MapDraft(6, 4);
            var editor = new MapEditor(draft);

            editor.Drag(new Cell(0, 0), new Cell(5, 3));
            draft.PlaceStart(new Cell(0, 0));

            return editor;
        }

        [Fact]
        public void ThePaletteReadsAndEveryPropHasAPackAModelAndACategory()
        {
            PropCatalogue palette = PropCatalogue.Srd();

            Assert.Equal(new[] { "containers", "furniture", "light", "dungeon", "nature", "village" }, palette.Categories);
            Assert.True(palette.Props.Count >= 40);

            foreach (string category in palette.Categories)
                Assert.NotEmpty(palette.In(category));

            Assert.All(palette.Props, p =>
            {
                Assert.False(string.IsNullOrEmpty(p.Pack), p.Id);
                Assert.EndsWith(".gltf", p.Model);
            });

            Assert.True(palette.Find("barrel").Blocks);
            Assert.Equal("prop.barrel.name", palette.Find("barrel").NameKey);
        }

        [Fact]
        public void ThePaletteRefusesATwiceNamedPropAndAStrayCategory()
        {
            Assert.False(PropCatalogue.TryRead(@"{ ""categories"": [""light""],
                ""props"": [ { ""id"": ""torch"", ""category"": ""light"" },
                             { ""id"": ""torch"", ""category"": ""light"" },
                             { ""id"": ""rock"", ""category"": ""nature"" } ] }", out _, out var problems));

            Assert.Contains(problems, p => p.Contains("twice"));
            Assert.Contains(problems, p => p.Contains("'nature' is not one of"));
        }

        [Fact]
        public void EachToolDoesItsOneThing()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;

            Assert.Equal(Tile.Floor, draft.At(new Cell(5, 3)));

            editor.Tool = MapTool.Paint;
            editor.Ground = Tile.Rough;
            Assert.True(editor.Click(new Cell(2, 2)));
            Assert.Equal(Tile.Rough, draft.At(new Cell(2, 2)));

            editor.Tool = MapTool.Wall;
            Border line = Border.East(new Cell(1, 1));
            Assert.True(editor.Click(line));
            Assert.Equal(Edge.Wall, draft.At(line));

            editor.Browse("containers");
            Assert.All(editor.Showing, p => Assert.Equal("containers", p.Category));
            Assert.True(editor.Choose("barrel"));
            Assert.Equal(MapTool.Prop, editor.Tool);
            editor.Rotate(-1);
            Assert.True(editor.Click(new Cell(3, 1)));
            Assert.Equal(3, draft.Props.Single().Turn);

            editor.Tool = MapTool.Spawn;
            editor.SpawnSlot = 2;
            Assert.True(editor.Click(new Cell(5, 3)));
            Assert.Equal(new Cell(5, 3), draft.Spawns[2]);

            editor.Tool = MapTool.Start;
            Assert.True(editor.Click(new Cell(0, 3)));
            Assert.Equal(new Cell(0, 3), draft.Start);

            Assert.False(editor.Choose("a_thing_nobody_made"));
        }

        [Fact]
        public void EraseTakesThePropThenTheSpawnThenTheSquareAndNeverTheStart()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;
            var here = new Cell(3, 2);

            editor.Choose("crate");
            editor.Click(here);
            editor.Tool = MapTool.Spawn;
            editor.Click(here);

            editor.Tool = MapTool.Erase;

            Assert.True(editor.Click(here));
            Assert.Empty(draft.Props);
            Assert.True(editor.Click(here));
            Assert.Empty(draft.Spawns);
            Assert.True(editor.Click(here));
            Assert.Equal(Tile.Void, draft.At(here));

            Assert.False(editor.Click(draft.Start));

            // a wall erased from its line; and the whole lot undoes
            editor.Tool = MapTool.Wall;
            Border line = Border.East(new Cell(0, 1));
            editor.Click(line);
            editor.Tool = MapTool.Erase;
            Assert.True(editor.Click(line));
            Assert.Equal(Edge.None, draft.At(line));

            Assert.True(draft.Undo());
            Assert.Equal(Edge.Wall, draft.At(line));
        }

        [Fact]
        public void EveryPropTheSampleCampaignPlacesIsInThePalette()
        {
            string maps = Path.Combine(System.AppContext.BaseDirectory, "campaigns", "sample_millbrook", "maps");

            foreach (string file in Directory.GetFiles(maps, "*.map"))
            {
                Assert.True(MapDraft.TryRead(File.ReadAllText(file), out MapDraft draft, out string problem), problem);
                Assert.Empty(new MapEditor(draft).UnknownProps);
            }
        }
    }
}
