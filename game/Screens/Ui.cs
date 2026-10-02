using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Game.Localization;
using Godot;

namespace Game.Screens
{
    // THE SCREENS' BUILDING BLOCKS. Every screen is made in code from these, with containers doing
    // the layout and the theme (ui/lanorim_theme.tres) doing the look - so restyling is the theme's
    // job and not a hunt through positions. Every word is a key; nothing here spells English.
    public static class Ui
    {
        public static readonly ILocalizer Text = new GodotLocalizer();

        public static string Say(string key, params object[] args) =>
            key == null ? "" : args == null || args.Length == 0 ? Text.Get(key) : Text.Format(key, args);

        // an amount of money, in copper, said as coins: "1 gp 5 sp" (PackView.Money, cc_task_f 1.5)
        public static string Money(int copper) =>
            string.Join(" ", Content.Screens.PackView.Money(copper).Select(c => Say(c.Key, c.Count)));

        public static Label Label(string key, params object[] args) =>
            new Label { Text = Say(key, args), AutowrapMode = TextServer.AutowrapMode.WordSmart };

        // words that are not a key: a hero's own name, a number
        public static Label Plain(string words) =>
            new Label { Text = words ?? "", AutowrapMode = TextServer.AutowrapMode.WordSmart };

        // an empty label in one of the theme's variations, centred, for words set later: the HUD's status line,
        // pips and preview
        public static Label Centred(string variation) =>
            new Label { ThemeTypeVariation = variation, HorizontalAlignment = HorizontalAlignment.Center };

        public static Label Title(string key, params object[] args)
        {
            Label label = Label(key, args);
            label.ThemeTypeVariation = "TitleLabel";
            return label;
        }

        public static Button Button(string key, Action pressed, params object[] args) =>
            Button(Say(key, args), pressed, true);

        public static Button Button(string words, Action pressed, bool plain)
        {
            var button = new Button { Text = words ?? "", FocusMode = Control.FocusModeEnum.All };

            if (pressed != null) button.Pressed += pressed;

            return button;
        }

        // a greyed button says why in its tooltip (combat_ux.md; the combat bar may hide it instead, HudLayout)
        // NO HOVER TEXT (cc_task_ui-issues-9-30.md 3.1): a greyed button used to carry its reason as a
        // tooltip. Where the reason matters, the screen writes it beside the button (Why)
        public static Button Greyed(this Button button, bool disabled, string whyKey)
        {
            button.Disabled = disabled;
            return button;
        }

        // the reason a button is greyed, written out under it; nothing when it isn't
        public static Label Why(bool disabled, string whyKey)
        {
            Label why = Label(whyKey);
            why.Visible = disabled && whyKey != null;
            return why;
        }

        public static VBoxContainer Column(int gap = 8, params Control[] children)
        {
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", gap);

            foreach (Control child in children) if (child != null) column.AddChild(child);

            return column;
        }

        // A LABEL IN A ROW DOES NOT WRAP unless it is given the row's room. A wrapping label has no
        // width of its own, so a row squeezed it to nothing and it wrapped a letter to a line -
        // creation's ability names read S / t / r / e... (cc_ui_issues_9-25-2026.md). One set to
        // expand keeps its wrap and takes the space left over
        public static HBoxContainer Row(int gap = 8, params Control[] children)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", gap);

            foreach (Control child in children)
            {
                if (child == null) continue;

                if (child is Label label && !label.SizeFlagsHorizontal.HasFlag(Control.SizeFlags.Expand))
                    label.AutowrapMode = TextServer.AutowrapMode.Off;

                row.AddChild(child);
            }

            return row;
        }

        // A ROW THAT WRAPS (cc_task_ui-issues-9-30.md 2): the action bar and the turn strip. A flow
        // container on its own wraps at its widest child, so Fit gives it the width it would take on one
        // line, or at most `most`, and it wraps past that
        public static HFlowContainer Flow(int gap = 8)
        {
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", gap);
            flow.AddThemeConstantOverride("v_separation", gap);
            return flow;
        }

        public static void Fit(HFlowContainer flow, float most)
        {
            var shown = flow.GetChildren().OfType<Control>().Where(c => c.Visible).ToList();
            float gap = flow.GetThemeConstant("h_separation");
            float line = shown.Sum(c => c.GetCombinedMinimumSize().X) + gap * System.Math.Max(0, shown.Count - 1);
            float widest = shown.Count == 0 ? 0 : shown.Max(c => c.GetCombinedMinimumSize().X);

            flow.CustomMinimumSize = new Vector2(Mathf.Max(widest, Mathf.Min(line, most)), 0);
        }

        // a panel with its content inside, the theme's parchment
        public static PanelContainer Panel(Control content, string variation = null)
        {
            var panel = new PanelContainer();

            if (variation != null) panel.ThemeTypeVariation = variation;

            if (content != null) panel.AddChild(content);

            return panel;
        }

        // a size written for the old 1152-wide canvas, at today's (HudLayout.MenuScale)
        public static float Px(float old) => old * HudLayout.Current.MenuScale;

        public static ScrollContainer Scroll(Control content, float minHeight = 0)
        {
            var scroll = new ScrollContainer
            {
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                CustomMinimumSize = new Vector2(0, Px(minHeight)),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };

            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(content);
            return scroll;
        }

        // a centred box on the screen, at most this wide
        public static CenterContainer Centred(Control content, float width = 640)
        {
            var centre = new CenterContainer();
            centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            content.CustomMinimumSize = new Vector2(Px(width), content.CustomMinimumSize.Y);
            centre.AddChild(content);
            return centre;
        }

        public static void Clear(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                node.RemoveChild(child);
                child.QueueFree();
            }
        }

        // the first focusable control under a node gets the focus, so every screen is keyboard
        // reachable the moment it opens. Deferred, and only if it is still on screen by then: a bar
        // redrawn twice in a frame freed the button first, and Godot said "!is_inside_tree()"
        public static void FocusFirst(Node root)
        {
            FocusLater(FirstFocusable(root));
        }

        public static void FocusLater(Control control)
        {
            if (control == null) return;

            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(control) && control.IsInsideTree()) control.GrabFocus();
            }).CallDeferred();
        }

        static Control FirstFocusable(Node node) =>
            Nodes.Under<Control>(node).FirstOrDefault(c => c.Visible && c.FocusMode == Control.FocusModeEnum.All &&
                                                           !(c is BaseButton { Disabled: true }));

        public static IEnumerable<T> Each<T>(IEnumerable<T> things) => things ?? Array.Empty<T>();
    }
}
