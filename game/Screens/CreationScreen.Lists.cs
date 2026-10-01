using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Localization;
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

                Button less = Ui.Button("-", () => { _making.Lower(one); Update(); }, true);
                Button more = Ui.Button("+", () => { _making.Raise(one); Update(); }, true);
                Label score = Ui.Plain("");
                Label after = Ui.Plain("");

                _updates.Add(() =>
                {
                    int now = _making.Scores.Base(one);
                    less.Disabled = !_making.CanLower(one);
                    more.Disabled = !_making.CanRaise(one);
                    score.Text = now.ToString();
                    after.Text = $"→ {_making.ScoreAfter(one)}";
                });

                _body.AddChild(Ui.Row(8, Ui.Label(one.NameKey()), new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill },
                                      less, score, more, after));
            }

            Label left = Ui.Plain("");
            _updates.Add(() => left.Text = Ui.Say(ScreenWords.PointsLeft, _making.Scores.PointBuyLeft));
            _body.AddChild(left);
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

        // THE SPECIES' OWN CHOICES (cc_task_e-shop-species-and-ui-notes.md 1.3): a section each for the skill, the
        // spellcasting ability and the size, whichever this species and lineage have; under the list, what each
        // trait is, in the species' own words
        void Traits()
        {
            if (_making.TraitSkillPicks > 0)
            {
                _body.AddChild(Ui.Label(ScreenWords.TraitSkill));

                foreach (Skill skill in _making.TraitSkills.Concat(_making.TraitSkillChoices).Distinct().OrderBy(s => (int)s).ToList())
                {
                    Skill one = skill;

                    Button button = Ui.Button(one.NameKey(), () =>
                    {
                        if (_making.TraitSkills.Contains(one)) _making.UnpickTraitSkill(one);
                        else _making.PickTraitSkill(one);
                        Update();
                    });

                    button.ToggleMode = true;
                    _updates.Add(() =>
                    {
                        bool have = _making.TraitSkills.Contains(one);
                        button.SetPressedNoSignal(have);
                        button.Disabled = !have && _making.TraitSkillPicksLeft == 0;
                    });

                    _body.AddChild(button);
                }
            }

            if (_making.SpellAbilityChoices.Count > 0)
            {
                _body.AddChild(Ui.Label(ScreenWords.TraitSpellAbility));
                Toggles(_making.SpellAbilityChoices, a => Ui.Say(a.NameKey()), a => _making.SpellAbility == a, a => _making.PickSpellAbility(a));
            }

            if (_making.ChoosesSize)
            {
                _body.AddChild(Ui.Label(ScreenWords.TraitSize));
                Toggles(_making.SizeChoices, s => Ui.Say(s.UiNameKey("size")), s => _making.Size == s, s => _making.PickSize(s));
            }

            _describe = () => string.Join("\n", _making.TraitFeatures.Select(f => Ui.Say(f.NameKey) + ": " + Ui.Say(f.DescriptionKey)));
        }

        // THE STARTING EQUIPMENT (cc_task_e-shop-species-and-ui-notes.md 1.4): the class's gear or its gold, the background's
        // or its gold; under the list, what the gear is. gold for either opens the shop before the campaign
        void Equipment()
        {
            _body.AddChild(Ui.Label(ScreenWords.KitFromClass));
            Toggles(Enum.GetValues<KitChoice>(), k => Kit(k, _making.ClassGold(k)), k => _making.ClassKit == k,
                    k => { _making.PickClassKit(k); return true; });

            if (_making.Background != null)
            {
                _body.AddChild(Ui.Label(ScreenWords.KitFromBackground));
                Toggles(Enum.GetValues<KitChoice>(), k => Kit(k, _making.BackgroundGold(k)), k => _making.BackgroundKit == k,
                        k => { _making.PickBackgroundKit(k); return true; });
            }

            _describe = () =>
            {
                var said = new List<string>();

                if (_making.ClassKit == KitChoice.Gear) said.Add(Gear(_making.Class.StartingGear));
                if (_making.BackgroundKit == KitChoice.Gear && _making.Background != null) said.Add(Gear(_making.Background.Gear));

                said.Add(Ui.Say(ScreenWords.KitStartsWith, Ui.Say(Content.Screens.PackView.GoldKey, _making.StartingGold)));

                if (_making.ClassKit == KitChoice.Gold || _making.BackgroundKit == KitChoice.Gold)
                    said.Add(Ui.Say(ScreenWords.KitShopFirst));

                return string.Join("\n", said);
            };
        }

        static string Kit(KitChoice choice, int gold) =>
            Ui.Say(choice == KitChoice.Gear ? ScreenWords.KitGear : ScreenWords.KitGold,
                   Ui.Say(Content.Screens.PackView.GoldKey, gold));

        // "Greataxe, Handaxe ×4"
        string Gear(IEnumerable<string> ids) =>
            string.Join(", ", ids.GroupBy(id => id)
                                 .Select(g => _making.Library.Items.Find(g.Key) is { } item
                                     ? Ui.Say(item.NameKey) + (g.Count() > 1 ? $" ×{g.Count()}" : "")
                                     : g.Key));

        // one of a few, pressed in when chosen: a section of a page (Choices is a whole page). `words` is what the
        // button says, already said
        void Toggles<T>(IEnumerable<T> things, Func<T, string> words, Func<T, bool> chosen, Func<T, bool> pick)
        {
            foreach (T thing in things.ToList())
            {
                T one = thing;
                Button button = Ui.Button(words(one), () =>
                {
                    pick(one);
                    Update();
                }, true);

                button.ToggleMode = true;
                _updates.Add(() => button.SetPressedNoSignal(chosen(one)));
                _body.AddChild(button);
            }
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
