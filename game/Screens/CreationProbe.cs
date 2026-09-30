using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // DOES CREATION KEEP ITS PLACE, AND SAY WHAT A CHOICE IS WITHOUT HOVER TEXT? (cc_task_ui-issues-9-30.md 3)
    //
    //   godot --headless --path game res://launch.tscn -- --creation
    //
    // A mage's pages, driven the player's way (a press on a button, a toggle of a box):
    // - no control on any page has hover text;
    // - the class page's description area says what the chosen class is;
    // - cantrips and spells are two pages, each with "n of m chosen";
    // - a spell's card text (its school, its range, what it does) is under the list;
    // - learning a spell far down the list leaves the list scrolled where it was.
    // Says "creation ok" or what is wrong.
    public partial class CreationProbe : Node
    {
        public const string Flag = "--creation";

        readonly List<string> _problems = new List<string>();

        public override async void _Ready()
        {
            GameState.SaveProbesApart();

            var window = new Control { Size = new Vector2(1920, 1080) };
            AddChild(window);

            var creation = new CreationScreen(GameState.Content) { CustomMinimumSize = new Vector2(1000, 900), Size = new Vector2(1000, 900) };
            window.AddChild(creation);
            await this.Frames(2);

            // the class page: pick the mage and read about it under the list
            Button mage = Buttons(creation).FirstOrDefault(b => b.Text == Ui.Say(Core.Localization.KeyConventions.ClassName("mage")));
            if (mage == null) Fail("no Mage button on the class page");
            else mage.EmitSignal(BaseButton.SignalName.Pressed);
            await this.Frames(1);

            Expect(About(creation).Contains(Ui.Say(Core.Localization.KeyConventions.ClassDescription("mage"))),
                   "the class page's description area doesn't describe the mage");
            NoHoverText(creation, "class");

            // straight on to the cantrips (the defaults for the rest)
            creation.Making.Pick(GameState.Content.Kind("human"));
            creation.Making.Pick(GameState.Content.Background("soldier"));
            foreach (var skill in creation.Making.SkillChoices.Take(creation.Making.SkillPicksLeft).ToList()) creation.Making.Train(skill);

            creation.Open(Step.Cantrips);
            await this.Frames(2);
            Expect(creation.Showing == Step.Cantrips, "no cantrips page");
            Expect(Labels(creation).Any(l => l.Text == Ui.Say(ScreenWords.ChosenOf, 0, creation.Making.CantripPicks)),
                   "the cantrips page doesn't say 0 of n chosen");
            Expect(Boxes(creation).Count == creation.Making.SpellChoices.Count(s => s.IsCantrip), "the cantrips page lists something else");
            NoHoverText(creation, "cantrips");

            creation.Open(Step.Spells);
            await this.Frames(2);
            Expect(Labels(creation).Any(l => l.Text == Ui.Say(ScreenWords.ChosenOf, 0, creation.Making.SpellPicks)),
                   "the spells page doesn't say 0 of n chosen");
            Expect(Boxes(creation).Count == creation.Making.SpellChoices.Count(s => !s.IsCantrip),
                   "the spells page lists cantrips");
            NoHoverText(creation, "spells");

            string about = About(creation);
            GD.Print($"creation  the first spell reads: {about.Replace("\n", " / ")}");
            Expect(about.Contains("·") && about.Split('\n').Length >= 3, "the spell's card text isn't under the list");

            // scroll to the bottom, learn the last spell, and the list stays where it was
            ScrollContainer scroll = Nodes.Under<ScrollContainer>(creation).First();
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            await this.Frames(1);
            int before = scroll.ScrollVertical;

            CheckBox last = Boxes(creation).Last();
            last.ButtonPressed = true;
            await this.Frames(2);

            GD.Print($"creation  scrolled {before} before learning the last spell, {scroll.ScrollVertical} after; learned {creation.Making.Spells.Count(s => !s.IsCantrip)}");
            Expect(before > 0, "the spells list doesn't scroll at this size - nothing was tested");
            Expect(IsInstanceValid(scroll) && scroll.ScrollVertical == before, "learning a spell scrolled the list");
            Expect(creation.Making.Spells.Any(s => !s.IsCantrip), "the box didn't learn the spell");
            Expect(Labels(creation).Any(l => l.Text == Ui.Say(ScreenWords.ChosenOf, 1, creation.Making.SpellPicks)),
                   "the count didn't go to 1 of n");

            foreach (string problem in _problems) GD.PrintErr("creation  " + problem);

            GD.Print(_problems.Count == 0 ? "creation ok" : $"creation FAILED - {_problems.Count} problem(s)");

            GameState.ForgetProbeSaves();
            GetTree().Quit(_problems.Count == 0 ? 0 : 1);
        }


        void Expect(bool ok, string problem)
        {
            if (!ok) Fail(problem);
        }

        void Fail(string problem) => _problems.Add(problem);

        void NoHoverText(Node page, string name)
        {
            foreach (Control control in Nodes.Under<Control>(page).Where(c => !string.IsNullOrEmpty(c.TooltipText)))
                Fail($"the {name} page has hover text on {control.GetType().Name} '{control.TooltipText}'");
        }

        static List<Button> Buttons(Node page) => Nodes.Under<Button>(page).Where(b => b is not CheckBox).ToList();

        static List<CheckBox> Boxes(Node page) => Nodes.Under<CheckBox>(page).ToList();

        static List<Label> Labels(Node page) => Nodes.Under<Label>(page).ToList();

        // the description area: the label with the card's own font, under the list
        static string About(Node page) =>
            Nodes.Under<Label>(page).FirstOrDefault(l => l.ThemeTypeVariation == "CardLabel")?.Text ?? "";
    }
}
