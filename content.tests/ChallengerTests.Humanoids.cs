using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Challengers;
using Xunit;

namespace Content.Tests
{
    // HUMANOIDS ONLY (cc_task_ca Part 1; Kathleen 2026-11-16: "v1 won't have non-humanoid challengers"): a challenger's
    // stat blocks carry ArenaFolk's tags, humanoid or goblinoid. The importer refuses the rest (ChallengerTests'
    // bad-stats theory); so does the campaign check, for a type and for a special fight; and the shared lists draw only
    // humanoids
    public partial class ChallengerTests
    {
        [Fact]
        public void TheGoblinsCountAndTheOgreTheWolfAndTheUndeadDont()
        {
            Assert.True(ArenaFolk.Allows(Srd.Bestiary.Find("goblin")));          // fey, goblinoid: her test
            Assert.True(ArenaFolk.Allows(Srd.Bestiary.Find("goblin_boss")));
            Assert.True(ArenaFolk.Allows(Srd.Bestiary.Find("goblin_archer")));
            Assert.True(ArenaFolk.Allows(Srd.Bestiary.Find("gladiator")));

            foreach (string id in new[] { "ogre", "wolf", "ghoul", "skeleton", "brown_bear" })
            {
                Assert.False(ArenaFolk.Allows(Srd.Bestiary.Find(id)), id);
                Assert.StartsWith($"{id} isn't a humanoid", ArenaFolk.Refusal(Srd.Bestiary.Find(id)));
            }
        }

        [Fact]
        public void TheCampaignCheckRefusesANonHumanoidTypeAndSpecialFight()
        {
            string temp = Path.Combine(Path.GetTempPath(), "lanorim_challengers_" + Guid.NewGuid().ToString("N"));
            string pack = Path.Combine(temp, "crucible_fixture");

            try
            {
                Copy(CrucibleTests.Folder, pack);

                File.WriteAllText(Path.Combine(pack, "challengers", "beasts.json"),
                                  @"{""challengers"":[{""id"":""masked_ogre"",""shape"":""single"",""monsters"":[{""monster"":""ogre""}],""names"":0,""descriptions"":0}]}");

                string bouts = Path.Combine(pack, "encounters", "bouts.json");
                File.WriteAllText(bouts, File.ReadAllText(bouts).Replace(@"""monster"": ""guard""", @"""monster"": ""wolf"""));

                Package read = Package.Read(pack);

                Assert.Contains(read.Problems, p => p.File == Package.ChallengersFolder && p.Where == "challengers.masked_ogre" &&
                                                    p.What.StartsWith("ogre isn't a humanoid"));
                Assert.Contains(read.Problems, p => p.File == Package.EncountersFolder && p.Where == "bouts.spear_trial" &&
                                                    p.What.StartsWith("wolf isn't a humanoid"));
                Assert.False(read.Sound);
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [Fact]
        public void TheSharedListsDrawOnlyHumanoids()
        {
            ChallengerType[] shared = Pack().Challengers.Where(t => t.Shared).ToArray();

            var drawn = Enumerable.Range(1, 30)
                                  .SelectMany(seed => Drawn(1, seed, reputation: 40, types: shared,
                                                            bestiary: new[] { "ogre", "wolf", "giant_rat", "goblin", "bandit" }).Roster)
                                  .Where(c => !c.Special)
                                  .ToList();

            Assert.NotEmpty(drawn);
            Assert.All(drawn, c => Assert.All(c.Group, m => Assert.Contains(m.Monster, new[] { "goblin", "bandit" })));
        }
    }
}
