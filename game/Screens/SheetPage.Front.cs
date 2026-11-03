using Content.Combat;
using Content.Items;
using Content.Screens;
using Content.Sheet;
using Game.Access;
using Godot;

namespace Game.Screens
{
    public partial class SheetPage
    {
        // --- the front: who, the basics, the abilities, the skills, the turn and the weapons -------------------
        //
        // Four columns, as a printed sheet has them: who and the basics; the six abilities in boxes with their saves;
        // the eighteen skills and Passive Perception; the turn and the weapons, which scroll inside themselves when a
        // pack holds many to swap to
        Control FrontSide()
        {
            Control who = Column(1f,
                Section(SheetReading.Section.Identity),
                Section(SheetReading.Section.Basics));

            Control abilities = Column(1f, Abilities());
            Control skills = Column(1f, Section(SheetReading.Section.Skills));

            ScrollContainer weapons = Ui.Scroll(Weapons());
            Control turn = Column(1.1f, Section(SheetReading.Section.Turn), weapons);

            return Ui.Row(28, who, abilities, skills, turn);
        }

        // the six scores, two to a row: the name, the score large, the modifier, and the save under it
        Control Abilities()
        {
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 12);

            foreach (AbilityRow row in _sheet.Abilities)
            {
                Label name = Ui.Label(row.NameKey);
                name.ThemeTypeVariation = "CardLabel";
                name.AutowrapMode = TextServer.AutowrapMode.Off;
                name.HorizontalAlignment = HorizontalAlignment.Center;

                Label score = Ui.Plain(row.Score.ToString());
                score.ThemeTypeVariation = "TitleLabel";
                score.HorizontalAlignment = HorizontalAlignment.Center;

                Label modifier = Ui.Plain(row.Modifier.ToString("+0;-0"));
                modifier.ThemeTypeVariation = "CardTitle";
                modifier.HorizontalAlignment = HorizontalAlignment.Center;

                // the save in words, so it carries its label and whether it's proficient
                Label save = Line(AccessDesk.Words(SheetReading.BoxSave(row)));
                save.HorizontalAlignment = HorizontalAlignment.Center;

                PanelContainer box = Box(Ui.Column(0, name, score, modifier, save));
                box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                grid.AddChild(box);
            }

            return Ui.Column(2, Heading(SheetReading.HeadingKey(SheetReading.Section.Abilities, _sheet)), grid);
        }

        // an ability's box: an outline the colour of the ink. The one style the sheet draws itself
        static PanelContainer Box(Control content)
        {
            var outline = new StyleBoxFlat
            {
                DrawCenter = false,
                BorderColor = new Color(0.2f, 0.13f, 0.08f, 0.6f),
                ContentMarginLeft = 6,
                ContentMarginRight = 6,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            };
            outline.SetBorderWidthAll(2);
            outline.SetCornerRadiusAll(6);

            var box = new PanelContainer();
            box.AddThemeStyleboxOverride("panel", outline);
            box.AddChild(content);
            return box;
        }

        // each weapon in hand as a line; then what Swap costs, and a Swap for each weapon in the pack
        Control Weapons()
        {
            Control section = Section(SheetReading.Section.Weapons);

            foreach (Item spare in _sheet.SpareWeapons)
            {
                string id = spare.Id;
                section.AddChild(Act(Ui.Say(CombatSession.SwapKey, Ui.Say(spare.NameKey)), () => Swap(id),
                                     _act.CanSwap(id), _act.SwapWhyNotKey(id)));
            }

            return section;
        }
    }
}
