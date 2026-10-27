using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Story;
using Game.Access;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // A CARD OPEN (cc_task_o Part 2): its name, colour, Preview, Play from here (Q), its problems, and its blocks top to
    // bottom - each with its form and its own small menu (Move up, Move down, Duplicate, Delete) - and Add block, whose
    // palette is grouped (Talk, Go; P adds Rolls, Fights, Rewards, Places, Companion, Remember). A Text card shows its
    // Yarn, editable only in Advanced. Advanced shows the card's Yarn beside its blocks, read-only
    public partial class CampaignScreen
    {
        void Card(VBoxContainer page, string title)
        {
            CardDraft card = Draft.Story.Card(title);

            Button back = Ui.Button(StoryWords.BackToBoardKey, () => { _openStory = ""; _highlight = null; Redraw(); });
            back.Name = "BackToBoard";

            LineEdit name = Forms.Line("CardName", Draft.Story.NameOf(title), _ => { });
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.Editable = Draft.Chapter(title) == null;

            void Rename()
            {
                if (name.Text.Trim() == Draft.Story.NameOf(title)) return;
                Editor.RenameCard(title, name.Text);
                _openStory = Draft.Story.Cards.FirstOrDefault(c => Draft.Story.NameOf(c.Title) == name.Text.Trim())?.Title ?? title;
                Redraw();
            }

            name.TextSubmitted += _ => Rename();
            name.FocusExited += Rename;

            CardLook look = Draft.Story.Looks.TryGetValue(title, out CardLook l) ? l : new CardLook();
            var colour = new ColorPickerButton { Name = "CardColour", Color = look.Colour.Length > 0 ? Color.FromHtml(look.Colour) : new Color(0.98f, 0.93f, 0.8f), CustomMinimumSize = new Vector2(Ui.Px(48), 0) };
            colour.PopupClosed += () => Editor.ColourCard(title, colour.Color.ToHtml(false));

            Button preview = Ui.Button(StoryWords.PreviewKey, () => Preview(title));
            preview.Name = "Preview";
            preview.Disabled = card.IsText;

            Button play = Ui.Button(Content.Workshop.Testing.TestWords.PlayFromHereKey, () => PlayFromHere(title, null));
            play.Name = "PlayFromHere";

            Button delete = Ui.Button(WorkshopWords.DeleteKey, AskToDeleteOpenCard);
            delete.Name = "DeleteCard";

            var top = Ui.Row(8, back, name, colour, preview, play, delete);
            top.Name = "CardTop";
            page.AddChild(top);

            if (Draft.Chapter(title) != null) page.AddChild(Forms.Hint(StoryWords.ChapterStartKey));

            foreach (StoryProblems.OnCard problem in _storyProblems.Where(p => p.Card == title))
            {
                Label said = problem.HintKey != null ? Ui.Label(problem.HintKey) : Forms.Note(ProblemsPanel.Said(problem.Problem, Draft));
                said.AddThemeColorOverride("font_color", problem.Dot == Dot.Red ? Forms.Wrong : new Color(0.75f, 0.5f, 0.05f));
                said.Name = "CardProblem";
                page.AddChild(said);
            }

            if (card.IsText)
            {
                page.AddChild(Forms.Hint(StoryWords.TextCardHintKey));
                var raw = new TextEdit
                {
                    Name = "TextCard",
                    Text = card.Text,
                    Editable = _advanced,
                    CustomMinimumSize = new Vector2(0, Ui.Px(320)),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                raw.AddThemeFontOverride("font", ThemeDB.FallbackFont);
                raw.TextChanged += () => Editor.EditText(title, raw.Text);
                page.AddChild(raw);
                return;
            }

            Control blocks = BlockList(title, Array.Empty<int>(), card.Blocks);

            if (!_advanced)
            {
                page.AddChild(blocks);
                return;
            }

            // Advanced: the Yarn it writes, beside the blocks
            var yarn = new TextEdit
            {
                Name = "CardYarn",
                Text = card.Write("\n"),
                Editable = false,
                CustomMinimumSize = new Vector2(Ui.Px(360), Ui.Px(320)),
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            yarn.AddThemeFontOverride("font", ThemeDB.FallbackFont);
            blocks.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            page.AddChild(Ui.Row(12, blocks, Ui.Column(4, Ui.Label(StoryWords.TheirYarnKey), yarn)));
        }

        void AskToDeleteOpenCard()
        {
            string title = _openStory;
            Node top = Nodes.Under<HBoxContainer>(this).FirstOrDefault(r => r.Name == "CardTop");
            if (top == null) return;

            Ui.Clear(top);
            top.AddChild(Ui.Label(StoryWords.DeleteCardKey, Draft.Story.NameOf(title)));
            Button yes = Ui.Button(WorkshopWords.DeleteKey, () => { _openStory = ""; Change(() => Editor.DeleteCard(title)); });
            yes.Name = "ReallyDeleteCard";
            Button keep = Ui.Button(WorkshopWords.KeepKey, Redraw);
            top.AddChild(yes);
            top.AddChild(keep);
            Ui.FocusLater(keep);
        }

        // --- a list of blocks, and each block -----------------------------------------------------------------------

        // `inside`: the path of the list (StoryPath): [] the card's own, [2, 1] the second option's of the third block
        VBoxContainer BlockList(string card, int[] inside, List<Block> list)
        {
            var column = Ui.Column(6);
            column.Name = "Blocks_" + StoryPath.Name(inside);

            for (int i = 0; i < list.Count; i++)
            {
                column.AddChild(BlockRow(card, StoryPath.Then(inside, i), list[i], i, list.Count));

                // after a note, the card carries on: "and then"
                if (list[i] is Choice && i < list.Count - 1) column.AddChild(Forms.Hint(StoryWords.AndThenKey));
            }

            column.AddChild(Palette(card, inside));
            return column;
        }

        Control BlockRow(string card, int[] path, Block block, int at, int count)
        {
            Label name = Ui.Label(StoryWords.BlockName(KindOf(block)));
            name.ThemeTypeVariation = "TitleLabel";
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.AutowrapMode = TextServer.AutowrapMode.Off;

            var menu = new MenuButton { Name = "Menu_" + StoryPath.Name(path), Text = "...", FocusMode = FocusModeEnum.All, Flat = false };
            PopupMenu items = menu.GetPopup();
            items.AddItem(Ui.Say(StoryWords.MoveUpKey), 0);
            items.AddItem(Ui.Say(StoryWords.MoveDownKey), 1);
            items.AddItem(Ui.Say(StoryWords.DuplicateBlockKey), 2);
            items.AddItem(Ui.Say(StoryWords.DeleteBlockKey), 3);
            items.SetItemDisabled(0, at == 0);
            items.SetItemDisabled(1, at == count - 1);
            items.IdPressed += id => BlockMenu(card, path, (int)id);

            var column = Ui.Column(6, Ui.Row(8, name, menu));
            BlockForm(column, card, path, block);

            PanelContainer row = Ui.Panel(column);
            row.Name = "Block_" + StoryPath.Name(path);
            row.FocusMode = FocusModeEnum.All;
            row.GuiInput += e => BlockKeys(card, path, e, row);

            if (_highlight != null && _highlight.SequenceEqual(path))
            {
                row.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(1f, 0.85f, 0.4f), BorderColor = new Color(0.8f, 0.4f, 0.05f), BorderWidthLeft = 4, ContentMarginLeft = 10, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
                row.SetMeta("highlighted", true);
                Ui.FocusLater(row);
            }

            if (path.Length <= 1) return row;

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", (int)Ui.Px(20));
            margin.AddChild(row);
            return margin;
        }

        void BlockMenu(string card, int[] path, int item)
        {
            switch (item)
            {
                case 0: Change(() => Editor.MoveBlock(card, path, -1)); break;
                case 1: Change(() => Editor.MoveBlock(card, path, 1)); break;
                case 2: Change(() => Editor.DuplicateBlock(card, path)); break;
                case 3: Change(() => Editor.DeleteBlock(card, path)); break;
            }
        }

        // a block's keys: Ctrl+D duplicates it, Delete takes it out
        void BlockKeys(string card, int[] path, InputEvent @event, Control row)
        {
            if (@event is not InputEventKey { Pressed: true } key) return;

            Act? act = Keyboard.Pressed(key);

            if (act == Act.DuplicateCard) Change(() => Editor.DuplicateBlock(card, path));
            else if (act == Act.DeleteSelection) Change(() => Editor.DeleteBlock(card, path));
            else return;

            row.AcceptEvent();
        }

        // the palette word for a block ("say", "note", "jump"...; a command's own verb, P)
        static string KindOf(Block block) => block switch
        {
            Content.Workshop.Story.Say => "say",
            Choice => "note",
            Jump => "jump",
            Stop => "stop",
            Do act => BlockKinds.Of(act),
            Set set => BlockKinds.Of(set),
            Branch => "only_if",
            Comment => "comment",
            _ => "blank",
        };

        // ADD BLOCK: the palette, grouped, into this list's end
        MenuButton Palette(string card, int[] inside)
        {
            var add = new MenuButton { Name = "AddBlock_" + StoryPath.Name(inside), Text = Ui.Say(StoryWords.AddBlockKey), Flat = false, FocusMode = FocusModeEnum.All };
            PopupMenu menu = add.GetPopup();
            var kinds = new List<string>();

            foreach ((string group, string[] blocks) in BlockKinds.Palette)
            {
                menu.AddSeparator(Ui.Say(StoryWords.Group(group)));
                foreach (string kind in blocks)
                {
                    menu.AddItem(Ui.Say(StoryWords.BlockName(kind)), kinds.Count);
                    kinds.Add(kind);
                }
            }

            menu.IdPressed += id =>
            {
                string kind = kinds[(int)id];
                int[] made = Editor.AddBlock(card, inside, -1, (story, c) => BlockKinds.Make(kind, story, c, Defaults()));
                _highlight = made;
                Redraw();
            };

            return add;
        }

        // the words a new block starts with, from the game's own locale (BlockKinds.Make)
        BlockKinds.Words Defaults() => new(Ui.Say(StoryWords.NewLineKey), Ui.Say(StoryWords.NewOptionKey),
                                           Draft.Story.Cards.Select(c => c.Title).FirstOrDefault(t => t != _openStory) ?? "",
                                           Ui.Say(StoryWords.NewMemoryKey), Ui.Say(StoryWords.NewNumberKey));
    }
}
