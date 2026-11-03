using System.Collections.Generic;
using Content.Combat;
using Content.Screens;
using Content.Sheet;
using Content.Spells;
using Game.Access;
using Godot;

namespace Game.Screens
{
    public partial class SheetPage
    {
        // --- the back: the spells and the special features --------------------------------------------------
        //
        // The spells (a caster's): what's held, End concentration, the slots or the points, and a table of every spell
        // known, each name opening its card. The special features beside them, each opening its card, and the
        // keepsake. Each table scrolls inside itself; nothing else does
        Control BackSide()
        {
            Control special = Column(2f, Special());

            if (!_sheet.Casts) return Ui.Row(28, special, Column(1f));

            return Ui.Row(28, Column(3f, Spells()), special);
        }

        Control Spells()
        {
            var column = Ui.Column(4, Heading(SheetReading.HeadingKey(SheetReading.Section.Spells, _sheet)));
            column.SizeFlagsVertical = SizeFlags.ExpandFill;

            // the lines above the table: what's held, the slots or points, the solo slot (SheetReading's, less the rows)
            IReadOnlyList<Said> lines = SheetReading.Lines(SheetReading.Section.Spells, _sheet);

            for (int i = 0; i < lines.Count - _sheet.Spells.Count; i++)
            {
                column.AddChild(Line(AccessDesk.Words(lines[i])));

                if (i == 0 && _sheet.CanEndConcentration)
                    column.AddChild(Act(Ui.Say(CombatSession.EndConcentrationKey), EndConcentration,
                                        _act.CanEndConcentration, _act.EndConcentrationWhyNotKey));
            }

            var table = Table(5, SheetWords.ColumnNameKey, SheetWords.ColumnLevelKey, SheetWords.ColumnRollKey,
                              SheetWords.ColumnEffectKey, SheetWords.ColumnConcentrationKey);

            foreach (SpellCard card in _sheet.Spells)
            {
                IReadOnlyList<Said> cells = SheetReading.SpellCells(card);
                SpellCard one = card;

                table.AddChild(Opens(AccessDesk.Words(cells[0]), () => CardOverlay.Show(this, one)));

                for (int i = 1; i < cells.Count; i++) table.AddChild(Cell(AccessDesk.Words(cells[i])));
            }

            column.AddChild(Ui.Scroll(table));
            return column;
        }

        Control Special()
        {
            var column = Ui.Column(4, Heading(SheetReading.HeadingKey(SheetReading.Section.Special, _sheet)));
            column.SizeFlagsVertical = SizeFlags.ExpandFill;

            var table = Table(3, SheetWords.ColumnNameKey, SheetWords.ColumnUseKey, SheetWords.ColumnUsesKey);

            foreach (SpecialRow row in _sheet.Special)
            {
                IReadOnlyList<Said> cells = SheetReading.FeatureCells(row);
                SpecialRow one = row;

                table.AddChild(Opens(AccessDesk.Words(cells[0]), () => CardOverlay.Show(this, one)));

                for (int i = 1; i < cells.Count; i++) table.AddChild(Cell(AccessDesk.Words(cells[i])));
            }

            column.AddChild(Ui.Scroll(table));

            // the keepsake, ready or spent until a long rest (cc_task_h 3.2): SheetReading's last line when there is one
            if (_sheet.Keepsake.HasValue)
            {
                IReadOnlyList<Said> lines = SheetReading.Lines(SheetReading.Section.Special, _sheet);
                column.AddChild(Line(AccessDesk.Words(lines[lines.Count - 1])));
            }

            return column;
        }

        // a table with its column heads
        static GridContainer Table(int columns, params string[] heads)
        {
            var table = new GridContainer { Columns = columns };
            table.AddThemeConstantOverride("h_separation", 18);
            table.AddThemeConstantOverride("v_separation", 2);

            foreach (string head in heads)
            {
                Label label = Ui.Label(head);
                label.ThemeTypeVariation = "CardLabel";
                label.AutowrapMode = TextServer.AutowrapMode.Off;
                label.Modulate = new Color(1, 1, 1, 0.7f);
                table.AddChild(label);
            }

            return table;
        }

        static Label Cell(string words)
        {
            Label cell = Line(words);
            cell.AutowrapMode = TextServer.AutowrapMode.Off;
            cell.VerticalAlignment = VerticalAlignment.Center;
            return cell;
        }
    }
}
