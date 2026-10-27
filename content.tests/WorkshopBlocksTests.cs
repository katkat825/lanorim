using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Tables;
using Core.Characters;

namespace Content.Tests
{
    // cc_task_p: every block to Yarn and back; the two-keys band exactly where TwoKeys warns, and its two fixes clear it;
    // fail starters write valid blocks; memory - rename updates everything, never-set and never-checked are found;
    // conditions to Yarn and back, all and any, and on a choice
    public class WorkshopBlocksTests
    {
        static CampaignDraft Draft()
        {
            var draft = new CampaignDraft { Id = "probe" };
            draft.Tables.Encounters.Add(new EncounterTableDraft
            {
                Id = "fights",
                Entries = { new EncounterDraft { Id = "bridge_goblins", Kind = Core.Tables.EntryKind.Fight } },
            });
            draft.Tables.Encounters.Add(new EncounterTableDraft
            {
                Id = "road",
                Trigger = new TriggerDraft { Roll = "1d20", AtLeast = 11 },
                Entries = { new EncounterDraft { Id = "owl", Kind = Core.Tables.EntryKind.Line } },
            });
            draft.Tables.Loot.Add(new LootTableDraft { Id = "purse" });
            draft.Tables.Shops.Add(new ShopDraft { Id = "store" });
            draft.Tables.Shops.Add(new ShopDraft { Id = "racks", Armory = true });
            draft.Story.Cards.Add(CardDraft.New("gate"));
            draft.Story.Cards.Add(CardDraft.New("road"));
            return draft;
        }

        static readonly BlockKinds.Words Words = new("A line.", "An option.", "road", "Tavin was brave", "Korr trusts you");

        [Fact]
        public void EveryKindOfBlockWritesYarnThatReadsBackAsItself()
        {
            CampaignDraft draft = Draft();
            CardDraft gate = draft.Story.Card("gate");

            foreach (string kind in BlockKinds.All.Where(k => k != "blank"))
            {
                Block block = BlockKinds.Make(kind, draft, gate, Words);
                Assert.True(block is not Blank, $"{kind} makes a block");
                gate.Blocks.Add(block);
                Assert.Equal(kind == "level" ? "level" : kind == "xp" ? "xp" : kind, Kind(block, kind));
            }

            gate.Blocks.Add(new Stop());

            string yarn = gate.Write("\n");
            CardDraft again = YarnText.Of(yarn).Nodes.Select(n => CardDraft.Of(n, "x")).Single();

            Assert.False(again.IsText, "every block is in the editor's subset:\n" + yarn);
            Assert.Equal(yarn, again.Write("\n"));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(gate.Blocks), System.Text.Json.JsonSerializer.Serialize(again.Blocks));

            // and the whole card compiles with the game's own commands
            DialogueBook book = DialogueBook.Of("probe", ("x.yarn", yarn + CardDraft.New("road").Write("\n").Replace("---\n===", "---\nThe road. #line:road_1\n===")),
                                                ("memory.yarn", Memory.Declarations(draft.Story)));
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
            Assert.True(book.Program != null);

            foreach (Do act in Blocks.All(gate.Blocks).OfType<Do>())
                Assert.True(Request.Parse(act.Verb + " " + act.Words, out string problem) != null, $"<<{act.Verb} {act.Words}>>: {problem}");
        }

        static string Kind(Block block, string asked) => block switch
        {
            Do act => BlockKinds.Of(act) == "coins" && asked == "coins" ? "coins" : BlockKinds.Of(act),
            Set set => BlockKinds.Of(set),
            Branch => "only_if",
            Choice => "note",
            Say => "say",
            Jump => "jump",
            Stop => "stop",
            Comment => "comment",
            _ => "blank",
        };

        // --- two keys -------------------------------------------------------------------------------------------------

        static CardDraft Locked(CampaignDraft draft)
        {
            CardDraft gate = draft.Story.Card("gate");
            Do check = (Do)BlockKinds.Make("check", draft, gate, Words);
            check.After.Arms[0].Blocks.Add(new Jump { Target = "road" });
            check.After.Else.Add(new Stop());
            gate.Blocks.Add(new Say { Text = "The gate is locked.", LineId = draft.Story.NewLineId("gate") });
            gate.Blocks.Add(check);
            return gate;
        }

