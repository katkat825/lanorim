using System;
using System.IO;
using Content.Maps;
using Content.Workshop;

namespace Content.Tests
{
    // A MAP'S NAME AND ITS ID (cc_task_m2 Part 7): the name is what the author types (letters of either case, numbers,
    // spaces, dashes, underscores); the id is made from it, lowercase, spaces and dashes underscores, and free in the
    // place the map lives; the name is kept in the map file, and a map without one shows its id. The campaign name in
    // the wizard keeps the same rule
    public class MapNameTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_mapname_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        MapPlace Place(string name) => MapPlace.Library(Path.Combine(_root, name));

        [Theory]
        [InlineData("Mill Yard", true)]
        [InlineData("mill_yard-2", true)]
        [InlineData("UPPER lower 123", true)]
        [InlineData("Kathleen's yard", false)]
        [InlineData("a/b", false)]
        [InlineData("café", false)]
        [InlineData("   ", false)]
        [InlineData("", false)]
        public void OnlyLettersNumbersDashesSpacesAndUnderscores(string name, bool allowed) =>
            Assert.Equal(allowed, MapName.Allowed(name));

        [Theory]
        [InlineData("Mill Yard", "mill_yard")]
        [InlineData("Mill Yard - North", "mill_yard_north")]
        [InlineData("old-road", "old_road")]
        [InlineData("Cellar_2", "cellar_2")]
        public void TheIdIsMadeFromTheName(string name, string id)
        {
            Assert.Equal(id, MapName.IdOf(name));
            Assert.True(Campaigns.ContentId.IsLocal(MapName.IdOf(name)));
        }

        [Fact]
        public void TheIdIsFreeInItsOwnPlaceAndOnlyThere()
        {
            MapPlace mine = Place("mine"), theirs = Place("campaign_maps");
            var draft = new MapDraft(6, 6) { Name = "Mill Yard" };

            Assert.Null(MapFiles.Write(mine, MapName.IdIn(mine, "Mill Yard"), draft));

            // the same name again where one lives: the next free id; somewhere else, its own
            Assert.Equal("mill_yard_2", MapName.IdIn(mine, "Mill Yard"));
            Assert.Equal("mill_yard_2", MapName.IdIn(mine, "mill-yard"));
            Assert.Equal("mill_yard", MapName.IdIn(theirs, "Mill Yard"));
        }

        [Fact]
        public void TheNameIsKeptInTheFileAndReadBack()
        {
            MapPlace mine = Place("mine");
            var draft = new MapDraft(6, 6) { Name = "Mill Yard - North" };

            Assert.Contains("\"name\": \"Mill Yard - North\"", draft.Save());
            Assert.Null(MapFiles.Write(mine, "mill_yard_north", draft));

            Assert.True(MapFiles.Read(mine, "mill_yard_north", out MapDraft back, out string problem), problem);
            Assert.Equal("Mill Yard - North", back.Name);
            Assert.Equal("Mill Yard - North", MapName.Of(mine, "mill_yard_north"));
        }

        [Fact]
        public void AMapWithNoNameShowsItsId()
        {
            MapPlace mine = Place("mine");

            // written with no name at all, as format 1 maps and the templates are
            Assert.DoesNotContain("\"name\"", new MapDraft(6, 6).Save());
            Assert.Null(MapFiles.Write(mine, "old_cellar", new MapDraft(6, 6)));
            Assert.Equal("old_cellar", MapName.Of(mine, "old_cellar"));

            Directory.CreateDirectory(Path.Combine(_root, "old"));
            File.WriteAllText(Path.Combine(_root, "old", "format_one.map"),
                              new MapDraft(3, 3).Save().Replace("\"format\": 2", "\"format\": 1"));
            Assert.Equal("format_one", MapName.Of(Place("old"), "format_one"));
            Assert.True(MapFiles.Read(Place("old"), "format_one", out MapDraft old, out string problem), problem);
            Assert.Equal("", old.Name);
        }

        [Fact]
        public void BlankIsTheFirstTemplateOpenGroundWithTheStartInTheMiddle()
        {
            Assert.Equal("blank", MapTemplates.Ids[0]);

            MapDraft blank = MapTemplates.Make("blank", 12, 10);
            Assert.Empty(blank.Props);
            Assert.Equal(new Core.Space.Cell(6, 5), blank.Start);
            Assert.DoesNotContain("\"walls\"", blank.Save());
            Assert.DoesNotContain("\"ground\"", blank.Save());
            Assert.DoesNotContain("|", blank.Save());
        }

        [Fact]
        public void TheWizardHoldsACampaignNameThatBreaksTheRule()
        {
            CampaignWizard wizard = WorkshopTests.Filled(null);

            wizard.Draft.Name = "Kathleen's Quest";
            Assert.Equal(MapName.RuleKey, wizard.Missing(WizardPage.About));

            wizard.Draft.Name = "Kathleens Quest - Part 1";
            Assert.Null(wizard.Missing(WizardPage.About));
        }
    }
}
