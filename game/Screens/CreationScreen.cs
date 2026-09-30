using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Localization;
using Core.Magic;
using Godot;

namespace Game.Screens
{
    // CHARACTER CREATION (Tier 3b): one page a step, in the order a player thinks about a character -
    // class, species (and lineage), background, ability scores, improvements when made above level 4,
    // skills and expertise, cantrips, spells, how spells are paid for, alignment, a name. The rules are
    // all in Content.Creation; this shows a step and hands the pick back. Next is held until the step
    // is complete, with the reason written beside it.
    //
    // NO HOVER TEXT (cc_task_ui-issues-9-30.md 3.1): what a choice is about is written in the
    // description area under the list, for the one selected. A pick updates the page in place rather
    // than rebuilding it, so the list keeps its scroll and the keyboard keeps its place (3.4); the few
    // pages that must rebuild put the scroll and the focus back.
    public partial class CreationScreen : VBoxContainer
    {
        readonly Creation _making;
        readonly List<Step> _pages;
        int _at;

        public CreationScreen(Library library, int level = 1)
        {
            _making = new Creation(library, library.Backgrounds);

            if (level > 1) _making.StartAt(level);

            _pages = new List<Step>
            {
                Step.Class, Step.Species, Step.Lineage, Step.Background, Step.Abilities, Step.Improvements,
                Step.Skills, Step.Cantrips, Step.Spells, Step.SpellResource, Step.Alignment, Step.Name,
            };
        }

        public event Action<Hero> Finished;

        public event Action Cancelled;

        public Creation Making => _making;