        [Fact]
        public void TheBandIsThereExactlyWhereTwoKeysWarns()
        {
            CampaignDraft draft = Draft();
            CardDraft gate = Locked(draft);
            Do check = gate.Blocks.OfType<Do>().Single();

            Assert.True(Rolls.NoWayPast(gate, check));
            Assert.NotEmpty(TwoKeys.Warnings("x", gate.Write("\n")));

            // a fail that carries on is no dead end
            check.After.Else.Clear();
            check.After.Else.Add(new Jump { Target = "road" });
            Assert.False(Rolls.NoWayPast(gate, check));
            Assert.Empty(TwoKeys.Warnings("x", gate.Write("\n")));
        }

        [Fact]
        public void AnotherWayAndAWayWithoutARollBothClearTheWarning()
        {
            foreach (bool skill in new[] { true, false })
            {
                CampaignDraft draft = Draft();
                CardDraft gate = Locked(draft);
                int at = gate.Blocks.FindIndex(b => b is Do);
                var check = (Do)gate.Blocks[at];

                gate.Blocks[at] = skill
                    ? Rolls.AnotherWay(draft.Story, gate, check, Rolls.OtherWays("athletics").First(s => s == Skill.Investigation), "Climb it", "Find another way")
                    : Rolls.WithoutARoll(draft.Story, gate, check, "Climb it", "Go round");

                string yarn = gate.Write("\n");
                Assert.Empty(TwoKeys.Warnings("x", yarn));
                Assert.False(YarnText.Of(yarn).Nodes.Select(n => CardDraft.Of(n, "x")).Single().IsText);

                DialogueBook book = DialogueBook.Of("probe", ("x.yarn", yarn + "title: road\nspeaker: dm\n---\nThe road. #line:road_1\n===\n"));
                Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
                Assert.Equal(gate.Write("\n").Split("#line:").Length - 1, Blocks.Lines(gate.Blocks).Select(l => l.Id).Distinct().Count());
            }
        }

        [Fact]
        public void EveryFailStarterWritesValidBlocks()
        {
            foreach (string starter in Rolls.Starters)
            {
                CampaignDraft draft = Draft();
                CardDraft gate = Locked(draft);
                Do check = gate.Blocks.OfType<Do>().Single();
                check.After.Else.Clear();
                check.After.Else.AddRange(Rolls.Starter(starter, "It costs you.", draft.Story, gate, draft.Tables));

                string yarn = gate.Write("\n");
                Assert.False(YarnText.Of(yarn).Nodes.Select(n => CardDraft.Of(n, "x")).Single().IsText, yarn);

                DialogueBook book = DialogueBook.Of("probe", ("x.yarn", yarn + "title: road\nspeaker: dm\n---\nThe road. #line:road_1\n===\n"));
                Assert.True(book.Problems.All(p => p.IsACaution), starter + ": " + string.Join("; ", book.Problems));
            }
        }

        // --- memory ---------------------------------------------------------------------------------------------------

        [Fact]
        public void TheMemoryListFindsNeverSetAndNeverCheckedAndRenameChangesEverything()
        {
            CampaignDraft draft = Draft();
            CardDraft gate = draft.Story.Card("gate"), road = draft.Story.Card("road");

            gate.Blocks.Add(new Set { Variable = Memory.Variable("Tavin was brave"), Value = "true" });
            gate.Blocks.Add(new Set { Variable = Memory.Variable("Korr trusts you"), Value = "$korr_trusts_you + 1" });
            road.Blocks.Add(new Branch { Arms = { new Arm { Condition = "$tavin_was_brave and $korr_trusts_you >= 2" } } });
            road.Blocks.Add(new Branch { Arms = { new Arm { Condition = "$the_bridge_fell" } } });
            road.Blocks.Add(new Set { Variable = "$picked_the_lock", Value = "true" });

            IReadOnlyList<Memory.Remembered> list = Memory.List(draft.Story);

            Assert.Equal(new[] { "$korr_trusts_you", "$picked_the_lock", "$tavin_was_brave", "$the_bridge_fell" }, list.Select(m => m.Variable));
            Assert.True(list.Single(m => m.Variable == "$the_bridge_fell").NeverSet);
            Assert.True(list.Single(m => m.Variable == "$picked_the_lock").NeverChecked);
            Assert.True(list.Single(m => m.Variable == "$korr_trusts_you").Count);
            Assert.DoesNotContain(list, m => m.Variable.StartsWith("$hero") || m.Variable == "$passed");

            Memory.Rename(draft.Story, draft.Story.Phrases, "$tavin_was_brave", "Tavin stood his ground");

            Assert.Equal("$tavin_stood_his_ground", gate.Blocks.OfType<Set>().First().Variable);
            Assert.Equal("$tavin_stood_his_ground and $korr_trusts_you >= 2", road.Blocks.OfType<Branch>().First().Arms[0].Condition);
            Assert.Equal("Tavin stood his ground", Memory.Phrase("$tavin_stood_his_ground", draft.Story.Phrases));
        }

