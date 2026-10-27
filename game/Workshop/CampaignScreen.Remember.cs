using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Story;
using Core.Localization;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // REMEMBER THAT... AND ONLY IF... (cc_task_p Part 4): the story's variables, which the author never sees. A phrase
    // ("Tavin was brave") is what's remembered; a condition is sentences picked from a list ("they remember that Tavin was
    // brave", "the hero is a Fighter"), all of them or any; an option can carry one ("Only shown if the hero is a Mage").
    // And the memory list on the Story tab: every phrase, where it's set and checked, Rename, and what's odd about it
    public partial class CampaignScreen
    {
        // --- remember, forget, a number -------------------------------------------------------------------------------

        void SetForm(VBoxContainer form, string card, int[] path, Set set)
        {
            string id = StoryPath.Name(path);
            string kind = BlockKinds.Of(set);

            // the phrase: one the story already remembers, or a new one typed
            LineEdit phrase = Forms.Line("Phrase_" + id, Memory.Phrase(set.Variable, Draft.Story.Phrases), _ => { }, BlockWords.PhraseHintKey);
            phrase.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            void Commit()
            {
                string variable = Memory.Variable(phrase.Text);
                if (variable == set.Variable && Memory.Phrase(variable, Draft.Story.Phrases) == phrase.Text.Trim()) return;

                Editor.Edit("block", d =>
                {
                    if (d.Story.Card(card)?.Blocks is not { } blocks || StoryPath.At(blocks, path) is not Set s) return;
                    string was = s.Variable;
                    s.Variable = variable;
                    if (kind == "count" && s.Value.StartsWith(was)) s.Value = variable + s.Value.Substring(was.Length);
                    d.Story.Phrases[variable] = phrase.Text.Trim();
                });
                Redraw();
            }

            phrase.TextSubmitted += _ => Commit();
            phrase.FocusExited += Commit;

            var known = Memory.List(Draft.Story).Where(m => m.Count == (kind == "count")).Select(m => m.Variable).ToList();
            var row = Ui.Row(8, phrase);

            if (known.Count > 0)
            {
                OptionButton pick = Forms.Choice("PickPhrase_" + id, known, known.Contains(set.Variable) ? set.Variable : known[0],
                    _ => null, v => { phrase.Text = Memory.Phrase(v, Draft.Story.Phrases); Commit(); });
                for (int i = 0; i < known.Count; i++) pick.SetItemText(i, Memory.Phrase(known[i], Draft.Story.Phrases));
                pick.CustomMinimumSize = new Vector2(Ui.Px(200), 0);
                row.AddChild(pick);
            }

            form.AddChild(Forms.Field(BlockWords.PhraseKey, row, null));

            if (kind == "count")
            {
                // add 1, take away 1, or set to a number
                string add = $"{set.Variable} + 1", take = $"{set.Variable} - 1";
                string how = set.Value == add ? "add" : set.Value == take ? "take" : "set";
                int.TryParse(set.Value, out int to);

                var choice = Forms.Choice("CountHow_" + id, new[] { "add", "take", "set" }, how,
                    h => h == "add" ? BlockWords.AddOneKey : h == "take" ? BlockWords.TakeOneKey : BlockWords.SetToKey,
                    h => Change(() => Editor.EditBlock<Set>(card, path, s => s.Value = h == "add" ? $"{s.Variable} + 1" : h == "take" ? $"{s.Variable} - 1" : "0")));
                var count = Ui.Row(8, choice);

                if (how == "set") count.AddChild(Forms.Number("CountTo_" + id, -99, 99, to, v => Editor.EditBlock<Set>(card, path, s => s.Value = v.ToString(), typing: true)));

                form.AddChild(count);
            }

            if (_advanced) form.AddChild(Forms.Note($"<<set {set.Variable} to {set.Value}>>"));
        }

        // --- only if --------------------------------------------------------------------------------------------------

        void BranchForm(VBoxContainer form, string card, int[] path, Branch branch)
        {
            string id = StoryPath.Name(path);

            for (int i = 0; i < branch.Arms.Count; i++)
            {
                int arm = i;
                form.AddChild(Ui.Label(i == 0 ? BlockWords.ThenKey : BlockWords.OrElseIfKey));
                form.AddChild(ConditionEditor($"{id}_{i}", branch.Arms[i].Condition,
                    yarn => Editor.EditBlock<Branch>(card, path, b => b.Arms[arm].Condition = yarn)));
                form.AddChild(BlockList(card, StoryPath.Then(path, i), branch.Arms[i].Blocks));
            }

            if (branch.Else != null)
            {
                form.AddChild(Ui.Label(BlockWords.OtherwiseKey));
                form.AddChild(BlockList(card, StoryPath.Then(path, branch.Arms.Count), branch.Else));
            }

            var adds = Ui.Row(8, Ui.Button(BlockWords.AddElseIfKey, () => Change(() => Editor.EditBlock<Branch>(card, path,
                b => b.Arms.Add(new Arm { Condition = Content.Dialogue.RequestVariables.Passed })))));
            if (branch.Else == null) adds.AddChild(Ui.Button(BlockWords.AddOtherwiseKey, () => Change(() => Editor.EditBlock<Branch>(card, path, b => b.Else = new List<Block>()))));
            adds.Name = "BranchAdds_" + id;
            form.AddChild(adds);
        }

        // AN OPTION'S "ONLY IF": "Always shown", or the condition it's shown under
        Control OptionCondition(string card, int[] path, int option, Option on)
        {
            string id = $"{StoryPath.Name(path)}_{option}";

            if (on.Condition.Length == 0)
            {
                Button make = Ui.Button(BlockWords.MakeConditionalKey, () => Change(() => Editor.EditBlock<Choice>(card, path,
                    c => c.Options[option].Condition = new Clause(Term.HeroClass, "fighter").Yarn)));
                make.Name = "OnlyShownIf_" + id;
                return Ui.Row(8, Forms.Hint(BlockWords.AlwaysShownKey), make);
            }

            var column = Ui.Column(4, Ui.Label(BlockWords.OnlyShownIfKey),
                ConditionEditor("Option_" + id, on.Condition, yarn => Editor.EditBlock<Choice>(card, path, c => c.Options[option].Condition = yarn)),
                Ui.Button(BlockWords.AlwaysShownKey, () => Change(() => Editor.EditBlock<Choice>(card, path, c => c.Options[option].Condition = ""))));
            column.Name = "OptionIf_" + id;
            return column;
        }

        // A CONDITION, IN SENTENCES: each clause a sentence with its blanks, All of these or Any of these, add another. One
        // the picker has no sentence for is its Yarn, editable in Advanced
        Control ConditionEditor(string id, string yarn, Action<string> changed)
        {
            OnlyIf condition = OnlyIf.Of(yarn);
            var column = Ui.Column(4);
            column.Name = "Condition_" + id;

            void Write(List<Clause> clauses, bool any)
            {
                changed(new OnlyIf(clauses, any).Yarn);
                Redraw();
            }

            if (condition.Clauses.Count > 1)
                column.AddChild(Forms.Choice("AllAny_" + id, new[] { false, true }, condition.Any, a => a ? BlockWords.AnyOfKey : BlockWords.AllOfKey,
                                             a => Write(condition.Clauses.ToList(), a)));

            for (int i = 0; i < condition.Clauses.Count; i++)
            {
                int at = i;
                column.AddChild(ClauseRow($"{id}_{i}", condition.Clauses[i], c =>
                {
                    var clauses = condition.Clauses.ToList();
                    if (c == null) clauses.RemoveAt(at);
                    else clauses[at] = c;
                    if (clauses.Count == 0) clauses.Add(new Clause(Term.Passed));
                    Write(clauses, condition.Any);
                }));
            }

            Button more = Ui.Button(BlockWords.AddClauseKey, () => Write(condition.Clauses.Append(new Clause(Term.Passed)).ToList(), condition.Any));
            more.Name = "AddClause_" + id;
            column.AddChild(more);
            return column;
        }

        // the sentences a clause can be, in the picker's order
        static readonly Term[] Picker =
        {
            Term.Remembers, Term.Forgot, Term.CountAtLeast, Term.CountAtMost, Term.CountIs, Term.HeroClass, Term.HeroLevel,
            Term.HeroCasts, Term.Carrying, Term.Passed, Term.Failed, Term.FightWas, Term.FightBroke, Term.RollAtLeast,
        };

        // ONE CLAUSE: which sentence, and its blank (a phrase, a class, an item, a number, an outcome); null removes it
        Control ClauseRow(string id, Clause clause, Action<Clause> changed)
        {
            var row = Ui.Row(6);
            row.Name = "Clause_" + id;

            if (clause.Term == Term.Raw)
            {
                LineEdit raw = Forms.Line("RawClause_" + id, clause.Name, t => { });
                raw.Editable = _advanced;
                raw.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                raw.TextSubmitted += t => changed(Clause.Of(t));
                row.AddChild(raw);
                return row;
            }

            var memories = Memory.List(Draft.Story);
            var flags = memories.Where(m => !m.Count).Select(m => m.Variable).DefaultIfEmpty("$something").ToList();
            var counts = memories.Where(m => m.Count).Select(m => m.Variable).DefaultIfEmpty("$a_number").ToList();

            OptionButton term = Forms.Choice("Term_" + id, Picker, clause.Term, BlockWords.Clause, t => changed(t switch
            {
                Term.Remembers or Term.Forgot => new Clause(t, flags[0]),
                Term.CountAtLeast or Term.CountAtMost or Term.CountIs => new Clause(t, counts[0], 1),
                Term.HeroClass => new Clause(t, "fighter"),
                Term.HeroLevel => new Clause(t, Number: 3),
                Term.Carrying => new Clause(t, "rope"),
                Term.FightWas => new Clause(t, "won"),
                Term.RollAtLeast => new Clause(t, Number: 15),
                _ => new Clause(t),
            }));
            for (int i = 0; i < Picker.Length; i++) term.SetItemText(i, Ui.Say(BlockWords.Clause(Picker[i]), "...", "..."));
            term.CustomMinimumSize = new Vector2(Ui.Px(300), 0);
            row.AddChild(term);

            switch (clause.Term)
            {
                case Term.Remembers:
                case Term.Forgot:
                    row.AddChild(Phrases("ClausePhrase_" + id, flags, clause.Name, v => changed(clause with { Name = v })));
                    break;

                case Term.CountAtLeast:
                case Term.CountAtMost:
                case Term.CountIs:
                    row.AddChild(Phrases("ClausePhrase_" + id, counts, clause.Name, v => changed(clause with { Name = v })));
                    row.AddChild(Forms.Number("ClauseNumber_" + id, -99, 99, clause.Number, v => changed(clause with { Number = v })));
                    break;

                case Term.HeroClass:
                    var classes = GameState.Content.Classes.Select(c => c.Id).ToList();
                    row.AddChild(Forms.Choice("ClauseClass_" + id, classes, classes.Contains(clause.Name) ? clause.Name : classes[0],
                                              c => KeyConventions.Key("class", c, "name"), c => changed(clause with { Name = c })));
                    break;

                case Term.HeroLevel:
                case Term.RollAtLeast:
                    row.AddChild(Forms.Number("ClauseNumber_" + id, 1, clause.Term == Term.HeroLevel ? 20 : 100, clause.Number, v => changed(clause with { Number = v })));
                    break;

                case Term.Carrying:
                    var items = Things.Items.All.Select(i => i.Id).OrderBy(Pickers.ItemName).ToList();
                    OptionButton item = Forms.Choice("ClauseItem_" + id, items, items.Contains(clause.Name) ? clause.Name : items[0], _ => null, v => changed(clause with { Name = v }));
                    for (int i = 0; i < items.Count; i++) item.SetItemText(i, Pickers.ItemName(items[i]));
                    row.AddChild(item);
                    break;

                case Term.FightWas:
                    row.AddChild(Forms.Choice("ClauseFight_" + id, BlockWords.Outcomes, BlockWords.Outcomes.Contains(clause.Name) ? clause.Name : "won", BlockWords.Outcome,
                                              v => changed(clause with { Name = v })));
                    break;
            }

            row.AddChild(Ui.Button(WorkshopWords.RemoveKey, () => changed(null)));
            return row;
        }

        OptionButton Phrases(string name, List<string> variables, string now, Action<string> picked)
        {
            if (!variables.Contains(now)) variables = variables.Append(now).ToList();

            OptionButton list = Forms.Choice(name, variables, now, _ => null, picked);
            for (int i = 0; i < variables.Count; i++) list.SetItemText(i, Memory.Phrase(variables[i], Draft.Story.Phrases));
            list.CustomMinimumSize = new Vector2(Ui.Px(180), 0);
            return list;
        }

        // a clause's blanks, said: a phrase as the author wrote it, a class by its name
        object[] ClauseArgs(Clause clause) => clause.Term switch
        {
            Term.HeroClass => new object[] { Ui.Say(KeyConventions.Key("class", clause.Name, "name")) },
            Term.Carrying => new object[] { Pickers.ItemName(clause.Name) },
            Term.Remembers or Term.Forgot => new object[] { Memory.Phrase(clause.Name, Draft.Story.Phrases) },
            Term.CountAtLeast or Term.CountAtMost or Term.CountIs => new object[] { Memory.Phrase(clause.Name, Draft.Story.Phrases), clause.Number },
            Term.FightWas => new object[] { Ui.Say(BlockWords.Outcome(clause.Name)) },
            _ => new object[] { clause.Number == 0 ? clause.Name : clause.Number.ToString() },
        };

        // --- the memory list ------------------------------------------------------------------------------------------

        // WHAT THE STORY REMEMBERS, on the Story tab: each phrase, where it's set and checked, what's odd, and Rename
        Control MemoryList()
        {
            var column = Ui.Column(4, Ui.Title(BlockWords.MemoryKey));
            column.Name = "MemoryList";

            IReadOnlyList<Memory.Remembered> all = Memory.List(Draft.Story);
            if (all.Count == 0) column.AddChild(Forms.Hint(BlockWords.NoMemoryKey));

            foreach (Memory.Remembered one in all)
            {
                string variable = one.Variable;
                LineEdit phrase = Forms.Line("Memory_" + variable.TrimStart('$'), Memory.Phrase(variable, Draft.Story.Phrases), _ => { });
                phrase.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                Button rename = Ui.Button(BlockWords.RenameKey, () =>
                {
                    if (string.IsNullOrWhiteSpace(phrase.Text)) return;
                    Change(() => Editor.Edit("memory", d => Memory.Rename(d.Story, d.Story.Phrases, variable, phrase.Text)));
                });
                rename.Name = "RenameMemory_" + variable.TrimStart('$');

                var row = Ui.Column(2, Ui.Row(8, phrase, rename),
                    Forms.Hint(BlockWords.SetInKey, string.Join(", ", one.SetIn.Select(Draft.Story.NameOf))),
                    Forms.Hint(BlockWords.CheckedInKey, string.Join(", ", one.CheckedIn.Select(Draft.Story.NameOf))));

                if (one.NeverChecked || one.NeverSet)
                {
                    Label odd = Ui.Label(one.NeverSet ? BlockWords.NeverSetKey : BlockWords.NeverCheckedKey);
                    odd.AddThemeColorOverride("font_color", one.NeverSet ? Forms.Wrong : new Color(0.75f, 0.5f, 0.05f));
                    odd.Name = one.NeverSet ? "NeverSet" : "NeverChecked";
                    row.AddChild(odd);
                }

                if (_advanced) row.AddChild(Forms.Note(variable));

                row.Name = "Remembered_" + variable.TrimStart('$');
                column.AddChild(row);
            }

            if (Draft.Tags.Contains("published")) column.AddChild(Forms.Hint(BlockWords.PublishedKey));

            return column;
        }
    }
}
