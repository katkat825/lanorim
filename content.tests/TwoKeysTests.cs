using System.Linq;
using Content.Dialogue;
using Xunit;

namespace Content.Tests
{
    // 3.5 (cc_task_h): TWO KEYS. A check the story can't get past on a fail is warned about, unless the node offers
    // another skill on a different ability or a choice; a shape the check can't read, it says nothing about
    public class TwoKeysTests
    {
        const string DeadEnd = @"title: gate
---
The gate is barred.
<<check athletics 15>>
<<if $passed>>
    It gives.
    <<jump yard>>
<<endif>>
===
";

        static string[] Said(string source) =>
            TwoKeys.Warnings("story.yarn", source).Where(p => p.IsACaution).Select(p => p.What).ToArray();

        [Fact]
        public void AFailThatGoesNowhereIsWarned() =>
            Assert.Single(Said(DeadEnd), w => w.Contains("athletics") && w.Contains("two keys"));

        [Fact]
        public void AFailThatStopsTheStoryIsWarned() =>
            Assert.Single(Said(@"title: gate
---
<<check athletics 15>>
<<if $passed>>
    <<jump yard>>
<<else>>
    You give up and go home.
    <<stop>>
<<endif>>
===
"));

        [Fact]
        public void AnotherSkillOnAnotherAbilityIsTheSecondKey()
        {
            // the same dead end, with a Dexterity way in offered beside it in the node: quiet
            Assert.Empty(Said(DeadEnd.Replace("The gate is barred.", "The gate is barred.\n<<check sleight_of_hand 12>>")));

            // but a second Strength check is the same key twice
            Assert.NotEmpty(Said(DeadEnd.Replace("The gate is barred.", "The gate is barred.\n<<check athletics 10>>")));
        }

        [Fact]
        public void AChoiceIsAWayThrough() =>
            Assert.Empty(Said(DeadEnd.Replace("The gate is barred.", "The gate is barred.\n-> Climb\n-> Look for another way")));

        [Fact]
        public void AFailThatCarriesOnIsFine() =>
            Assert.Empty(Said(DeadEnd.Replace("<<endif>>", "<<endif>>\nYou find the coal chute instead.\n<<jump yard>>")));

        [Fact]
        public void TheSampleCampaignHasNoDeadEnds()
        {
            string story = System.IO.File.ReadAllText(System.IO.Path.Combine(SampleCampaignTests.Folder, "dialogue", "story.yarn"));
            Assert.Empty(Said(story));
        }
    }
}
