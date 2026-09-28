using System;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Core.Characters;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class CombatDirector
    {
        // --- the hero's hands -------------------------------------------------------------------------

        void ShowSquares()
        {
            Board.Unflash();

            if (!HerosMove) return;

            if (Session.Selected == null)
            {
                Board.Flash(Session.Reachable().Keys, new Color(0.35f, 0.55f, 0.85f, 0.45f));
                return;
            }

            switch (Session.Selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    Board.Flash(Session.LegalTargets().Select(a => Session.Fight.Field.Where(a))
                                       .Where(c => c.HasValue).Select(c => c.Value),
                                new Color(0.85f, 0.3f, 0.25f, 0.5f));
                    break;

                case Targeting.Square:
                case Targeting.Direction:
                    Preview preview = Session.Preview(Array.Empty<Actor>(), _hover);
                    Board.Flash(preview.Template, new Color(0.9f, 0.55f, 0.2f, 0.5f));
                    break;
            }
        }

        public void Pick(ActionOption option)
        {
            if (!HerosMove || option == null || !option.Enabled) return;

            if (option.Targeting == Targeting.None && option.Kind != OptionKind.Spell && option.Kind != OptionKind.Again)
            {
                Rules.Post(() => Session.Take(option), Refresh);
                return;
            }

            Session.Select(option);

            // a spell that aims at nothing (Shield on yourself, a self-centred aura) goes at once
            if (option.Targeting == Targeting.None)
            {
                Rules.Post(() => Session.Confirm(), Refresh);
                return;
            }

            Refresh();
        }

        public void EndTurn()
        {
            if (!HerosMove) return;

            Rules.Post(() => Session.EndTurn(), Refresh);
        }

        public void Skip()
        {
            if (Presenting) _skipping = true;
        }

        void Cancel()
        {
            Session.Cancel();
            _hud?.Preview(null);
            Refresh();
        }

        void Click(Cell cell)
        {
            ActionOption selected = Session.Selected;

            if (selected == null)
            {
                if (Session.Reachable().ContainsKey(cell)) Rules.Post(() => Session.Move(cell), Refresh);
                return;
            }

            switch (selected.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    {
                        Actor target = Session.LegalTargets().FirstOrDefault(a => Session.Fight.Field.Where(a) == cell);

                        if (target != null) Rules.Post(() => Session.Confirm(target), Refresh);
                        break;
                    }

                case Targeting.Square:
                    if (Session.LegalSquares().Contains(cell)) Rules.Post(() => Session.Confirm(cell), Refresh);
                    break;

                case Targeting.Direction:
                    Rules.Post(() => Session.Confirm(), Refresh);
                    break;
            }
        }

        void Hover(Cell? cell)
        {
            if (cell == _hover) return;

            _hover = cell;

            if (!HerosMove) return;

            ActionOption selected = Session.Selected;

            if (selected == null)
            {
                ShowSquares();

                if (cell.HasValue && Session.Reachable().TryGetValue(cell.Value, out int cost))
                {
                    Board.Flash(Session.PathTo(cell.Value), new Color(0.95f, 0.85f, 0.4f, 0.6f));
                    _hud?.Preview(Ui.Say(ScreenKeys.Key("combat", "path_cost"), cost));
                }
                else _hud?.Preview(null);

                return;
            }

            if (selected.Targeting is Targeting.Creature or Targeting.Creatures)
            {
                Actor target = cell.HasValue
                    ? Session.LegalTargets().FirstOrDefault(a => Session.Fight.Field.Where(a) == cell.Value)
                    : null;

                _hud?.Preview(target == null ? null : Describe(Session.Preview(target)));
                return;
            }

            ShowSquares();
            _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), cell)));
        }

        static string Describe(Preview preview)
        {
            if (preview == null) return null;

            var parts = new List<string>();

            if (preview.HitChance >= 0) parts.Add(Ui.Say(CombatHud.HitChanceKey, Math.Round(preview.HitChance * 100)));
            if (preview.FailChance >= 0) parts.Add(Ui.Say(CombatHud.SaveChanceKey, Math.Round(preview.FailChance * 100)));
            if (preview.Damage.RollsAnything) parts.Add(preview.Damage.ToString());
            parts.Add(Ui.Say(CombatHud.ExpectedDamageKey, preview.ExpectedDamage.ToString("0.0")));

            return string.Join("   ", parts);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Session == null) return;

            if (@event is InputEventMouseMotion motion)
            {
                Hover(Board.CellUnder(motion.Position));
                return;
            }

            if (!HerosMove) return;

            if (@event is InputEventMouseButton { Pressed: true } click)
            {
                if (click.ButtonIndex == MouseButton.Right && Session.Selected != null)
                {
                    Cancel();
                    GetViewport().SetInputAsHandled();
                }
                else if (click.ButtonIndex == MouseButton.Left && Board.CellUnder(click.Position) is Cell cell)
                {
                    Click(cell);
                    GetViewport().SetInputAsHandled();
                }
                else if (Session.Selected?.Targeting == Targeting.Direction &&
                         click.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                {
                    Session.Rotate(click.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                    ShowSquares();
                    _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), null)));
                    GetViewport().SetInputAsHandled();
                }

                return;
            }

            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            if (key.IsActionPressed("ui_cancel") && Session.Selected != null)
            {
                Cancel();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (Session.Selected?.Targeting == Targeting.Direction &&
                (key.IsActionPressed("turn_left") || key.IsActionPressed("turn_right")))
            {
                Session.Rotate(key.IsActionPressed("turn_left") ? -1 : 1);
                ShowSquares();
                _hud?.Preview(Describe(Session.Preview(Array.Empty<Actor>(), null)));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (Session.Selected?.Targeting is Targeting.Direction && key.Keycode == Key.Enter)
            {
                Rules.Post(() => Session.Confirm(), Refresh);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
            {
                Pick(new CombatHud(Session).Hotkey((int)(key.Keycode - Key.Key0)));
                GetViewport().SetInputAsHandled();
            }
        }

        // Space when nothing is waiting on the tray: End Turn on the hero's turn, skip otherwise
        public bool GoOn()
        {
            if (Session == null) return false;

            if (HerosMove)
            {
                EndTurn();
                return true;
            }

            if (Presenting)
            {
                Skip();
                return true;
            }

            return false;
        }
    }
}
