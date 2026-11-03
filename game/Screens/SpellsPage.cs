using System;
using Content.Screens;
using Content.Spells;
using Core.Localization;
using Core.Magic;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "SPELLS" (cc_task_cs 2.3): every spell the game ships, by level, narrowed by class, by level and by name
    // as it's typed, each name opening its card (CardOverlay). The list and the narrowing are SpellReference's; this lays
    // them out. Reached from the contents like Credits; how it looks is Kathleen's
    public sealed class SpellsPage : IBookPage
    {
        public string Id => "spells";

        public string NameKey => SpellReference.TitleKey;

        SpellReference _reference;
        VBoxContainer _list;
        Label _count;

        public void Open(Launch launch, string open = null)
        {
            _reference = new SpellReference(SpellBook.Srd(), key => Ui.Say(key));

            var search = new LineEdit
            {
                PlaceholderText = Ui.Say(SpellReference.SearchKey),
                FocusMode = Control.FocusModeEnum.All,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClearButtonEnabled = true,
            };
            search.TextChanged += words =>
            {
                _reference.Search = words;
                Fill(launch);
            };

            OptionButton classes = Picker(SpellReference.AllClassesKey);

            foreach (string id in _reference.Classes) classes.AddItem(Ui.Say(KeyConventions.ClassName(id)));

            classes.ItemSelected += at =>
            {
                _reference.Class = at == 0 ? null : _reference.Classes[(int)at - 1];
                Fill(launch);
            };

            OptionButton levels = Picker(SpellReference.AllLevelsKey);

            foreach (int level in _reference.Levels)
                levels.AddItem(level == 0 ? Ui.Say(Ui0("cantrip")) : Ui.Say(Ui0("leveled"), level));

            levels.ItemSelected += at =>
            {
                _reference.Level = at == 0 ? null : _reference.Levels[(int)at - 1];
                Fill(launch);
            };

            _count = Ui.Plain("");
            _list = Ui.Column(6);

            var page = Ui.Column(12,
                Ui.Title(SpellReference.TitleKey),
                Ui.Row(12, search, classes, levels),
                _count,
                Ui.Scroll(_list, 380),
                Ui.Button(CampaignBook.ContentsKey, launch.ShowBook));

            Fill(launch);
            launch.Show(Ui.Panel(page), 900);
        }

        // "Cantrip", "Level 3": the card's own words for a level
        static string Ui0(string which) => KeyConventions.Key(KeyConventions.UiNs, "spell_level", which, "name");

        static OptionButton Picker(string allKey)
        {
            var picker = new OptionButton { FocusMode = Control.FocusModeEnum.All };
            picker.AddItem(Ui.Say(allKey));
            picker.Selected = 0;
            return picker;
        }

        // the spells that match, a heading for each level and the names under it
        void Fill(Launch launch)
        {
            Ui.Clear(_list);

            _count.Text = Ui.Say(SpellReference.CountKey, _reference.ShownCount, _reference.Count);

            if (_reference.ShownCount == 0) _list.AddChild(Ui.Label(SpellReference.NoneKey));

            foreach ((int level, var spells) in _reference.Shown)
            {
                Label heading = Ui.Plain(level == 0 ? Ui.Say(Ui0("cantrip")) : Ui.Say(Ui0("leveled"), level));
                heading.ThemeTypeVariation = "CardTitle";
                _list.AddChild(heading);

                HFlowContainer names = Ui.Flow(6);

                foreach (Spell spell in spells)
                {
                    Spell one = spell;
                    names.AddChild(Ui.Button(Ui.Say(one.NameKey), () => CardOverlay.Show(launch, SpellCard.Of(one)), true));
                }

                _list.AddChild(names);
            }
        }
    }
}
