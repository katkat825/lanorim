using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Content.Workshop.Story;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Workshop.Testing
{
    // RUN IT FOR ME (cc_task_q Part 5): the campaign (or from a card) played by itself, the --auto way - first choices, an
    // AutoPlayer in each fight (the one the balance sim and the sample's play-through use), the dice for real - and a
    // report: the cards it reached, the ones it never did (and what stands in the way: a choice, an Only if), and what
    // went wrong. TRY EVERY CHOICE plays it again with each different answer at the choices it met, up to a cap
    public static class RunForMe
    {
        public const int MostRuns = 25;

        const int MostSteps = 5000;

        // what stands in the way of a card: the card that leads to it, the option of a note the go-to is under (its letter, ""
        // for none), and every condition it's behind (an option's "only if", a branch's arm; "" for an otherwise)
        public sealed record Gate(string From, string Option, IReadOnlyList<string> Conditions)
        {
            public override string ToString() =>
                From + (Option.Length > 0 ? $": option {Option}" : "") + (Conditions.Count > 0 ? $" ({string.Join(", ", Conditions)})" : "");
        }

        public sealed record Report(IReadOnlyList<string> Reached, IReadOnlyList<(string Card, IReadOnlyList<Gate> Behind)> NeverReached,
                                    IReadOnlyList<TestProblem> Problems, int Runs, IReadOnlyList<string> Endings);

        // one run, `plan` answering the choices it meets in order (0, the first option, past its end); what it reached
        // and how many options each choice it met had
        public sealed record Run(List<string> Reached, List<int> Choices, string Ended, List<TestProblem> Problems);

        public static Run Once(Library library, Package pack, Func<Hero> hero, string start, IReadOnlyList<int> plan, int seed,
                               SaveLibrary saves, Func<string, bool> hasWords = null)
        {
            var rng = new SeededRng(seed);
            var run = new CampaignRun(library, pack, hero(), new StandardResolver(rng), rng, saves, 0);
            var reached = new List<string>();
            var choices = new List<int>();
            var problems = new List<TestProblem>();

            void Seen()
            {
                string node = run.Talk.Node;
                if (!string.IsNullOrEmpty(node) && !reached.Contains(node)) reached.Add(node);

                if (run.Talk.Saying is { } line && hasWords != null && !hasWords(line.Key))
                    Add(problems, new TestProblem(TestProblemKind.NoWords, line.Key, node ?? ""));
            }

            run.Start(string.IsNullOrEmpty(start) ? null : start);

            for (int step = 0; step < MostSteps; step++)
            {
                Seen();

                switch (run.Now)
                {
                    case Scene.Line:
                        run.Next();
                        break;

                    case Scene.Choice:
                        int at = choices.Count;
                        choices.Add(run.Talk.Choosing.Count);
                        int pick = at < plan.Count ? plan[at] : 0;
                        if (pick >= run.Talk.Choosing.Count || !run.Talk.Choosing[pick].Offered) pick = Enumerable.Range(0, run.Talk.Choosing.Count).FirstOrDefault(i => run.Talk.Choosing[i].Offered);
                        run.Choose(pick);
                        break;

                    case Scene.Shop:
                        run.LeaveShop();
                        break;

                    case Scene.Fight:
                        if (run.Fight.MapId.Length > 0 && !pack.Maps.ContainsKey(run.Fight.MapId))
                        {
                            Add(problems, new TestProblem(TestProblemKind.NoMap, run.Fight.MapId, run.Talk.Node ?? ""));
                            run.EndFight(Outcome.HeroesWon);
                            break;
                        }

                        Battle battle = run.BattleFor(new StandardResolver(rng));
                        Outcome outcome = battle == null ? Outcome.HeroesWon : new AutoPlayer().Play(new CombatSession(battle, library.Items, library.Forms));
                        run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome);
                        break;

                    case Scene.Dead:
                        return new Run(reached, choices, run.Talk.Node ?? "", problems);

                    default:
                        return new Run(reached, choices, run.Talk.Node ?? reached.LastOrDefault() ?? "", problems);
                }
            }

            Add(problems, new TestProblem(TestProblemKind.Other, $"still going after {MostSteps} steps", run.Talk.Node ?? ""));
            return new Run(reached, choices, run.Talk.Node ?? "", problems);
        }

        static void Add(List<TestProblem> problems, TestProblem problem)
        {
            if (!problems.Any(p => p.Kind == problem.Kind && p.What == problem.What)) problems.Add(problem);
        }

        // ONE RUN, or EVERY CHOICE: each plan that differs from one already run at a single choice it met, breadth first,
        // until no new plan is left or the cap is reached
        public static Report All(Library library, Package pack, Func<Hero> hero, string start, StoryDraft story, bool everyChoice,
                                 SaveLibrary saves, Func<string, bool> hasWords = null, int most = MostRuns)
        {
            var plans = new Queue<List<int>>();
            plans.Enqueue(new List<int>());
            var tried = new HashSet<string> { "" };
            var reached = new List<string>();
            var endings = new List<string>();
            var problems = new List<TestProblem>();
            int runs = 0;

            while (plans.Count > 0 && runs < (everyChoice ? most : 1))
            {
                List<int> plan = plans.Dequeue();
                Run one = Once(library, pack, hero, start, plan, 1000 + runs, saves, hasWords);
                runs++;

                foreach (string card in one.Reached.Where(c => !reached.Contains(c))) reached.Add(card);
                if (!endings.Contains(one.Ended)) endings.Add(one.Ended);
                foreach (TestProblem problem in one.Problems) Add(problems, problem);

                if (!everyChoice) break;

                // at each choice this run met past its plan, every other answer, the rest of the plan as it went
                for (int at = 0; at < one.Choices.Count; at++)
                    for (int other = 0; other < one.Choices[at]; other++)
                    {
                        var next = Enumerable.Range(0, at).Select(i => i < plan.Count ? plan[i] : 0).Append(other).ToList();
                        if (tried.Add(string.Join(",", next))) plans.Enqueue(next);
                    }
            }

            var never = story.Cards.Select(c => c.Title).Where(t => !reached.Contains(t))
                             .Select(t => (t, Behind(story, t))).ToList();

            return new Report(reached, never, problems, runs, endings);
        }

        // WHAT STANDS IN THE WAY OF A CARD: each card that leads to it, and the option or Only if the go-to is under
        // ("camp, option B", "cellar, only if the last check passed"); nothing leads to it at all, none
        public static IReadOnlyList<Gate> Behind(StoryDraft story, string card) =>
            story.Cards.Where(c => !c.IsText)
                 .SelectMany(from => Gates(from.Blocks, card, "", new List<string>()).Select(g => new Gate(from.Title, g.Option, g.Conditions)))
                 .ToList();

        static IEnumerable<(string Option, IReadOnlyList<string> Conditions)> Gates(IEnumerable<Block> blocks, string to, string option, List<string> under)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case Jump jump when jump.Target == to:
                        yield return (option, under.ToList());
                        break;

                    case Choice choice:
                        for (int i = 0; i < choice.Options.Count; i++)
                        {
                            Option one = choice.Options[i];
                            List<string> deeper = one.Condition.Length > 0 ? under.Append(one.Condition).ToList() : under;
                            foreach (var g in Gates(one.Blocks, to, StoryDraft.Letter(i), deeper)) yield return g;
                        }
                        break;

                    case Branch branch:
                        foreach (Arm arm in branch.Arms)
                            foreach (var g in Gates(arm.Blocks, to, option, under.Append(arm.Condition).ToList())) yield return g;
                        if (branch.Else != null)
                            foreach (var g in Gates(branch.Else, to, option, under.Append("").ToList())) yield return g;
                        break;

                    case Do { After: { } after }:
                        foreach (var g in Gates(new Block[] { after }, to, option, under)) yield return g;
                        break;
                }
            }
        }
    }
}
