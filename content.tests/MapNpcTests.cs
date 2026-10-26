using System.IO;
using System.Linq;
using Content.Combat;
using Content.Maps;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Dice;
using Core.Space;

namespace Content.Tests
{
    // PEOPLE ON THE MAP (cc_task_m3 Part 10): a figure from the People picker is placed, picked, turned, deleted and named
    // as a prop is; the map file keeps them in their own "npcs" list; in play one blocks a walk and not sight, and is in
    // no fight
    public class MapNpcTests
    {
        static readonly PropCatalogue Palette = PropCatalogue.Srd();

        // a painted floor, the start in a corner, as the hand tests' (MapEditorHandTests)
        static MapEditor Editor() => MapEditorHandTests.Editor();

        static MapTarget At(int x, int y) => MapEditorHandTests.At(x, y);

        [Fact]
        public void ThePeoplePickerOffersTheVillagersAndNothingElseDoes()
        {
            var people = Palette.Matching("", p => p.Id, null, PropShelf.People).ToList();

            Assert.Equal(34, people.Count);
            Assert.All(people, p => Assert.True(p.Figure && p.Blocks && p.Width == 1 && p.Depth == 1, p.Id));
            Assert.Equal(new[] { "townsfolk", "guards", "travellers" }, Palette.CategoriesOn(PropShelf.People));

            Assert.DoesNotContain(Palette.Matching("", p => p.Id, null, PropShelf.Props), p => p.Figure);
            Assert.DoesNotContain(Palette.Matching("", p => p.Id, null, PropShelf.Buildings), p => p.Figure);
            Assert.DoesNotContain(people, p => p.Id.Contains("goblin") || p.Id.Contains("zombie") || p.Id.Contains("suit") ||
                                               p.Id.Contains("cowboy") || p.Id.Contains("basecharacter"));

            // Kathleen, 2026-10-23: no Elf, no robes, no animals
            Assert.DoesNotContain(people, p => p.Id is "npc_elf" or "npc_kimono_male" or "npc_kimono_female" or "npc_cow" or "npc_pug");
        }

        [Fact]
        public void AFigureIsPlacedPickedTurnedNamedAndDeletedAsAPropIs()
        {
            MapEditor editor = Editor();

            Assert.True(editor.Choose("npc_chef_female"));
            Assert.Equal(1, editor.Apply(At(3, 3), At(3, 3), more: false));
            Assert.Equal("npc_chef_female", editor.Picked.Id);

            editor.Rotate(1);
            Assert.Equal(1, editor.Picked.Turn);

            Assert.True(editor.NamePicked("old_maud"));
            Assert.Equal("old_maud", editor.Draft.PropAt(new Cell(3, 3)).Actor);
            Assert.Equal(1, editor.Draft.PropAt(new Cell(3, 3)).Turn);

            // a barrel is no one
            Assert.True(editor.Choose("barrel"));
            editor.Apply(At(5, 5), At(5, 5), more: false);
            Assert.False(editor.NamePicked("old_maud"));

            // and undo takes the name back off
            editor.Draft.Undo();
            editor.Draft.Undo();
            Assert.Equal("", editor.Draft.PropAt(new Cell(3, 3)).Actor);
        }

        [Fact]
        public void TheMapFileKeepsThePeopleInTheirOwnList()
        {
            MapEditor editor = Editor();
            editor.Draft.PlaceProp("barrel", new Cell(1, 1));
            editor.Draft.PlaceProp("npc_wizard", new Cell(4, 4), 2, "elder_tam");
            editor.Draft.PlaceProp("npc_witch", new Cell(5, 4));

            string saved = editor.Draft.Save();

            Assert.Contains("\"npcs\": [", saved);
            Assert.Contains("{ \"figure\": \"npc_wizard\", \"x\": 4, \"y\": 4, \"turn\": 2, \"actor\": \"elder_tam\" }", saved);
            Assert.Contains("{ \"figure\": \"npc_witch\", \"x\": 5, \"y\": 4, \"turn\": 0 }", saved);
            Assert.DoesNotContain("\"id\": \"npc_wizard\"", saved);

            Assert.True(MapDraft.TryRead(saved, out MapDraft again, out string problem), problem);
            Assert.Equal(saved, again.Save());
            Assert.Contains(again.Props, p => p.Id == "npc_wizard" && p.Actor == "elder_tam" && p.Turn == 2);
        }

        [Fact]
        public void AMapWithNoPeopleIsWrittenAsItAlwaysWas()
        {
            foreach (string id in MapTemplates.Ids)
            {
                MapDraft drawn = MapTemplates.Read(id);
                Assert.DoesNotContain("npcs", drawn.Save());
            }

            Assert.True(MapDraft.TryRead(MapTemplates.Read("village_street").Save(), out MapDraft street, out _));
            Assert.Equal(MapTemplates.Read("village_street").Save(), street.Save());
        }

