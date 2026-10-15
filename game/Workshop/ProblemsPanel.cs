using System;
using System.Linq;
using Content.Workshop;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE PROBLEMS PANEL (cc_task_m Part 5): everything the campaign check says (WorkshopProblems), in plain words
    // (ProblemWords), grouped Story, Fights, Maps, Tables and About, errors and warnings told apart, each with Go there
    // when its part of the page exists. One ProblemWords doesn't know keeps its own words; Advanced shows every one's own
    // words under its plain ones. Filled again on every save
    public static class ProblemsPanel
    {
        static readonly Color ErrorColour = new(1f, 0.55f, 0.45f);
        static readonly Color WarningColour = new(1f, 0.82f, 0.45f);

        public static void Fill(VBoxContainer panel, CampaignEditor editor, bool advanced, Action<WorkshopTab> goThere)
        {
            Ui.Clear(panel);

            panel.AddChild(Ui.Title(WorkshopWords.ProblemsKey));

            var plain = ProblemWords.Plain(editor.Problems);

            if (plain.Count == 0)
            {
                panel.AddChild(Forms.Hint(WorkshopWords.NoProblemsKey));
                return;
            }

            foreach (var group in plain.GroupBy(p => p.Group).OrderBy(g => g.Key))
            {
                panel.AddChild(Ui.Label(ProblemWords.GroupKey(group.Key)));

                foreach (PlainProblem problem in group.OrderBy(p => p.IsError ? 0 : 1)) panel.AddChild(One(problem, editor.Draft, advanced, goThere));
            }
        }

        static Control One(PlainProblem problem, CampaignDraft draft, bool advanced, Action<WorkshopTab> goThere)
        {
            Label how = Ui.Label(problem.IsError ? WorkshopWords.ErrorKey : WorkshopWords.WarningKey);
            how.Modulate = problem.IsError ? ErrorColour : WarningColour;

            string said = problem.Known
                ? Ui.Say(problem.Key, problem.Args.Select(a => (object)Shown(a?.ToString(), draft)).ToArray())
                : problem.Problem.What;

            var one = Ui.Column(2, how, Ui.Plain(said));
            one.Name = "Problem_" + (problem.Kind ?? "unknown");

            if (advanced && problem.Known) one.AddChild(Forms.Hint(problem.Problem.ToString()));

            if (problem.Tab is { } tab)
            {
                Button go = Ui.Button(WorkshopWords.GoThereKey, () => goThere(tab));
                go.Name = "GoThere";
                one.AddChild(Ui.Row(8, go));
            }

            return one;
        }

        // a name a problem quotes, as the author knows it: a chapter by its title, anything else as written
        static string Shown(string arg, CampaignDraft draft) =>
            draft.Chapter(arg) is { Title.Length: > 0 } chapter ? chapter.Title : arg ?? "";
    }
}