        [Fact]
        public void WhatTheStoryRemembersIsDeclaredInTheWorkshopsOwnNode()
        {
            var story = new StoryDraft();
            CardDraft gate = CardDraft.New("gate");
            gate.Blocks.Add(new Set { Variable = "$tavin_was_brave", Value = "true" });
            gate.Blocks.Add(new Set { Variable = "$korr", Value = "$korr + 1" });
            gate.Blocks.Add(new Branch { Arms = { new Arm { Condition = "$tavin_was_brave and $korr >= 1 and $never_set", Blocks = { new Say { Text = "Brave.", Tags = { "line:g1" } } } } } });
            story.Cards.Add(gate);

            // a hand-written declaration in a Text card is the author's, not declared twice
            story.Cards.Add(new CardDraft { Text = "title: odd\nspeaker: dm\n---\n<<declare $own = 3>>\n<<set $own to 4>>\n===\n" });

            string memory = Memory.Declarations(story);
            Assert.Contains("<<declare $tavin_was_brave = false>>", memory);
            Assert.Contains("<<declare $korr = 0>>", memory);
            Assert.Contains("<<declare $never_set = false>>", memory);
            Assert.DoesNotContain("$own", memory);

            DialogueBook book = DialogueBook.Of("probe", ("x.yarn", gate.Write("\n")), ("memory.yarn", memory),
                                                ("odd.yarn", story.Cards[1].Text));
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
        }

        // --- conditions -----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("$tavin_was_brave")]
        [InlineData("not $tavin_was_brave")]
        [InlineData("$korr_trusts_you >= 2")]
        [InlineData("$korr_trusts_you <= 0")]
        [InlineData("$korr_trusts_you == 3")]
        [InlineData("$hero_class == \"fighter\"")]
        [InlineData("$hero_level >= 3")]
        [InlineData("$hero_casts")]
        [InlineData("has_item(\"rope\")")]
        [InlineData("$passed")]
        [InlineData("not $passed")]
        [InlineData("$fight == \"won\"")]
        [InlineData("$fight_broke")]
        [InlineData("$roll >= 15")]
        [InlineData("$encounter_fight")]
        [InlineData("$encounter == \"crows\"")]
        [InlineData("$loot_gold > 0")]
        [InlineData("$tavin_was_brave and $hero_level >= 3 and has_item(\"rope\")")]
        [InlineData("$hero_class == \"wizard\" or $hero_casts")]
        public void ConditionsGoToYarnAndBack(string yarn)
        {
            OnlyIf condition = OnlyIf.Of(yarn);

            Assert.Equal(yarn, condition.Yarn);
            Assert.DoesNotContain(condition.Clauses, c => c.Term == Term.Raw);
            Assert.Equal(yarn.Contains(" or "), condition.Any);
        }

        [Fact]
        public void AMixedConditionIsKeptAsWritten()
        {
            OnlyIf mixed = OnlyIf.Of("($a and $b) or $c");

            Assert.Equal(Term.Raw, mixed.Clauses.Single().Term);
            Assert.Equal("($a and $b) or $c", mixed.Yarn);
        }

        [Fact]
        public void AChoiceCanBeShownOnlyIf()
        {
            CampaignDraft draft = Draft();
            CardDraft gate = draft.Story.Card("gate");
            var note = (Choice)BlockKinds.Make("note", draft, gate, Words);
            note.Options[1].Condition = new OnlyIf(new[] { new Clause(Term.HeroClass, "wizard") }, false).Yarn;
            gate.Blocks.Add(note);
            gate.Blocks.Add(new Stop());

            string yarn = gate.Write("\n");
            Assert.Contains("-> An option. <<if $hero_class == \"wizard\">> #line:gate_2", yarn);

            CardDraft again = YarnText.Of(yarn).Nodes.Select(n => CardDraft.Of(n, "x")).Single();
            Assert.Equal("$hero_class == \"wizard\"", ((Choice)again.Blocks[0]).Options[1].Condition);

            DialogueBook book = DialogueBook.Of("probe", ("x.yarn", yarn));
            Assert.True(book.Problems.All(p => p.IsACaution), string.Join("; ", book.Problems));
        }
    }
}