        [Fact]
        public void APersonBlocksAWalkAndNotSight()
        {
            // a one-square-wide corridor, a guard standing in it
            var draft = new MapDraft(5, 1);
            draft.Paint(new Cell(0, 0), new Cell(4, 0), Tile.Floor);
            draft.PlaceStart(new Cell(0, 0));
            Assert.True(draft.PlaceProp("npc_soldier_male", new Cell(2, 0)));

            MapLayout map = draft.Layout();

            Assert.Contains(new Cell(2, 0), map.Blocked);
            Assert.False(Route.Exists(map, new Cell(0, 0), new Cell(4, 0), _ => false));
            Assert.True(Sight.Clear(map, new Cell(0, 0), new Cell(4, 0)));
        }

        [Fact]
        public void APersonIsInNoFight()
        {
            var draft = new MapDraft(8, 4);
            draft.Paint(new Cell(0, 0), new Cell(7, 3), Tile.Floor);
            draft.PlaceStart(new Cell(0, 0));
            draft.PlaceProp("npc_casual_male", new Cell(3, 1), 0, "farmer");

            Library srd = Library.Srd();
            Hero hero = Creation.QuickHero.Make(srd, "fighter", 1, "Tess");
            var resolver = new TableResolver(new ScriptedRng(1), new Twenties(), a => ReferenceEquals(a, hero.Actor));

            Battle battle = Battle.Set(srd, draft.Layout(), hero, new[] { new Battle.Foe(srd.Bestiary.Find("goblin"), new Cell(7, 3)) },
                                       resolver, new PolicyChooser(new ReactionSettings(), new AnswerAlways(false)));

            Assert.Equal(2, battle.Fight.Actors.Count());
            Assert.DoesNotContain(battle.Fight.Actors, a => a.Id.Contains("casual"));
            Assert.Contains(new Cell(3, 1), battle.Fight.Field.Map.Blocked);
        }

        sealed class Twenties : IDiceSource
        {
            public System.Collections.Generic.IReadOnlyList<int> Throw(System.Collections.Generic.IReadOnlyList<Die> dice) =>
                dice.Select(d => d.Sides()).ToList();
        }

        // --- the problems ---------------------------------------------------------------------------------------------

        [Fact]
        public void APersonWhereNobodyCouldStandIsAWarningAndTheMapStillPlays()
        {
            MapEditor editor = Editor();
            Assert.True(editor.Draft.PlaceProp("npc_wizard", new Cell(6, 2)));
            editor.Draft.Paint(new Cell(6, 2), Tile.Void);

            MapProblem problem = Assert.Single(editor.Draft.Findings());
            Assert.Equal(MapProblem.KeyFor("npc_cant_stand"), problem.Said.Key);
            Assert.True(problem.Warning);
            Assert.Equal(new Cell(6, 2), problem.At);
            Assert.True(editor.Draft.Sound);
        }

        [Fact]
        public void APersonNamingACharacterTheCampaignHasNoMoreIsAWarning()
        {
            string folder = Path.Combine(Path.GetTempPath(), "lanorim_npc_" + System.Guid.NewGuid().ToString("N"));
            WorkshopTests.Copy(SampleCampaignTests.Folder, folder);

            try
            {
                MapPlace place = MapPlace.OfCampaign(folder);
                MapEditor editor = Editor();
                editor.Draft.PlaceProp("npc_witch", new Cell(2, 2), 0, "nobody_we_know");

                var view = new MapBuilderView(editor, place, "npc_map");

                // the campaign's own characters are offered, and one added from the map is the campaign's
                string id = view.AddPerson("Old Maud");
                Assert.NotNull(id);
                Assert.Contains(view.People, p => p.Id == id && p.Name == "Old Maud");
                Assert.Contains(Workshop.CampaignEditor.Open(folder).Draft.People, p => p.Id == id);

                MapProblem stranger = Assert.Single(view.Problems, p => p.Said.Key == MapProblem.KeyFor("npc_unknown_person"));
                Assert.True(stranger.Warning);

                editor.Draft.NameProp(editor.Draft.PropAt(new Cell(2, 2)), id);
                Assert.DoesNotContain(view.Problems, p => p.Said.Key == MapProblem.KeyFor("npc_unknown_person"));
                Assert.Equal("Old Maud", view.NameOf(id));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void MyMapsOffersOnlySomeone()
        {
            var view = new MapBuilderView(Editor(), MapPlace.Library(Path.GetTempPath()), "mine");

            Assert.Empty(view.People);
            Assert.Null(view.AddPerson("Anyone"));
        }
    }
}
