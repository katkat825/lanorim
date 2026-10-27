using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Game.Play;
using Game.Screens;
using Game.Workshop;
using Godot;

namespace Game.Dev
{
    // THE STORY EDITOR, DRIVEN (cc_task_o and cc_task_p, check-workshop extended), on the probe's campaign through the Story
    // tab's own controls:
    //   - the first chapter's card gets a note with two options, A going to a new card "Gate", B to a new card "Meadow";
    //   - Meadow: a line, and the story ends (one ending);
    //   - Gate (P's scene): an Athletics check whose pass remembers "picked the lock", starts the fight with They won and
    //     They ran, searches the loot with "if they found coins", and goes on to "Ending"; the two-keys band shows, and
    //     "Add another way" clears it; after the note, the card goes on to Ending too;
    //   - Ending: Only if they remember that "picked the lock" - one line, otherwise another - and the story ends.
    // Saved, it loads with no errors, compiles, and Try every choice plays it headless to both endings
    public partial class WorkshopProbe
    {
        const string Gate = "gate", Meadow = "meadow", Ending = "ending", Picked = "$picked_the_lock";

        StoryDraft Story => Page.Editor.Draft.Story;

        // a block from a list's Add block palette, by its kind
        void AddBlock(string card, int[] inside, string kind)
        {
            if (Page.OpenStoryCard != card) Page.OpenStory(card);

            MenuButton add = Find<MenuButton>("AddBlock_" + StoryPath.Name(inside));
            Expect(add != null, $"an Add block palette under {card} [{StoryPath.Name(inside)}]");

            int id = BlockKinds.Palette.SelectMany(p => p.Kinds).ToList().IndexOf(kind);
            add?.GetPopup().EmitSignal(PopupMenu.SignalName.IdPressed, id);
        }

        void NewCard(string name)
        {
            Page.OpenStory("");
            Type<LineEdit>("NewCardName", name, changed: false);
            Press("NewCard");
            Expect(Story.Card(name.ToLowerInvariant()) != null, $"a card {name} on the board");
        }

        void StoryCards()
        {
            Page.GoThere(WorkshopTab.Story);
            Expect(Find<StoryBoard>("Board") != null, "the Story tab is a board");
            Expect(Story.Card("first_steps") != null, "the first chapter's card is on it");
            Expect(Has<HBoxContainer>(r => r.Name == "NoStart_second_steps"), "the second chapter has no card yet, and says so");
            Press("MakeStart_second_steps");
            Expect(Story.Card("second_steps") != null, "Make it makes its card");

            NewCard("Gate");
            NewCard("Meadow");
            NewCard("Ending");
        }

        // the first card: a note, A to the gate and B to the meadow
        void StoryNote()
        {
            CardDraft first = Story.Card("first_steps");
            int note = first.Blocks.Count;

            AddBlock("first_steps", Array.Empty<int>(), "note");
            Expect(Story.Card("first_steps").Blocks.ElementAtOrDefault(note) is Choice { Options.Count: 2 }, "a note with two options");

            Type<LineEdit>($"Option_{note}_0", "Try the gate.");
            Type<LineEdit>($"Option_{note}_1", "Wander the meadow.");

            foreach ((int option, string to) in new[] { (0, Gate), (1, Meadow) })
            {
                AddBlock("first_steps", new[] { note, option }, "jump");
                Choose(Find<OptionButton>($"GoTo_{note}_{option}_0"), Story.Cards.Select(c => c.Title).ToList().IndexOf(to));
            }

            Expect(StoryDraft.Leads(Story.Card("first_steps")).SequenceEqual(new[] { (Gate, "A"), (Meadow, "B") }),
                   $"the arrows go A to the gate and B to the meadow ({string.Join(", ", StoryDraft.Leads(Story.Card("first_steps")))})");

            // the meadow: a line and the end
            AddBlock(Meadow, Array.Empty<int>(), "say");
            Type<LineEdit>("What_0", "Bees, and nothing else. A quiet ending.");
            AddBlock(Meadow, Array.Empty<int>(), "stop");
            Expect(Story.Card(Meadow).Blocks.Select(b => b.GetType().Name).SequenceEqual(new[] { "Say", "Stop" }), "the meadow ends the story");
        }

