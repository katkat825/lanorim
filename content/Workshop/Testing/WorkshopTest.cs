using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Content.Spells;
using Content.Workshop.Story;
using Core.Magic;
using Core.Resolution;

namespace Content.Workshop.Testing
{
    // A TEST OF A CAMPAIGN BEING MADE (cc_task_q): the campaign as the page has it now - unsaved changes and all - written
    // into a folder of the test's own (never the author's, never the player's), read back as any campaign is, and played
    // with a test hero. The table's side (the Workshop bar, the fight-skip choice) is the game's; this is what it plays,
    // and the rules it bends: the hero's own checks and saves passed or failed on request, what the story remembers set
    // before the first line. Nothing it does reaches a real save
    public sealed class WorkshopTest
    {
        public WorkshopTest(CampaignEditor editor, TestSetup setup)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Setup = setup ?? new TestSetup();
        }

        public CampaignEditor Editor { get; }

        public TestSetup Setup { get; }

        // TRY THIS FIGHT (cc_task_n Part 1): the fight's id, played alone from a card of the test's own; "" for any other
        public string Fight { get; set; } = "";

        // the card a tried fight is started from
        public const string FightCard = "workshop_try_fight";

        // the hero's checks and saves: for real, always passed, always failed; and the next one only, once
        public RollRule Rolls { get; set; } = RollRule.Real;

        public RollRule? Next { get; set; }

        // the cards visited, in order, and the problems seen
        public List<string> Visited { get; } = new();

        public List<TestProblem> Problems { get; } = new();

        // the card the story is on, and the line (its id) it said last: where Back to the editor lands
        public string Card => Visited.LastOrDefault() ?? Setup.Start;

        public string LastLine { get; set; } = "";

        // --- the campaign, as it is now ------------------------------------------------------------------------------

        // WRITTEN INTO `root`/<its id>: the campaign's folder copied (its .draft/ left out), and the page's draft saved
        // over it. The folder, or null and why not
        public string Write(string root, out string problem)
        {
            problem = null;
            string folder = Path.Combine(root, Editor.Id);

            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);

                foreach (string file in Directory.EnumerateFiles(Editor.Folder, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(Editor.Folder, file);
                    if (relative.Split('/', '\\').Any(PackFolder.IsHidden)) continue;

                    string to = Path.Combine(folder, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(file, to);
                }

                CampaignDraft draft = Editor.Draft.Copy();

                // a tried fight: one card of its own, the fight and the end
                if (Fight.Length > 0)
                {
                    draft.Story.Cards.RemoveAll(c => c.Title == FightCard);
                    CardDraft card = CardDraft.New(FightCard);
                    card.Blocks.Add(new Do { Verb = "fight", Words = Fight });
                    card.Blocks.Add(new Stop());
                    draft.Story.Cards.Add(card);
                    Setup.Start = FightCard;
                }

                problem = CampaignFiles.Save(draft, folder);
                return problem == null ? folder : null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        // --- the run --------------------------------------------------------------------------------------------------

        // A RUN SET UP FOR THE TEST: what the story remembers set before its first line, and the hero's rolls ruled
        public void Prepare(CampaignRun run)
        {
            foreach ((string variable, string value) in Setup.Memory)
                run.Remember(variable, value == "true" ? true : value == "false" ? false
                                       : float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : (IConvertible)value);

            run.Referee.Judge = Judge;
        }

        // the hero's check or save, as the test says: for real, or passed or failed whatever the dice said. Only these:
        // the GM's rolls, a fight's attacks and the tables are never bent
        Attempt Judge(Attempt attempt)
        {
            RollRule rule = Next ?? Rolls;
            Next = null;

            return rule switch
            {
                RollRule.Pass when attempt.Failed => attempt.Rejudged(Math.Min(attempt.Against, attempt.Total)),
                RollRule.Fail when attempt.Succeeded => attempt.Rejudged(attempt.Total + 1),
                _ => attempt,
            };
        }

        // a step of the run seen: the card it's on, a line with no words, a fight with no map
        public void Saw(CampaignRun run, Func<string, bool> hasWords)
        {
            string node = run.Talk.Node;

            if (!string.IsNullOrEmpty(node) && node != Visited.LastOrDefault()) Visited.Add(node);

            if (run.Talk.Saying is { } line)
            {
                LastLine = line.Line.Local;
                if (!hasWords(line.Key)) Problem(TestProblemKind.NoWords, line.Key, node);
            }

            if (run.Now == Scene.Fight && run.Fight is { } fight && fight.MapId.Length > 0 && !run.Pack.Maps.ContainsKey(fight.MapId))
                Problem(TestProblemKind.NoMap, fight.MapId, node);
        }

        public void Problem(TestProblemKind kind, string what, string card)
        {
            if (!Problems.Any(p => p.Kind == kind && p.What == what)) Problems.Add(new TestProblem(kind, what, card ?? ""));
        }

        // the block a line is, in its card: where Back to the editor highlights
        public int[] Block(StoryDraft story)
        {
            CardDraft card = story.Card(Card);
            if (card?.Blocks == null || LastLine.Length == 0) return null;

            return Find(card.Blocks, Array.Empty<int>());

            int[] Find(List<Block> blocks, int[] inside)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    int[] path = StoryPath.Then(inside, i);

                    if (blocks[i] is Say say && say.LineId == LastLine) return path;
                    if (blocks[i] is Choice choice && choice.Options.Any(o => o.LineId == LastLine)) return path;

                    var lists = blocks[i].Inside.ToList();
                    for (int l = 0; l < lists.Count; l++)
                        if (Find(lists[l], StoryPath.Then(path, l)) is { } found) return found;
                }

                return null;
            }
        }
    }

    // something that went wrong in a test, and the card it was on
    public sealed record TestProblem(TestProblemKind Kind, string What, string Card);

    public static class EverySpell
    {
        // EVERY SPELL OF THE HERO'S LISTS it can cast at its level, known (the test hero's "everything at this level")
        public static void KnowEverySpell(this Hero hero, SpellBook spells)
        {
            if (!hero.Casts) return;

            int highest = hero.Caster.HighestAffordable;

            foreach (Spell spell in spells.For(hero.Class.Id).Where(s => s.Solo != Solo.Unavailable && s.Level <= highest && !hero.Caster.Knows(s.Id)).ToList())
                hero.Caster.Learn(spell);
        }
    }
}
