using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Story;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE BLOCKS' FORMS, TALK AND GO (cc_task_o Part 2): Someone says... (who, what, the hero's name as a chip), the GM
    // hands a note (its options A, B, C, each with the blocks under it and maybe a go-to at its end), Go to card... (or a
    // new card, made to the right), and The story ends. P's commands, rememberings and Only ifs are in
    // CampaignScreen.Commands.cs and CampaignScreen.Remember.cs. No Yarn word shows outside Advanced
    public partial class CampaignScreen
    {
        void BlockForm(VBoxContainer form, string card, int[] path, Block block)
        {
            switch (block)
            {
                case Say say: SayForm(form, card, path, say); break;
                case Choice note: NoteForm(form, card, path, note); break;
                case Jump jump: JumpForm(form, card, path, jump); break;
                case Stop: form.AddChild(Forms.Hint(StoryWords.StoryEndsKey)); break;
                case Do act: CommandForm(form, card, path, act); break;
                case Set set: SetForm(form, card, path, set); break;
                case Branch branch: BranchForm(form, card, path, branch); break;
                case Comment comment:
                    form.AddChild(Forms.Line("Comment_" + StoryPath.Name(path), comment.Text.TrimStart(),
                        t => Editor.EditBlock<Comment>(card, path, c => c.Text = " " + t, typing: true)));
                    break;
            }
        }

        // --- someone says -----------------------------------------------------------------------------------------

        void SayForm(VBoxContainer form, string card, int[] path, Say say)
        {
            string id = StoryPath.Name(path);
            CardDraft owner = Draft.Story.Card(card);

            form.AddChild(Forms.Field(StoryWords.WhoKey, Speaker("Who_" + id, owner, say.Speaker,
                who => Editor.EditBlock<Say>(card, path, s => s.Speaker = who == owner.Speaker ? "" : who)),
                say.Speaker == StoryWords.Companion || say.Speaker.Length == 0 && owner.Speaker == StoryWords.Companion ? StoryWords.CompanionHintKey : null));

            LineEdit words = Forms.Line("What_" + id, say.Text, t => Editor.EditBlock<Say>(card, path, s => s.Text = t, typing: true));
            words.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // the hero's name, at the cursor: {$hero_name}, shown as a chip under the box
            Button hero = Ui.Button(StoryWords.HeroNameKey, () =>
            {
                words.InsertTextAtCaret(StoryWords.HeroName);
                Editor.EditBlock<Say>(card, path, s => s.Text = words.Text, typing: true);
                Redraw();
            });
            hero.Name = "HeroName_" + id;

            form.AddChild(Forms.Field(StoryWords.WhatKey, Ui.Row(8, words, hero), null, Chips(say.Text)));

            if (_advanced) form.AddChild(Forms.Note(Ui.Say(StoryWords.LineIdKey, say.LineId)));
        }

        // the hero's name in a line, as a chip: "the hero's name" in a box of its own
        static Control Chips(string text)
        {
            if (!text.Contains(StoryWords.HeroName)) return null;

            Label chip = Ui.Label(StoryWords.HeroNameKey);
            chip.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.85f, 0.75f, 0.5f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 6, ContentMarginRight = 6 });
            chip.Name = "HeroChip";
            return Ui.Row(4, chip);
        }

        // WHO: the GM, the companion ("written once, said by whichever companion the hero has"), or one of the campaign's
        // characters
        OptionButton Speaker(string name, CardDraft card, string now, Action<string> picked)
        {
            var who = new List<string> { StoryWords.Gm, StoryWords.Companion };
            who.AddRange(Draft.People.Select(p => p.Id).Where(p => !who.Contains(p)));

            string current = now.Length > 0 ? now : card.Speaker;
            if (!who.Contains(current)) who.Add(current);

            OptionButton list = Forms.Choice(name, who, current, w => w == StoryWords.Gm ? StoryWords.GmKey : w == StoryWords.Companion ? StoryWords.CompanionKey : null, picked);

            for (int i = 2; i < who.Count; i++)
                list.SetItemText(i, Draft.Character(who[i]) is { Name.Length: > 0 } person ? person.Name : Content.Workshop.Tables.TablesDraft.Plain(who[i]));

            return list;
        }

        // --- the note ---------------------------------------------------------------------------------------------

        void NoteForm(VBoxContainer form, string card, int[] path, Choice note)
        {
            string id = StoryPath.Name(path);

            // the GM's line before the note: the block above it, when that's a line
            if (path[^1] > 0 && StoryPath.At(Draft.Story.Card(card).Blocks, StoryPath.Then(path.Take(path.Length - 1).ToArray(), path[^1] - 1)) is Say before)
                form.AddChild(Forms.Note(Ui.Say(StoryWords.NoteIntroKey) + ": " + before.Text));

            for (int i = 0; i < note.Options.Count; i++)
            {
                int at = i;
                Option option = note.Options[i];

                Label letter = Ui.Plain(StoryDraft.Letter(i));
                letter.ThemeTypeVariation = "TitleLabel";

                LineEdit words = Forms.Line($"Option_{id}_{i}", option.Text,
                    t => Editor.EditBlock<Choice>(card, path, c => c.Options[at].Text = t, typing: true));
                words.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                var row = Ui.Row(8, letter, words,
                    Ui.Button(StoryWords.MoveUpKey, () => Change(() => Editor.EditBlock<Choice>(card, path, c => Swap(c.Options, at, at - 1)))).Greyed(at == 0, null),
                    note.Options.Count <= 1 ? new Control() : Forms.Removal($"RemoveOption_{id}_{i}", () => Change(() => Editor.EditBlock<Choice>(card, path, c => c.Options.RemoveAt(at)))));
                row.Name = $"OptionRow_{id}_{i}";
                form.AddChild(row);

                // "only shown if..." (P)
                form.AddChild(OptionCondition(card, path, at, option));

                // the blocks under it, indented
                form.AddChild(BlockList(card, StoryPath.Then(path, at), option.Blocks));

                if (_advanced) form.AddChild(Forms.Note(Ui.Say(StoryWords.LineIdKey, option.LineId)));
            }

            Button add = Ui.Button(StoryWords.AddOptionKey, () => Change(() => Editor.EditBlock<Choice>(card, path, c =>
            {
                var option = new Option { Text = Ui.Say(StoryWords.NewOptionKey) };
                option.LineId = Draft.Story.NewLineId(card);
                c.Options.Add(option);
            })));
            add.Name = "AddOption_" + id;
            form.AddChild(add);
        }

        static void Swap<T>(List<T> list, int a, int b)
        {
            if (b < 0 || b >= list.Count) return;
            (list[a], list[b]) = (list[b], list[a]);
        }

        // --- go to card -------------------------------------------------------------------------------------------

        void JumpForm(VBoxContainer form, string card, int[] path, Jump jump)
        {
            var titles = Draft.Story.Cards.Select(c => c.Title).ToList();
            if (!titles.Contains(jump.Target)) titles.Add(jump.Target);

            const string fresh = "\n";
            var values = titles.Append(fresh).ToList();

            OptionButton to = Forms.Choice("GoTo_" + StoryPath.Name(path), values, jump.Target, v => v == fresh ? StoryWords.ANewCardKey : null, v =>
            {
                if (v != fresh)
                {
                    Editor.EditBlock<Jump>(card, path, j => j.Target = v);
                    return;
                }

                // a new card, placed to the right of this one
                CardLook look = Draft.Story.Looks.TryGetValue(card, out CardLook l) ? l : new CardLook();
                string made = Editor.NewCard(Ui.Say(StoryWords.NewCardKey), look.X + StoryBoard.Tile.X + 70, look.Y);
                Change(() => Editor.EditBlock<Jump>(card, path, j => j.Target = made));
            });

            for (int i = 0; i < titles.Count; i++) to.SetItemText(i, Draft.Story.NameOf(titles[i]));

            form.AddChild(Forms.Field(StoryWords.GoToKey, to, null));
        }
    }
}
