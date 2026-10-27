using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Content.Screens;
using Content.Workshop;
using Content.Workshop.Tables;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // WHAT EVERY TABLE TAB SHARES (cc_task_n): the campaign's own things to pick from (the game's monsters and items and
    // the campaign's), each thing as a card - its name, a line about it, where it's used, Edit and Delete, and its form
    // under it while open - and Delete asking first, more strongly when something uses it (Part 6, where-used)
    public partial class CampaignScreen
    {
        // the one card open on a tab, "fight:goblin_camp"; kept across a redraw
        string _openCard = "";

        Library _library;

        // a thing's card opened from outside the tab ("fight:goblin_camp"): Go there, the probe, a picture of it
        public void OpenCard(string card, int further = 0)
        {
            // a story card ("card:old_mill"), opened in the story editor
            if (card != null && card.StartsWith("card:"))
            {
                OpenStory(card.Substring(5));
                if (further > 0) Callable.From(() => ScrollBy(further)).CallDeferred();
                return;
            }

            _openCard = card ?? "";
            Redraw();

            // brought into view once the tab has laid out: its top at the top, and `further` pixels on
            string id = _openCard.Contains(':') ? _openCard.Substring(_openCard.IndexOf(':') + 1) : _openCard;
            Callable.From(() => ScrollTo("Card_" + id, further)).CallDeferred();
        }

        async void ScrollBy(int further)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (_tabs.GetCurrentTabControl() is ScrollContainer scroll) scroll.ScrollVertical = further;
        }

        async void ScrollTo(string name, int further)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (_tabs.GetCurrentTabControl() is not ScrollContainer scroll) return;

            Control card = Nodes.Under<Control>(scroll).FirstOrDefault(c => c.Name == name);
            if (card == null) return;

            scroll.ScrollVertical = (int)(card.GlobalPosition.Y - scroll.GlobalPosition.Y + scroll.ScrollVertical) + further;
        }

        // the game's monsters, items and spells and the campaign's own, read when the page opens and again on save
        public Library Things => _library ??= GameState.Content.With(Package.Read(Editor.Folder));

        void ForgetContent() => _library = null;

        // A THING ON A TAB: a card with its name, what it is, where it's used, and its form while open
        Control Card(string kind, string id, string title, string summary, WhereUsed.Use use, Func<bool> delete, Action<VBoxContainer> form)
        {
            string key = kind + ":" + id;
            bool open = _openCard == key;

            Label name = Ui.Plain(title);
            name.ThemeTypeVariation = "TitleLabel";
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var top = Ui.Row(8, name);
            top.Name = "Top_" + id;

            Button edit = Ui.Button(open ? TableWords.DoneKey : TableWords.EditKey, () => { _openCard = open ? "" : key; Redraw(); });
            edit.Name = "Edit_" + id;
            top.AddChild(edit);

            Button remove = Ui.Button(WorkshopWords.DeleteKey, () => AskToDelete(top, use, delete));
            remove.Name = "Delete_" + id;
            top.AddChild(remove);

            var column = Ui.Column(6, top);
            if (summary.Length > 0) column.AddChild(Forms.Note(summary));
            column.AddChild(Used(use));

            if (open)
            {
                var body = Ui.Column(10);
                body.Name = "Form_" + id;
                form(body);
                column.AddChild(body);
            }

            PanelContainer card = Ui.Panel(column);
            card.Name = "Card_" + id;
            return card;
        }

        // "Used in: 3 story cards, 1 fight", or "Not used anywhere yet"
        static Label Used(WhereUsed.Use use)
        {
            if (use == null || !use.Any) return Forms.Hint(TableWords.UsedNowhereKey);

            var parts = new List<string>();
            if (use.Cards.Count > 0) parts.Add(Say(TableWords.Counted(use.Cards.Count, TableWords.CardsOneKey, TableWords.CardsManyKey)));
            if (use.Fights.Count > 0) parts.Add(Say(TableWords.Counted(use.Fights.Count, TableWords.FightsOneKey, TableWords.FightsManyKey)));
            if (use.Tables.Count > 0) parts.Add(Say(TableWords.Counted(use.Tables.Count, TableWords.TablesOneKey, TableWords.TablesManyKey)));

            Label used = Forms.Hint(TableWords.UsedInKey, string.Join(", ", parts));
            used.Name = "Used";
            return used;
        }

        static string Say(Said said) => Ui.Say(said.Key, said.Args);

        // DELETE, asked on the card itself; said more strongly when something uses it
        void AskToDelete(HBoxContainer top, WhereUsed.Use use, Func<bool> delete)
        {
            Ui.Clear(top);

            Label question = Ui.Label(use?.Any == true ? TableWords.DeleteUsedKey : TableWords.DeleteThingKey);
            question.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Button keep = Ui.Button(WorkshopWords.KeepKey, Redraw);

            top.AddChild(question);
            Button yes = Ui.Button(WorkshopWords.DeleteKey, () => { _openCard = ""; Change(delete); });
            yes.Name = "ReallyDelete";
            top.AddChild(yes);
            top.AddChild(keep);
            Ui.FocusLater(keep);
        }

        // a box to name a new thing and the button that makes it (and opens its card)
        HBoxContainer Maker(string name, string placeholderKey, string buttonKey, Func<string, string> make, string kind)
        {
            LineEdit box = Forms.Line(name + "Name", "", _ => { }, placeholderKey);
            box.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            void Make()
            {
                if (string.IsNullOrWhiteSpace(box.Text)) return;

                string id = make(box.Text.Trim());
                GD.Print($"workshop {kind} {id} added to {Editor.Id}");
                _openCard = kind + ":" + id;
                Redraw();
            }

            box.TextSubmitted += _ => Make();
            Button add = Ui.Button(buttonKey, Make);
            add.Name = name;
            return Ui.Row(8, box, add);
        }

        // a thing's name, typed: kept in workshop.json (TablesDraft.Names); its id stays what the first name made
        Control NameField(string kind, string id)
        {
            LineEdit name = Forms.Line("ThingName_" + id, Draft.Tables.NameOf(kind, id), t => Edit($"{kind}.{id}.name", d => d.Tables.Name(kind, id, t), true));
            return Forms.Field(TableWords.NameKey, name, TableWords.NameHintKey, _advanced ? Forms.Note(id) : null);
        }

        // the JSON a table tab writes, under Advanced
        void TableJson(VBoxContainer page, IEnumerable<System.Text.Json.Nodes.JsonObject> things) =>
            Json(page, string.Join("\n", things.Select(t => t.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }))));

        // "This campaign's files have 2 settings the Workshop doesn't edit": the keys kept as they were (cc_task_n)
        void Unread(VBoxContainer page)
        {
            if (Draft.Unread.Count == 0) return;

            Label kept = Forms.Hint(TableWords.KeptKeysKey, Draft.Unread.Count);
            kept.Name = "KeptKeys";
            page.AddChild(kept);

            if (_advanced) page.AddChild(Ui.Plain(string.Join("\n", Draft.Unread)));
        }
    }
}
