using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Tables;
using Core.Words;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE RANDOM ENCOUNTERS TAB (cc_task_n Part 2): the tables the story rolls on with Random encounter. Each: its name,
    // how often something happens (the trigger, in words), whether the player hears the roll, its entries - a fight
    // (the Fights tab's form, inline), a moment, or nothing - each with how likely, and Roll it
    public partial class CampaignScreen
    {
        // the last Roll it's results, by table, shown until the next redraw that throws them away
        readonly Dictionary<string, IReadOnlyList<(string Entry, int Count)>> _rolled = new();

        void Encounters(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(TableWords.RandomHintKey));

            WhereUsed uses = Editor.Uses;
            var tables = Draft.Tables.RandomTables(id => uses.OfEncounterTable(id).Cards.Count > 0).ToList();

            if (tables.Count == 0) page.AddChild(Forms.Hint(TableWords.NoTablesKey));

            foreach (EncounterTableDraft table in tables)
            {
                string id = table.Id;
                string summary = Ui.Say(TableWords.Often(Oftenness.Of(table.Trigger))) + ", " +
                                 string.Join(", ", table.Entries.Select(e => Draft.Tables.NameOf(e.IsFight ? "fight" : "entry", e.Id)));

                page.AddChild(Card("encounters", id, Draft.Tables.NameOf("encounters", id), summary, uses.OfEncounterTable(id),
                                   () => Editor.DeleteRandomTable(id), body => RandomTable(body, id)));
            }

            page.AddChild(Maker("NewRandomTable", TableWords.NewTableKey, TableWords.NewTableKey, Editor.NewRandomTable, "encounters"));

            TableJson(page, tables.Select(t => t.ToJson()));
        }

        void RandomTable(VBoxContainer form, string id)
        {
            EncounterTableDraft table = Draft.Tables.Table(id);
            if (table == null) return;

            form.AddChild(NameField("encounters", id));

            // how often
            HowOften how = Oftenness.Of(table.Trigger);
            var often = Enum.GetValues<HowOften>().Where(h => h != HowOften.Other || how == HowOften.Other || _advanced).ToList();
            OptionButton choice = Forms.Choice("HowOften_" + id, often, how, TableWords.Often,
                h => Change(() => Editor.Edit("encounters", d => d.Tables.Table(id).Trigger = Oftenness.Trigger(h, d.Tables.Table(id).Trigger))));
            choice.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var oftenRow = Ui.Row(8, choice);

            if (table.Trigger != null && (how == HowOften.Other || _advanced))
            {
                oftenRow.AddChild(Forms.Line("TriggerRoll_" + id, table.Trigger.Roll, t =>
                {
                    if (Pickers.IsDice(t)) Edit("encounters.trigger", d => d.Tables.Table(id).Trigger.Roll = t.Trim(), true);
                }));
                oftenRow.AddChild(Ui.Label(TableWords.AtLeastKey));
                oftenRow.AddChild(Forms.Number("TriggerAtLeast_" + id, 1, 100, table.Trigger.AtLeast, v => Edit("encounters.trigger", d => d.Tables.Table(id).Trigger.AtLeast = v, true)));
            }

            form.AddChild(Forms.Field(TableWords.HowOftenKey, oftenRow, null));

            // heard
            EnumWords.TryParse(table.Rolled.Length == 0 ? "hidden" : table.Rolled, out Visibility rolled);
            form.AddChild(Forms.Field(TableWords.HeardKey,
                Forms.Choice("Rolled_" + id, new[] { Visibility.Hidden, Visibility.Quiet, Visibility.Shown }, rolled, TableWords.Rolled,
                             v => Edit("encounters", d => d.Tables.Table(id).Rolled = v == Visibility.Hidden ? "" : EnumWords.Id(v))), null));

            // the entries
            form.AddChild(Ui.Label(TableWords.EntriesKey));
            int total = table.Entries.Sum(e => Math.Max(1, e.Weight));

            foreach (EncounterDraft entry in table.Entries.ToList()) form.AddChild(Entry(id, entry, total));

            var adds = Ui.Row(8,
                Ui.Button(TableWords.AddFightEntryKey, () => Change(() => Editor.AddEntry(id, EntryKind.Fight) != null)),
                Ui.Button(TableWords.AddMomentEntryKey, () => Change(() => Editor.AddEntry(id, EntryKind.Line) != null)),
                Ui.Button(TableWords.AddNothingEntryKey, () => Change(() => Editor.AddEntry(id, EntryKind.Nothing) != null)));
            adds.Name = "AddEntries_" + id;
            form.AddChild(adds);

            // roll it
            Button roll = Ui.Button(TableWords.RollItKey, () =>
            {
                _rolled[id] = TableSamples.Encounters(Draft.Tables.Table(id), (int)Time.GetTicksMsec());
                Redraw();
            });
            roll.Name = "RollIt_" + id;
            form.AddChild(roll);

            if (_rolled.TryGetValue(id, out var results) && results != null)
            {
                var shown = Ui.Column(2, Ui.Label(TableWords.RolledKey));
                shown.Name = "Rolled_" + id;

                foreach ((string entry, int count) in results)
                    shown.AddChild(Ui.Label(TableWords.ResultKey,
                        entry.Length == 0 ? Ui.Say(TableWords.NothingHappenedKey) : Draft.Tables.NameOf(Draft.Tables.Fight(entry).Fight != null ? "fight" : "entry", entry), count));

                form.AddChild(shown);
            }
        }

        // ONE ENTRY: what it is, how likely, and its own form - a fight's, inline, or a moment's line
        Control Entry(string table, EncounterDraft entry, int total)
        {
            string id = entry.Id;

            Label kind = Ui.Label(TableWords.Entry(entry.Kind));
            kind.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var top = Ui.Row(8, kind,
                Pickers.Likely("Likely_" + id, Math.Max(1, entry.Weight), total, v => Edit("encounters.weight", d => d.Tables.Table(table).Entry(id).Weight = v, true)),
                Forms.Removal("RemoveEntry_" + id, () => Change(() => Editor.RemoveEntry(table, id))));

            var column = Ui.Column(6, top);
            column.Name = "Entry_" + id;

            switch (entry.Kind)
            {
                case EntryKind.Fight:
                    var inline = Ui.Column(8);
                    Fight(inline, id, named: true);
                    column.AddChild(inline);
                    break;

                case EntryKind.Line:
                    column.AddChild(Forms.Field(TableWords.LineKey,
                        Forms.Line("EntryLine_" + id, entry.Line, t => Edit("encounters.line", d => d.Tables.Table(table).Entry(id).Line = t, true)), null));
                    break;
            }

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", (int)Ui.Px(24));
            margin.AddChild(column);
            return margin;
        }
    }
}
