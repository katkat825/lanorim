using System;
using System.Collections.Generic;
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

        public static Label Label(string key, params object[] args) =>
            new Label { Text = Say(key, args), AutowrapMode = TextServer.AutowrapMode.WordSmart };

        // words that are not a key: a hero's own name, a number
        public static Label Plain(string words) =>
            new Label { Text = words ?? "", AutowrapMode = TextServer.AutowrapMode.WordSmart };

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

        // a greyed button says why in its tooltip - never hidden (combat_ux.md)
        public static Button Greyed(this Button button, bool disabled, string whyKey)
        {
            button.Disabled = disabled;
            button.TooltipText = disabled && whyKey != null ? Say(whyKey) : "";
            return button;
        }

        public static VBoxContainer Column(int gap = 8, params Control[] children)
        {
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", gap);

            foreach (Control child in children) if (child != null) column.AddChild(child);

            return column;
        }

        public static HBoxContainer Row(int gap = 8, params Control[] children)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", gap);

            foreach (Control child in children) if (child != null) row.AddChild(child);

            return row;
        }

        // a panel with its content inside, the theme's parchment
        public static PanelContainer Panel(Control content, string variation = null)
        {
            var panel = new PanelContainer();

            if (variation != null) panel.ThemeTypeVariation = variation;

            if (content != null) panel.AddChild(content);

            return panel;
        }

        public static ScrollContainer Scroll(Control content, float minHeight = 0)
        {
            var scroll = new ScrollContainer
            {
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                CustomMinimumSize = new Vector2(0, minHeight),
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

            content.CustomMinimumSize = new Vector2(width, content.CustomMinimumSize.Y);
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
        // reachable the moment it opens
        public static void FocusFirst(Node root)
        {
            Control first = FirstFocusable(root);
            first?.CallDeferred(Control.MethodName.GrabFocus);
        }

        static Control FirstFocusable(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Control { Visible: true } control && control.FocusMode == Control.FocusModeEnum.All &&
                    !(control is BaseButton { Disabled: true }))
                    return control;

                Control deeper = FirstFocusable(child);
                if (deeper != null) return deeper;
            }

            return null;
        }

        public static IEnumerable<T> Each<T>(IEnumerable<T> things) => things ?? Array.Empty<T>();
    }
}
