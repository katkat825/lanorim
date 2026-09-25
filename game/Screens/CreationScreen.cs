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
    // skills and expertise, spells, how spells are paid for, alignment, a name. The rules are all in
    // Content.Creation; this shows a step and hands the pick back. Next is held until the step is
    // complete, and greyed with the reason.
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
                Step.Skills, Step.Spells, Step.SpellResource, Step.Alignment, Step.Name,
            };
        }

        public event Action<Hero> Finished;

        public event Action Cancelled;

        public Creation Making => _making;

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
            Step.Spells => _making.Class?.Casts == true,
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
            Step.Spells when _making.CantripPicksLeft > 0 || _making.SpellPicksLeft > 0 => ScreenWords.PicksLeft,
            Step.Name when _making.Name.Length == 0 => ScreenWords.NameHint,
            _ => null,
        };

        void Draw()
        {
            Ui.Clear(this);

            Step step = _pages[_at];

            AddChild(Ui.Title(ScreenWords.CreateTitle));
            AddChild(Ui.Label(ScreenWords.StepKey(step)));

            var body = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 6);

            switch (step)
            {
                case Step.Class: Choices(body, _making.ClassChoices, c => KeyConventions.ClassName(c.Id), c => _making.Class == c, c => _making.Pick(c), c => KeyConventions.ClassDescription(c.Id)); break;
                case Step.Species: Choices(body, _making.SpeciesChoices, s => s.NameKey, s => _making.Species == s, s => _making.Pick(s), s => s.DescriptionKey); break;
                case Step.Lineage: Choices(body, _making.LineageChoices, s => s.NameKey, s => _making.Lineage == s, s => _making.PickLineage(s), s => s.DescriptionKey); break;
                case Step.Background: Choices(body, _making.Backgrounds, b => b.NameKey, b => _making.Background == b, b => _making.Pick(b), b => b.DescriptionKey); break;
                case Step.Abilities: Scores(body); break;
                case Step.Improvements: Improvements(body); break;
                case Step.Skills: Skills(body); break;
                case Step.Spells: Spells(body); break;
                case Step.SpellResource: Choices(body, Enum.GetValues<SpellResourceMode>(), Creation.LabelKey, m => _making.Resource == m, m => _making.Pick(m), Creation.BlurbKey); break;
                case Step.Alignment: Choices(body, Alignments.All, a => a.NameKey(), a => _making.Alignment == a, a => { _making.Pick(a); return true; }); break;
                case Step.Name: Naming(body); break;
            }

            AddChild(Ui.Scroll(body, 360));

            string held = Holds(step);
            bool last = NextPage(_at) < 0;

            Button back = Ui.Button(ScreenWords.Back, Back);
            Button next = Ui.Button(last ? ScreenWords.Begin : ScreenWords.Next, Forward).Greyed(held != null, held);

            AddChild(Ui.Row(12, back, new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }, next));

            Ui.FocusFirst(body);
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
            Draw();
        }

        // a list of buttons, the chosen one pressed in
        void Choices<T>(VBoxContainer body, IEnumerable<T> things, Func<T, string> key, Func<T, bool> chosen,
                        Func<T, bool> pick, Func<T, string> about = null)
        {
            foreach (T thing in things)
            {
                T one = thing;
                Button button = Ui.Button(key(one), () =>
                {
                    pick(one);
                    Draw();
                });

                button.ToggleMode = true;
                button.ButtonPressed = chosen(one);

                if (about != null) button.TooltipText = Ui.Say(about(one));

                body.AddChild(button);
            }

            // the chosen one's description under the list, where it can be read without hovering
            T picked = things.FirstOrDefault(chosen);

            if (about != null && picked != null) body.AddChild(Ui.Label(about(picked)));
        }

        void Scores(VBoxContainer body)
        {
            _making.Scores.IsLegalPointBuy(out _);

            foreach (Ability ability in Abilities.All)
            {
                Ability one = ability;
                int score = _making.Scores.Base(one);

                Button less = Ui.Button("-", () => Shift(one, -1), true);
                Button more = Ui.Button("+", () => Shift(one, +1), true);

                less.Disabled = score <= Abilities.PointBuyFloor;
                more.Disabled = score >= Abilities.PointBuyCeiling;

                body.AddChild(Ui.Row(8, Ui.Label(one.NameKey()), new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill },
                                     less, Ui.Plain(score.ToString()), more,
                                     Ui.Plain($"→ {_making.ScoreAfter(one)}")));
            }

            body.AddChild(Ui.Label(ScreenWords.PointsLeft, Abilities.PointBuyBudget - _making.Scores.PointBuySpend));
        }

        void Shift(Ability ability, int by)
        {
            _making.Scores.SetBase(ability, Math.Clamp(_making.Scores.Base(ability) + by,
                                                       Abilities.PointBuyFloor, Abilities.PointBuyCeiling));
            Draw();
        }

        void Improvements(VBoxContainer body)
        {
            body.AddChild(Ui.Label(ScreenWords.PicksLeft, _making.ImprovementPicksLeft));

            AbilityImprovement suggested = _making.SuggestedImprovement();

            body.AddChild(Ui.Button(ScreenWords.Suggested, () =>
            {
                _making.Improve(suggested);
                Draw();
            }).Greyed(_making.ImprovementPicksLeft == 0, ScreenWords.PicksLeft));

            foreach (Ability ability in Abilities.All)
            {
                Ability one = ability;
                body.AddChild(Ui.Button($"+2 {Ui.Say(one.NameKey())} ({_making.ScoreAfter(one)})", () =>
                {
                    _making.Improve(AbilityImprovement.Two(one), out _);
                    Draw();
                }, true).Greyed(_making.ImprovementPicksLeft == 0, ScreenWords.PicksLeft));
            }

            if (_making.Improvements.Count > 0)
                body.AddChild(Ui.Button(ScreenWords.Back, () =>
                {
                    _making.Unimprove();
                    Draw();
                }));
        }

        void Skills(VBoxContainer body)
        {
            body.AddChild(Ui.Label(ScreenWords.PicksLeft, _making.SkillPicksLeft));

            foreach (Skill skill in _making.Class.SkillChoices)
            {
                Skill one = skill;
                bool have = _making.Skills.Contains(one);

                Button button = Ui.Button(one.NameKey(), () =>
                {
                    if (have) _making.Untrain(one);
                    else _making.Train(one);
                    Draw();
                });

                button.ToggleMode = true;
                button.ButtonPressed = have;
                button.Disabled = !have && _making.SkillPicksLeft == 0;
                body.AddChild(button);
            }

            if (_making.ExpertisePicks == 0) return;

            body.AddChild(Ui.Label(ScreenWords.PicksLeft, _making.ExpertisePicksLeft));

            foreach (Skill skill in _making.Skills.Concat(_making.Background?.Skills ?? Array.Empty<Skill>()).Distinct())
            {
                Skill one = skill;
                bool have = _making.Expertise.Contains(one);

                Button button = Ui.Button($"★ {Ui.Say(one.NameKey())}", () =>
                {
                    if (have) _making.Unmaster(one);
                    else _making.Master(one);
                    Draw();
                }, true);

                button.ToggleMode = true;
                button.ButtonPressed = have;
                button.Disabled = !have && _making.ExpertisePicksLeft == 0;
                body.AddChild(button);
            }
        }

        void Spells(VBoxContainer body)
        {
            body.AddChild(Ui.Label(ScreenWords.PicksLeft, _making.CantripPicksLeft + _making.SpellPicksLeft));

            foreach (Spell spell in _making.SpellChoices.Concat(_making.Spells).Distinct()
                                           .OrderBy(s => s.Level).ThenBy(s => Ui.Say(s.NameKey)))
            {
                Spell one = spell;
                bool have = _making.Spells.Any(s => s.Id == one.Id);
                bool room = one.IsCantrip ? _making.CantripPicksLeft > 0 : _making.SpellPicksLeft > 0;

                Button button = Ui.Button($"{(one.IsCantrip ? "·" : one.Level.ToString())}  {Ui.Say(one.NameKey)}", () =>
                {
                    if (have) _making.Unlearn(one);
                    else _making.Learn(one);
                    Draw();
                }, true);

                button.ToggleMode = true;
                button.ButtonPressed = have;
                button.Disabled = !have && !room;
                body.AddChild(button);
            }
        }

        void Naming(VBoxContainer body)
        {
            var name = new LineEdit
            {
                Text = _making.Name,
                PlaceholderText = Ui.Say(ScreenWords.NameHint),
                FocusMode = FocusModeEnum.All,
            };

            name.TextChanged += words =>
            {
                _making.Call(words);
                // the Begin button's state follows without redrawing (which would steal the focus)
                if (GetChild(GetChildCount() - 1) is HBoxContainer row && row.GetChild(2) is Button begin)
                    begin.Greyed(_making.Name.Length == 0, ScreenWords.NameHint);
            };

            name.TextSubmitted += _ => Forward();

            body.AddChild(name);
        }
    }
}