        // the gate: P's scene
        void StoryGate()
        {
            AddBlock(Gate, Array.Empty<int>(), "check");
            Expect(Story.Card(Gate).Blocks[0] is Do { Verb: "check", After: not null }, "a check, with its pass and its fail");

            // the pass: remember, the fight, the loot, on to the ending
            int[] pass = { 0, 0 };
            AddBlock(Gate, pass, "remember");
            Type<LineEdit>("Phrase_0_0_0", "Picked the lock", changed: false);
            Find<LineEdit>("Phrase_0_0_0")?.EmitSignal(LineEdit.SignalName.TextSubmitted, "Picked the lock");
            Expect(Story.Card(Gate).Blocks[0] is Do { After.Arms: [{ Blocks: [Set { Variable: Picked, Value: "true" }] }] }, "the pass remembers that they picked the lock");

            AddBlock(Gate, pass, "fight");
            foreach (string outcome in new[] { "won", "fled" })
                Nodes.Under<Button>(Find<HBoxContainer>("AddAfter_0_0_1") ?? new HBoxContainer())
                     .FirstOrDefault(b => b.Text == Ui.Say(BlockWords.AddOutcomeKey, Ui.Say(BlockWords.Outcome(outcome))))
                     ?.EmitSignal(BaseButton.SignalName.Pressed);
            Do fight = StoryPath.At(Story.Card(Gate).Blocks, new[] { 0, 0, 1 }) as Do;
            Expect(fight?.Verb == "fight" && fight.After?.Arms.Select(a => a.Condition).SequenceEqual(new[] { "$fight == \"won\"", "$fight == \"fled\"" }) == true,
                   $"a fight with They won and They ran ({string.Join(", ", fight?.After?.Arms.Select(a => a.Condition) ?? Array.Empty<string>())})");

            AddBlock(Gate, pass, "loot");
            Find<HBoxContainer>("AddAfter_0_0_2")?.GetChildren().OfType<Button>().FirstOrDefault()?.EmitSignal(BaseButton.SignalName.Pressed);
            Expect(StoryPath.At(Story.Card(Gate).Blocks, new[] { 0, 0, 2 }) is Do { Verb: "loot", After.Arms: [{ Condition: "$loot_gold > 0" }] }, "a loot search with \"if they found coins\"");

            AddBlock(Gate, pass, "jump");
            Choose(Find<OptionButton>("GoTo_0_0_3"), Story.Cards.Select(c => c.Title).ToList().IndexOf(Ending));

            // the band: only heroes good at Athletics get past here; Add another way clears it
            Expect(Find<PanelContainer>("TwoKeys_0") != null, "the two-keys band shows on the check");
            Press("AnotherWay_0");
            Expect(Story.Card(Gate).Blocks[0] is Choice { Options.Count: 2 }, "Add another way makes a note of two checks");
            Expect(Find<PanelContainer>("TwoKeys_0_0_0") == null, "and the band is gone");
            Expect(!TwoKeys.Warnings("x", Story.Card(Gate).Write("\n")).Any(), "TwoKeys has nothing to say");

            // after the note, on to the ending as well (a fail carries on)
            AddBlock(Gate, Array.Empty<int>(), "jump");
            Choose(Find<OptionButton>("GoTo_1"), Story.Cards.Select(c => c.Title).ToList().IndexOf(Ending));
        }

        // the ending: Only if they remember that they picked the lock
        void StoryEnding()
        {
            AddBlock(Ending, Array.Empty<int>(), "only_if");
            Choose(Find<OptionButton>("Term_0_0_0"), 0);
            Choose(Find<OptionButton>("ClausePhrase_0_0_0"), 0);
            Expect((Story.Card(Ending).Blocks[0] as Branch)?.Arms[0].Condition == Picked, $"Only if they remember that they picked the lock ({(Story.Card(Ending).Blocks[0] as Branch)?.Arms[0].Condition})");

            AddBlock(Ending, new[] { 0, 0 }, "say");
            Type<LineEdit>("What_0_0_0", "The gate stands open behind you. You got in.");
            Find<HBoxContainer>("BranchAdds_0")?.GetChildren().OfType<Button>().Last().EmitSignal(BaseButton.SignalName.Pressed);
            AddBlock(Ending, new[] { 0, 1 }, "say");
            Type<LineEdit>("What_0_1_0", "The gate stays shut. You go home the long way.");
            AddBlock(Ending, Array.Empty<int>(), "stop");

            Expect(Memory.List(Story).Single(m => m.Variable == Picked) is { NeverSet: false, NeverChecked: false }, "the memory list: set at the gate, checked at the ending");
        }

        // after Save: the story's files, compiled, and played headless to both endings
        void StorySaved()
        {
            string yarn = File.ReadAllText(Path.Combine(Folder, "dialogue", "story.yarn"));
            Expect(yarn.Contains("<<check athletics 15>>") && yarn.Contains("<<if $picked_the_lock>>") && !yarn.Contains("\t"), "the story is the editor's Yarn");
            Expect(File.Exists(Path.Combine(Folder, Memory.File)), "and what it remembers is declared");

            DialogueBook book = DialogueBook.Read(Path.Combine(Folder, "dialogue"), CampaignId);
            Expect(book.Problems.All(p => p.IsACaution), "it compiles: " + string.Join("; ", book.Problems));

            Package pack = Package.Read(Folder);
            var library = GameState.Content.With(pack);
            var saves = new Content.Saves.SaveLibrary(ProjectSettings.GlobalizePath(Root + "/run_saves"));
            RunForMe.Report every = RunForMe.All(library, pack, () => new TestSetup { Level = 3 }.Hero(library, "Probe"), null, Story, true, saves);

            Expect(every.Reached.Contains(Meadow) && every.Reached.Contains(Ending), $"played headless to both endings ({string.Join(", ", every.Reached)}; {every.Runs} runs)");
        }
    }
}
