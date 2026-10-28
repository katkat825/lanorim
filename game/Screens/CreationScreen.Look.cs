using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Sheet;
using Core.Localization;
using Game.Board;
using Godot;

namespace Game.Screens
{
    // YOUR MINI, creation's last page (Kathleen's UI notes, 2026-10-27): the mini turning, the suggestion (the class's own
    // figures) first and then every class's, a skin tone and a hair colour (FigureSkins), each with "its own" to put the
    // figure's back. A Beastfolk stands as its kind's mini, so its page says so and offers nothing to change
    public partial class CreationScreen
    {
        // the mini as everything picked so far makes it: class, figure, species, lineage and the look
        MiniLook LookNow() =>
            _making.Class == null
                ? null
                : HeroMinis.LookOf(_making.Class.Id, _making.Figure, _making.Species?.Id, _making.Lineage?.Id, _making.Look);

        void LookPage()
        {
            if (HeroMinis.OwnKind(_making.Lineage?.Id))
            {
                _body.AddChild(Ui.Label(ScreenWords.LookKindKey));
                return;
            }

            // nothing to dress before there's a class (a page opened out of order)
            if (_making.Class == null) return;

            // the bodies: the suggestion first, then every other class's two figures
            _body.AddChild(Ui.Label(ScreenWords.LookBodyKey));

            var bodies = new List<string> { _making.Class.Id };
            bodies.AddRange(HeroMinis.Classes.Where(c => c != _making.Class.Id));

            HFlowContainer row = Ui.Flow(8);
            row.Name = "Bodies";

            foreach (string body in bodies)
                for (int figure = 0; figure < HeroMinis.Figures; figure++)
                    row.AddChild(BodyCard(body, figure));

            _body.AddChild(row);

            _body.AddChild(Ui.Label(ScreenWords.LookSkinKey));
            _body.AddChild(Swatches("Skin", FigureSkins.Tones, () => _making.Look.Skin, at => _making.Look = _making.Look with { Skin = at }));

            _body.AddChild(Ui.Label(ScreenWords.LookHairKey));
            _body.AddChild(Swatches("Hair", FigureSkins.Hair, () => _making.Look.Hair, at => _making.Look = _making.Look with { Hair = at }));
        }

        // one figure of one class, the mini itself on the button; the suggestion is the hero's own class
        Button BodyCard(string body, int figure)
        {
            string own = _making.Class.Id;
            var size = new Vector2I((int)Ui.Px(64), (int)Ui.Px(80));

            var card = new Button
            {
                Name = $"Body_{body}_{figure + 1}",
                ToggleMode = true,
                CustomMinimumSize = size + new Vector2I(12, 12),
                AccessibilityName = $"{Ui.Say(KeyConventions.ClassName(body))} {Ui.Say(ScreenWords.FigureKey(figure))}",
            };

            var mini = new MiniPreview(size) { Turning = false, Position = new Vector2(6, 6) };
            card.AddChild(mini);

            card.Pressed += () =>
            {
                _making.Figure = figure;
                _making.Look = _making.Look with { Body = body == own ? "" : body };
                Update();
            };

            _updates.Add(() =>
            {
                string chosen = _making.Look.Body.Length > 0 ? _making.Look.Body : own;
                card.SetPressedNoSignal(chosen == body && _making.Figure == figure);
                mini.Show(HeroMinis.LookOf(body, figure, _making.Species?.Id, null, _making.Look with { Body = "" }));
            });

            return card;
        }

        // "Its own" and a swatch for each colour; the pick pressed
        HFlowContainer Swatches(string name, Color[] colours, Func<int> now, Action<int> pick)
        {
            HFlowContainer row = Ui.Flow(6);
            row.Name = name + "s";
            var group = new ButtonGroup();

            Button own = Ui.Button(ScreenWords.LookOwnKey, () => { pick(-1); Update(); });
            own.Name = name + "_Own";
            own.ToggleMode = true;
            own.ButtonGroup = group;
            row.AddChild(own);
            _updates.Add(() => own.SetPressedNoSignal(now() < 0));

            for (int i = 0; i < colours.Length; i++)
            {
                int at = i;
                var swatch = new Button
                {
                    Name = $"{name}_{i}",
                    ToggleMode = true,
                    ButtonGroup = group,
                    CustomMinimumSize = new Vector2(Ui.Px(40), Ui.Px(40)),
                    AccessibilityName = $"{Ui.Say(name == "Skin" ? ScreenWords.LookSkinKey : ScreenWords.LookHairKey)} {i + 1}",
                };

                var fill = new ColorRect { Color = colours[i], MouseFilter = Control.MouseFilterEnum.Ignore };
                fill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 5);
                swatch.AddChild(fill);

                swatch.Pressed += () => { pick(at); Update(); };
                _updates.Add(() => swatch.SetPressedNoSignal(now() == at));
                row.AddChild(swatch);
            }

            return row;
        }
    }
}