        // the page's moving parts, kept so a pick can update them where they are
        readonly List<Action> _updates = new List<Action>();
        ScrollContainer _scroll;
        VBoxContainer _body;
        Label _count;
        Label _about;
        Label _held;
        Button _next;
        Func<string> _describe;

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 12);
            Draw();
        }

        // steps that have nothing to ask for this character are passed over
        bool Asks(Step step) => step switch
        {
            Step.Lineage => _making.NeedsLineage,
            Step.Improvements => _making.ImprovementPicks > 0,
            Step.Cantrips => _making.Class?.Casts == true && _making.CantripPicks > 0,
            Step.Spells => _making.Class?.Casts == true && _making.SpellPicks > 0,
            Step.SpellResource => _making.ChoosesResource,
            _ => true,
        };

        // whether the page is complete, and if not, why
        string Holds(Step step) => step switch
        {
            Step.Class when _making.Class == null => ScreenWords.StepKey(Step.Class),
            Step.Species when _making.Species == null => ScreenWords.StepKey(Step.Species),
            Step.Lineage when _making.Lineage == null => ScreenWords.StepKey(Step.Lineage),
            Step.Background when _making.Background == null => ScreenWords.StepKey(Step.Background),
            Step.Abilities when !_making.Scores.IsLegalPointBuy(out _) => ScreenWords.PointsLeft,
            Step.Improvements when _making.ImprovementPicksLeft > 0 => ScreenWords.PicksLeft,
            Step.Skills when _making.SkillPicksLeft > 0 || _making.ExpertisePicksLeft > 0 => ScreenWords.PicksLeft,
            Step.Cantrips when _making.CantripPicksLeft > 0 => ScreenWords.PicksLeft,
            Step.Spells when _making.SpellPicksLeft > 0 => ScreenWords.PicksLeft,
            Step.Name when _making.Name.Length == 0 => ScreenWords.NameHint,
            _ => null,
        };

        // how many of how many a counted page has chosen ("2 of 3 chosen"), or null
        (int Chosen, int Of)? Counted(Step step) => step switch
        {
            Step.Skills => (_making.SkillPicks - _making.SkillPicksLeft, _making.SkillPicks),
            Step.Cantrips => (_making.CantripPicks - _making.CantripPicksLeft, _making.CantripPicks),
            Step.Spells => (_making.SpellPicks - _making.SpellPicksLeft, _making.SpellPicks),
            _ => null,
        };

        void Draw()
        {
            // a rebuild of the same page keeps its place: the scroll, and which control had the focus
            bool again = _body != null && IsInstanceValid(_body);
            int scrolled = again ? _scroll.ScrollVertical : 0;
            int focused = again ? Focusables().IndexOf(GetViewport()?.GuiGetFocusOwner()) : -1;

            Ui.Clear(this);
            _updates.Clear();
            _describe = null;

            Step step = _pages[_at];

            AddChild(Ui.Title(ScreenWords.CreateTitle));
            AddChild(Ui.Label(ScreenWords.StepKey(step)));

            _count = Ui.Plain("");
            AddChild(_count);

            _body = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            _body.AddThemeConstantOverride("separation", 6);

            switch (step)
            {
                case Step.Class: Choices(_making.ClassChoices, c => KeyConventions.ClassName(c.Id), c => _making.Class == c, c => _making.Pick(c), c => KeyConventions.ClassDescription(c.Id)); break;
                case Step.Species: Choices(_making.SpeciesChoices, s => s.NameKey, s => _making.Species == s, s => _making.Pick(s), s => s.DescriptionKey); break;
                case Step.Lineage: Choices(_making.LineageChoices, s => s.NameKey, s => _making.Lineage == s, s => _making.PickLineage(s), s => s.DescriptionKey); break;
                case Step.Background: Choices(_making.Backgrounds, b => b.NameKey, b => _making.Background == b, b => _making.Pick(b), b => b.DescriptionKey); break;
                case Step.Abilities: Scores(); break;
                case Step.Improvements: Improvements(); break;
                case Step.Skills: Skills(); break;
                case Step.Cantrips: Spells(cantrips: true); break;
                case Step.Spells: Spells(cantrips: false); break;
                case Step.SpellResource: Choices(Enum.GetValues<SpellResourceMode>(), Creation.LabelKey, m => _making.Resource == m, m => _making.Pick(m), Creation.BlurbKey); break;
                case Step.Alignment: Choices(Alignments.All, a => a.NameKey(), a => _making.Alignment == a, a => { _making.Pick(a); return true; }); break;
                case Step.Name: Naming(); break;
            }

            _scroll = Ui.Scroll(_body, 300);
            AddChild(_scroll);

            // THE DESCRIPTION AREA: what the selected choice is, under the list rather than on hover
            _about = Ui.Plain("");
            _about.ThemeTypeVariation = "CardLabel";
            _about.CustomMinimumSize = new Vector2(0, Ui.Px(HudLayout.Current.CreationAboutHeight));
            AddChild(_about);

            Button back = Ui.Button(ScreenWords.Back, Back);
            _next = Ui.Button(ScreenWords.Next, Forward);
            _held = Ui.Plain("");
            _held.HorizontalAlignment = HorizontalAlignment.Right;
            _held.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            AddChild(Ui.Row(12, back, _held, _next));

            Update();

            if (again && focused >= 0 && focused < Focusables().Count) Ui.FocusLater(Focusables()[focused]);
            else Ui.FocusFirst(_body);

            if (again && scrolled > 0) KeepScroll(scrolled);
        }

        // everything a pick can change, without rebuilding the page
        void Update()
        {
            foreach (Action update in _updates) update();

            Step step = _pages[_at];

            (int Chosen, int Of)? counted = Counted(step);
            _count.Text = counted is { } c ? Ui.Say(ScreenWords.ChosenOf, c.Chosen, c.Of) : "";
            _count.Visible = counted.HasValue;

            string about = _describe?.Invoke() ?? "";
            _about.Text = about;
            _about.Visible = _describe != null;

            string held = Holds(step);
            bool last = NextPage(_at) < 0;

            _next.Text = Ui.Say(last ? ScreenWords.Begin : ScreenWords.Next);
            _next.Disabled = held != null;
            _held.Text = held == null ? "" : Ui.Say(held, PicksLeftFor(step));
        }

        int PicksLeftFor(Step step) => step switch
        {
            Step.Improvements => _making.ImprovementPicksLeft,
            Step.Skills => _making.SkillPicksLeft + _making.ExpertisePicksLeft,
            Step.Cantrips => _making.CantripPicksLeft,
            Step.Spells => _making.SpellPicksLeft,
            Step.Abilities => Abilities.PointBuyBudget - _making.Scores.PointBuySpend,
            _ => 0,
        };

        List<Control> Focusables() =>
            _body == null || !IsInstanceValid(_body)
                ? new List<Control>()
                : Nodes.Under<Control>(_body).Where(c => c.FocusMode == FocusModeEnum.All).ToList();

        // the rebuilt list is laid out next frame, and a scroll set before then is clamped to the top
        async void KeepScroll(int scrolled)
        {
            ScrollContainer scroll = _scroll;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (IsInstanceValid(scroll)) scroll.ScrollVertical = scrolled;
        }

        int NextPage(int from)
        {
            for (int i = from + 1; i < _pages.Count; i++) if (Asks(_pages[i])) return i;
            return -1;
        }

        int PreviousPage(int from)
        {
            for (int i = from - 1; i >= 0; i--) if (Asks(_pages[i])) return i;
            return -1;
        }

        void Forward()
        {
            if (Holds(_pages[_at]) != null) return;

            int next = NextPage(_at);

            if (next < 0)
            {
                Hero hero = _making.Finish();

                if (hero == null)
                {
                    GD.PushError("creation: " + string.Join("; ", _making.Problems));
                    return;
                }

                Finished?.Invoke(hero);
                return;
            }

            _at = next;
            _body = null;
            Draw();
        }

        void Back()
        {
            int back = PreviousPage(_at);

            if (back < 0)
            {
                Cancelled?.Invoke();
                return;
            }

            _at = back;
            _body = null;
            Draw();
        }

        // which page it is on, for a test or a picture
        public Step Showing => _pages[_at];

        // straight to a page (the --show flag, for a picture of it)
        public void Open(Step step)
        {
            int at = _pages.IndexOf(step);
            if (at < 0) return;

            _at = at;
            _body = null;
            if (IsInsideTree()) Draw();
        }
    }
}
