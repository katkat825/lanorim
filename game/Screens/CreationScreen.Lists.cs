using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Magic;
using Godot;

namespace Game.Screens
{
    public partial class CreationScreen
    {
        // --- the pages ------------------------------------------------------------------------------

        // a list of buttons, the chosen one pressed in, and what it is about under the list
        void Choices<T>(IEnumerable<T> things, Func<T, string> key, Func<T, bool> chosen,
                        Func<T, bool> pick, Func<T, string> about = null)
        {
            List<T> all = things.ToList();

            foreach (T thing in all)
            {
                T one = thing;
                Button button = Ui.Button(key(one), () =>
                {
                    pick(one);
                    Update();
                });

                button.ToggleMode = true;
                _updates.Add(() => button.SetPressedNoSignal(chosen(one)));

                _body.AddChild(button);
            }

            if (about == null) return;

            _describe = () => all.Where(chosen).Select(t => Ui.Say(about(t))).FirstOrDefault() ?? "";
        }

        void Scores()
        {
            foreach (Ability ability in Abilities.All)
            {
                Ability one = ability;

                Button less = Ui.Button("-", () => Shift(one, -1), true);
                Button more = Ui.Button("+", () => Shift(one, +1), true);
                Label score = Ui.Plain("");
                Label after = Ui.Plain("");

                _updates.Add(() =>
                {
                    int now = _making.Scores.Base(one);
                    less.Disabled = now <= Abilities.PointBuyFloor;
                    more.Disabled = now >= Abilities.PointBuyCeiling;
                    score.Text = now.ToString();
                    after.Text = $"→ {_making.ScoreAfter(one)}";
                });

                _body.AddChild(Ui.Row(8, Ui.Label(one.NameKey()), new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill },
                                      less, score, more, after));
            }

            Label left = Ui.Plain("");
            _updates.Add(() => left.Text = Ui.Say(ScreenWords.PointsLeft, Abilities.PointBuyBudget - _making.Scores.PointBuySpend));
            _body.AddChild(left);
        }

        void Shift(Ability ability, int by)
        {
            _making.Scores.SetBase(ability, Math.Clamp(_making.Scores.Base(ability) + by,
                                                       Abilities.PointBuyFloor, Abilities.PointBuyCeiling));
            Update();
        }

        // the "take it back" button comes and goes, so this page rebuilds (keeping its place)
        void Improvements()
        {
            _body.AddChild(Ui.Label(ScreenWords.PicksLeft, _making.ImprovementPicksLeft));

            AbilityImprovement suggested = _making.SuggestedImprovement();

            Button take = Ui.Button(ScreenWords.Suggested, () =>
            {
                _making.Improve(suggested);
                Draw();
            });
            take.Disabled = _making.ImprovementPicksLeft == 0;
            _body.AddChild(take);

            foreach (Ability ability in Abilities.All)
            {
                Ability one = ability;
                Button two = Ui.Button($"+2 {Ui.Say(one.NameKey())} ({_making.ScoreAfter(one)})", () =>
                {
                    _making.Improve(AbilityImprovement.Two(one), out _);
                    Draw();
                }, true);
                two.Disabled = _making.ImprovementPicksLeft == 0;
                _body.AddChild(two);
            }

            if (_making.Improvements.Count > 0)
                _body.AddChild(Ui.Button(ScreenWords.Back, () =>
                {
                    _making.Unimprove();
                    Draw();
                }));
        }

        void Skills()
        {
            foreach (Skill skill in _making.Class.SkillChoices)
            {
                Skill one = skill;

                Button button = Ui.Button(one.NameKey(), () =>
                {
                    if (_making.Skills.Contains(one)) _making.Untrain(one);
                    else _making.Train(one);

                    // the expertise list is the trained skills, so a rogue's page rebuilds
                    if (_making.ExpertisePicks > 0) Draw();
                    else Update();
                });

                button.ToggleMode = true;
                _updates.Add(() =>
                {
                    bool have = _making.Skills.Contains(one);
                    button.SetPressedNoSignal(have);
                    button.Disabled = !have && _making.SkillPicksLeft == 0;
                });

                _body.AddChild(button);
            }

            if (_making.ExpertisePicks == 0) return;

            Label left = Ui.Plain("");
            _updates.Add(() => left.Text = Ui.Say(ScreenWords.PicksLeft, _making.ExpertisePicksLeft));
            _body.AddChild(left);

            foreach (Skill skill in _making.Skills.Concat(_making.Background?.Skills ?? Array.Empty<Skill>()).Distinct())
            {
                Skill one = skill;

                Button button = Ui.Button($"★ {Ui.Say(one.NameKey())}", () =>
                {
                    if (_making.Expertise.Contains(one)) _making.Unmaster(one);
                    else _making.Master(one);
                    Update();
                }, true);

                button.ToggleMode = true;
                _updates.Add(() =>
                {
                    bool have = _making.Expertise.Contains(one);
                    button.SetPressedNoSignal(have);
                    button.Disabled = !have && _making.ExpertisePicksLeft == 0;
                });

                _body.AddChild(button);
            }
        }

        // A SPELL IS A ROW: a box that learns it, and its name, which selects it to read about. So a
        // spell can be read before it is chosen, and when the picks are full the others can still be
        // read; the card's text is in the description area (cc_task_ui-issues-9-30.md 3.2)
        void Spells(bool cantrips)
        {
            List<Spell> list = _making.SpellChoices.Concat(_making.Spells).Distinct()
                                      .Where(s => s.IsCantrip == cantrips)
                                      .OrderBy(s => s.Level).ThenBy(s => Ui.Say(s.NameKey))
                                      .ToList();

            // the first one is open to read before anything is touched
            Spell reading = list.FirstOrDefault();

            foreach (Spell spell in list)
            {
                Spell one = spell;
                string name = (one.IsCantrip ? "" : one.Level + "  ") + Ui.Say(one.NameKey);

                var learn = new CheckBox { FocusMode = FocusModeEnum.All, AccessibilityName = name };
                Button title = Ui.Button(name, () =>
                {
                    reading = one;
                    Update();
                }, true);

                title.ToggleMode = true;
                title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                title.Alignment = HorizontalAlignment.Left;
                title.FocusEntered += () =>
                {
                    reading = one;
                    Update();
                };

                learn.FocusEntered += () =>
                {
                    reading = one;
                    Update();
                };

                learn.Toggled += on =>
                {
                    if (on) _making.Learn(one);
                    else _making.Unlearn(one);
                    reading = one;
                    Update();
                };

                _updates.Add(() =>
                {
                    bool have = _making.Spells.Any(s => s.Id == one.Id);
                    bool room = cantrips ? _making.CantripPicksLeft > 0 : _making.SpellPicksLeft > 0;
                    learn.SetPressedNoSignal(have);
                    learn.Disabled = !have && !room;
                    title.SetPressedNoSignal(ReferenceEquals(reading, one));
                });

                _body.AddChild(Ui.Row(8, learn, title));
            }

            _describe = () => reading == null ? "" : SpellCardText.Of(SpellCard.Of(reading));
        }

        void Naming()
        {
            var name = new LineEdit
            {
                Text = _making.Name,
                PlaceholderText = Ui.Say(ScreenWords.NameHint),
                FocusMode = FocusModeEnum.All,
            };

            // the Begin button's state follows without rebuilding (which would steal the focus)
            name.TextChanged += words =>
            {
                _making.Call(words);
                Update();
            };

            name.TextSubmitted += _ => Forward();

            _body.AddChild(name);
        }
    }
}
