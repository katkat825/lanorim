using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Workshop;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // a change to the draft a form is over: the wizard's (straight on) or a campaign's (an undo step, CampaignEditor.Edit)
    public delegate void DraftEdit(string what, Action<CampaignDraft> change, bool typing = false);

    // THE WORKSHOP'S FORM FIELDS (cc_task_m 3.1, 3.2, 4.1): each a label, the control, and a one-line hint under it, made
    // from the Ui helpers and the theme (WORKSHOP_DESIGN.md principle 8: plain screens, the look is Kathleen's). The
    // wizard's first two pages and the campaign page's About tab are the same fields over a CampaignDraft, so they're
    // written once here
    public static class Forms
    {
        // a field: its name, the control, and what it's for
        public static VBoxContainer Field(string labelKey, Control control, string hintKey)
        {
            control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            return Ui.Column(2, Ui.Label(labelKey), control, hintKey == null ? null : Hint(hintKey));
        }

        public static Label Hint(string key, params object[] args)
        {
            Label hint = Ui.Label(key, args);
            hint.Modulate = new Color(1f, 1f, 1f, 0.7f);
            return hint;
        }

        public static LineEdit Line(string name, string text, Action<string> changed, string placeholderKey = null)
        {
            var line = new LineEdit { Name = name, Text = text ?? "", PlaceholderText = placeholderKey == null ? "" : Ui.Say(placeholderKey) };
            line.TextChanged += t => changed(t);
            return line;
        }

        public static TextEdit Paragraph(string name, string text, Action<string> changed)
        {
            var box = new TextEdit
            {
                Name = name,
                Text = text ?? "",
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                CustomMinimumSize = new Vector2(0, Ui.Px(80)),
            };
            box.TextChanged += () => changed(box.Text);
            return box;
        }

        public static OptionButton Choice<T>(string name, IReadOnlyList<T> values, T now, Func<T, string> key, Action<T> picked)
        {
            var list = new OptionButton { Name = name, ClipText = true, FitToLongestItem = false };

            for (int i = 0; i < values.Count; i++)
            {
                list.AddItem(Ui.Say(key(values[i])), i);
                if (EqualityComparer<T>.Default.Equals(values[i], now)) list.Selected = i;
            }

            list.ItemSelected += i => picked(values[(int)i]);
            return list;
        }

        public static CheckBox Tick(string name, string key, bool on, Action<bool> changed)
        {
            var tick = new CheckBox { Name = name, Text = Ui.Say(key), ButtonPressed = on };
            tick.Toggled += now => changed(now);
            return tick;
        }

        public static SpinBox Number(string name, int min, int max, int value, Action<int> changed)
        {
            var number = new SpinBox { Name = name, MinValue = min, MaxValue = max, Value = value, Rounded = true };
            number.ValueChanged += v => changed((int)v);
            return number;
        }

        // --- the wizard's first page, and the About tab -------------------------------------------------------------

        // the name, the blurb, the author, the tags and the mature tick box. `id` is what Advanced shows under them:
        // the wizard's, which can be typed over (typed), or a campaign's, which can't (typed null)
        public static void About(VBoxContainer page, CampaignDraft draft, DraftEdit edit, bool advanced, string id, Action<string> typed)
        {
            page.AddChild(Field(WorkshopWords.NameKey, Line("Name", draft.Name, t => edit("about.name", d => d.Name = t, true)), WorkshopWords.NameHintKey));
            page.AddChild(Field(WorkshopWords.DescriptionKey, Paragraph("Description", draft.Description, t => edit("about.description", d => d.Description = t, true)),
                                WorkshopWords.DescriptionHintKey));
            page.AddChild(Field(WorkshopWords.AuthorKey, Line("Author", draft.Author, t => edit("about.author", d => d.Author = t, true)), WorkshopWords.AuthorHintKey));
            page.AddChild(Tags(draft, edit));
            page.AddChild(Ui.Column(2,
                Tick("Mature", WorkshopWords.MatureKey, draft.Mature, on => edit("about.mature", d => d.Mature = on)),
                Hint(WorkshopWords.MatureHintKey)));

            if (!advanced) return;

            Control shown = typed == null ? Ui.Plain(id) : Line("Id", id, t => typed(t));
            page.AddChild(Field(WorkshopWords.IdKey, shown, WorkshopWords.IdHintKey));
        }

        // the short list as tick boxes, any of the author's own beside them, and a box to type another
        static VBoxContainer Tags(CampaignDraft draft, DraftEdit edit)
        {
            HFlowContainer ticks = Ui.Flow(12);
            ticks.Name = "Tags";

            IEnumerable<string> shown = CampaignWizard.Tags.Concat(draft.Tags.Where(t => t != CampaignDraft.MatureTag && !CampaignWizard.Tags.Contains(t)));

            foreach (string tag in shown.ToList())
            {
                string one = tag;
                var tick = new CheckBox
                {
                    Name = "Tag_" + one,
                    Text = CampaignWizard.Tags.Contains(one) ? Ui.Say(CampaignWizard.TagKey(one)) : one.Replace('_', ' '),
                    ButtonPressed = draft.Tags.Contains(one),
                };
                tick.Toggled += on => edit("about.tags", d => { d.Tags.Remove(one); if (on) d.Tags.Add(one); });
                ticks.AddChild(tick);
            }

            LineEdit own = Line("OwnTag", "", _ => { }, WorkshopWords.OwnTagKey);
            own.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            void Add()
            {
                string tag = CampaignWizard.TagFrom(own.Text);
                if (tag == null || tag == CampaignDraft.MatureTag || draft.Tags.Contains(tag)) return;

                edit("about.tags", d => d.Tags.Add(tag));

                var tick = new CheckBox { Name = "Tag_" + tag, Text = CampaignWizard.Tags.Contains(tag) ? Ui.Say(CampaignWizard.TagKey(tag)) : tag.Replace('_', ' '), ButtonPressed = true };
                tick.Toggled += on => edit("about.tags", d => { d.Tags.Remove(tag); if (on) d.Tags.Add(tag); });
                ticks.AddChild(tick);
                own.Text = "";
            }

            own.TextSubmitted += _ => Add();

            Button add = Ui.Button(WorkshopWords.AddTagKey, Add);
            add.Name = "AddTag";

            return Ui.Column(2, Ui.Label(WorkshopWords.TagsKey), ticks, Ui.Row(8, own, add), Hint(WorkshopWords.TagsHintKey));
        }

        // --- the wizard's second page, and the rest of the About tab: how it plays ------------------------------------

        public static void Rules(VBoxContainer page, CampaignDraft draft, DraftEdit edit)
        {
            page.AddChild(Field(WorkshopWords.DefeatKey,
                Choice("Defeat", WorkshopWords.Defeats, draft.Defeat, WorkshopWords.DefeatChoiceKey, v => edit("rules", d => d.Defeat = v)),
                WorkshopWords.DefeatHintKey));
            page.AddChild(Field(WorkshopWords.LevelingKey,
                Choice("Leveling", Enum.GetValues<Leveling>(), draft.Leveling, WorkshopWords.LevelingChoiceKey, v => edit("rules", d => d.Leveling = v)),
                WorkshopWords.LevelingHintKey));
            page.AddChild(Field(WorkshopWords.CharactersKey,
                Number("Characters", 1, CampaignRules.MostCharacters, draft.Characters, v => edit("rules.characters", d => d.Characters = v, true)),
                WorkshopWords.CharactersHintKey));
            page.AddChild(Ui.Column(2,
                Tick("StartingShop", WorkshopWords.StartingShopKey, draft.StartingShop, on => edit("rules", d => d.StartingShop = on)),
                Hint(WorkshopWords.StartingShopHintKey)));
            page.AddChild(Field(WorkshopWords.ConsequencesKey,
                Choice("Consequences", Enum.GetValues<Layering>(), draft.Consequences, WorkshopWords.ConsequencesChoiceKey, v => edit("rules", d => d.Consequences = v)),
                WorkshopWords.ConsequencesHintKey));
            page.AddChild(Ui.Column(2, Ui.Label(WorkshopWords.GmScreenKey), GmScreens(draft.GmScreen, s => edit("rules", d => d.GmScreen = s)),
                                    Hint(WorkshopWords.GmScreenHintKey)));
        }

        // THE FIVE GM SCREENS, each with its picture; the one picked is pressed
        static HFlowContainer GmScreens(string now, Action<string> picked)
        {
            HFlowContainer row = Ui.Flow(10);
            row.Name = "GmScreens";

            var group = new ButtonGroup();

            foreach (string skin in Manifest.GmScreens)
            {
                string one = skin;
                var choice = new Button
                {
                    Name = "Screen_" + one.Replace('-', '_'),
                    ToggleMode = true,
                    ButtonGroup = group,
                    ButtonPressed = one == now,
                    FocusMode = Control.FocusModeEnum.All,
                };

                var inside = Ui.Column(4, Pictures.CoverOf(null, one, new Vector2(Ui.Px(120), Ui.Px(68))), Ui.Label(WorkshopWords.ScreenNameKey(one)));
                inside.MouseFilter = Control.MouseFilterEnum.Ignore;
                foreach (Control c in Nodes.Under<Control>(inside)) c.MouseFilter = Control.MouseFilterEnum.Ignore;

                var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 6);
                margin.AddChild(inside);

                choice.AddChild(margin);
                choice.CustomMinimumSize = new Vector2(Ui.Px(132), Ui.Px(100));
                choice.Pressed += () => picked(one);
                row.AddChild(choice);
            }

            return row;
        }
    }
}
