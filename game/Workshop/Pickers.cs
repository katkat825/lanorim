using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Items;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Content.Workshop.Tables;
using Core.Dice;
using Core.Localization;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE TABLE EDITORS' SHARED PIECES (cc_task_n Part 6, built once, #9): the monster picker, the item picker and the map
    // picker, the dice-or-number field and the "how likely" control. The fights, loot and shops tabs use them, and so do
    // the story editor's blocks (O, P)
    public static class Pickers
    {
        // --- the monster picker: search by name, filter by challenge and kind, each with a line from its stat block ---

        public static VBoxContainer Monster(string name, Bestiary bestiary, Action<string> picked)
        {
            var column = Ui.Column(4);
            column.Name = name;

            LineEdit search = Forms.Line(name + "_Search", "", _ => { }, TableWords.SearchKey);
            search.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var challenges = new[] { -1.0 }.Concat(bestiary.All.Select(m => m.Challenge).Distinct().OrderBy(c => c)).ToList();
            var kinds = new[] { "" }.Concat(bestiary.All.Select(Kind).Where(k => k.Length > 0).Distinct().OrderBy(k => k)).ToList();

            double challenge = -1;
            string kind = "";

            var list = new ItemList { Name = name + "_List", CustomMinimumSize = new Vector2(0, Ui.Px(140)), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var shown = new List<Monster>();

            OptionButton byChallenge = Forms.Choice(name + "_Challenge", challenges, -1.0, c => c < 0 ? TableWords.AnyChallengeKey : null, c => { challenge = c; Fill(); });
            for (int i = 1; i < challenges.Count; i++) byChallenge.SetItemText(i, Challenge(challenges[i]));
            OptionButton byKind = Forms.Choice(name + "_Kind", kinds, "", k => k.Length == 0 ? TableWords.AnyTypeKey : null, k => { kind = k; Fill(); });
            for (int i = 1; i < kinds.Count; i++) byKind.SetItemText(i, TablesDraft.Plain(kinds[i]));

            void Fill()
            {
                list.Clear();
                shown.Clear();

                foreach (Monster monster in bestiary.All.Where(m => (challenge < 0 || m.Challenge == challenge) && (kind.Length == 0 || Kind(m) == kind))
                                                        .Where(m => search.Text.Length == 0 || NameOf(m).Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    shown.Add(monster);
                    list.AddItem($"{NameOf(monster)}   {Ui.Say(TableWords.MonsterLineKey, Challenge(monster.Challenge), monster.HitPoints, monster.ArmorClass)}");
                }
            }

            search.TextChanged += _ => Fill();
            list.ItemActivated += i => picked(shown[(int)i].Id);

            Button add = Ui.Button(TableWords.AddMonsterKey, () =>
            {
                int[] at = list.GetSelectedItems();
                if (at.Length > 0) picked(shown[at[0]].Id);
            });
            add.Name = name + "_Add";

            Fill();

            byChallenge.CustomMinimumSize = new Vector2(Ui.Px(150), 0);
            byKind.CustomMinimumSize = new Vector2(Ui.Px(150), 0);
            column.AddChild(Ui.Row(8, search, byChallenge, byKind));
            column.AddChild(list);
            column.AddChild(add);
            return column;
        }

        public static string NameOf(Monster monster) => Said(KeyConventions.MonsterName(monster.Id), monster.Id);

        // a monster by its id, named, whether the campaign has it or not
        public static string MonsterName(Bestiary bestiary, string id) => Said(KeyConventions.MonsterName(id), id);

        static string Kind(Monster monster) => monster.Tags.FirstOrDefault() ?? "";

        // 1/8, 1/4, 1/2, 1, 2... as the SRD writes a challenge. the data keeps the fractions in tenths, as the SRD's XP
        // table is read (FightBudget: 0.1 is 1/8, 0.2 is 1/4, 0.5 is 1/2)
        public static string Challenge(double challenge) =>
            (int)Math.Round(challenge * 10) switch { 1 => "1/8", 2 => "1/4", 5 when challenge < 1 => "1/2", _ => challenge.ToString("0.##") };

        // a key's words, or the id made plain when the locale has none (a campaign's own monster not named yet)
        static string Said(string key, string id)
        {
            string said = Ui.Say(key);
            return said == key ? TablesDraft.Plain(id) : said;
        }

        // --- the item picker: search by name, each with its price; `allowed` greys out what can't go here ------------

        public static VBoxContainer Item(string name, ItemShelf items, Action<string> picked, Func<Item, bool> allowed = null, string whyNotKey = null)
        {
            var column = Ui.Column(4);
            column.Name = name;

            LineEdit search = Forms.Line(name + "_Search", "", _ => { }, TableWords.SearchKey);
            search.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var list = new ItemList { Name = name + "_List", CustomMinimumSize = new Vector2(0, Ui.Px(140)), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var shown = new List<Item>();

            void Fill()
            {
                list.Clear();
                shown.Clear();

                foreach (Item item in items.All.Where(i => search.Text.Length == 0 || ItemName(i.Id).Contains(search.Text, StringComparison.OrdinalIgnoreCase))
                                               .OrderBy(i => ItemName(i.Id)))
                {
                    shown.Add(item);
                    int at = list.AddItem($"{ItemName(item.Id)}   {(item.Cost > 0 ? Ui.Money(item.Cost) : "")}");
                    list.SetItemDisabled(at, allowed != null && !allowed(item));
                }
            }

            void Pick(int at)
            {
                if (at >= 0 && at < shown.Count && !list.IsItemDisabled(at)) picked(shown[at].Id);
            }

            search.TextChanged += _ => Fill();
            list.ItemActivated += i => Pick((int)i);

            Button add = Ui.Button(TableWords.AddItemKey, () =>
            {
                int[] at = list.GetSelectedItems();
                if (at.Length > 0) Pick(at[0]);
            });
            add.Name = name + "_Add";

            Fill();

            column.AddChild(search);
            column.AddChild(list);
            if (whyNotKey != null) column.AddChild(Forms.Hint(whyNotKey));
            column.AddChild(add);
            return column;
        }

        public static string ItemName(string id) => Said(KeyConventions.ItemName(id), id);

        // --- the map picker: the campaign's maps, each by its name, with a small picture of the one picked ----------

        public static HBoxContainer Map(string name, MapPlace place, string now, Action<string> picked, Action makeNew = null)
        {
            IReadOnlyList<string> maps = MapFiles.Ids(place);
            var values = new[] { "" }.Concat(maps).ToList();

            var thumb = new MapThumb { Name = name + "_Picture", CustomMinimumSize = new Vector2(Ui.Px(96), Ui.Px(72)) };
            thumb.Show(place, now);

            OptionButton list = Forms.Choice(name, values, values.Contains(now) ? now : "", m => m.Length == 0 ? TableWords.NoMapKey : null, m =>
            {
                thumb.Show(place, m);
                picked(m);
            });
            for (int i = 1; i < values.Count; i++) list.SetItemText(i, MapName.Of(place, values[i]));
            list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var row = Ui.Row(8, thumb, list);

            if (makeNew != null)
            {
                Button make = Ui.Button(TableWords.MakeMapKey, makeNew);
                make.Name = name + "_Make";
                row.AddChild(make);
            }

            return row;
        }

        // the spawns a campaign map has (0 when it has none, or isn't there)
        public static int Spawns(MapPlace place, string map) =>
            map.Length > 0 && MapFiles.Read(place, map, out MapDraft draft, out _) ? draft.Spawns.Count : 0;

        // --- the dice-or-number field: a number by default, dice in Advanced ---------------------------------------

        // "" is the unsaid default (one); `advanced` shows a box for dice, otherwise a number
        public static Control Dice(string name, string now, bool advanced, Action<string> changed, int min = 1, int max = 99)
        {
            bool flat = now.Length == 0 || int.TryParse(now, out _);

            if (advanced || !flat)
            {
                LineEdit box = Forms.Line(name, now, t => changed(t.Trim()));
                box.CustomMinimumSize = new Vector2(Ui.Px(90), 0);
                box.TooltipText = "";
                return Ui.Column(2, box, Forms.Hint(TableWords.DiceHintKey));
            }

            SpinBox number = Forms.Number(name, min, max, now.Length == 0 ? 1 : int.Parse(now), v => changed(v.ToString()));
            return number;
        }

        // whether what's typed is dice the game can read
        public static bool IsDice(string text) => text.Length == 0 || DiceRoll.TryParse(text, out DiceRoll dice, out _) && !dice.IsNothing;

        // --- how likely: a weight, said as a share of its table ("3 in 6") ----------------------------------------------

        public static HBoxContainer Likely(string name, int weight, int total, Action<int> changed)
        {
            var slider = new HSlider { Name = name, MinValue = 1, MaxValue = 10, Step = 1, Value = Math.Clamp(weight, 1, 10), CustomMinimumSize = new Vector2(Ui.Px(140), 0) };
            Label share = Ui.Label(TableWords.LikelyShareKey, weight, Math.Max(total, weight));
            share.AutowrapMode = TextServer.AutowrapMode.Off;

            slider.ValueChanged += v =>
            {
                share.Text = Ui.Say(TableWords.LikelyShareKey, (int)v, total - weight + (int)v);
                changed((int)v);
            };

            Label label = Ui.Label(TableWords.LikelyKey);
            label.AutowrapMode = TextServer.AutowrapMode.Off;

            return Ui.Row(8, label, slider, share);
        }
    }

    // A MAP, SMALL: each square its ground's colour, walls dark, spawns red and the start gold. Drawn, so it needs no
    // render of the board (the map picker's picture)
    public partial class MapThumb : Control
    {
        MapDraft _map;

        public void Show(MapPlace place, string id)
        {
            _map = id?.Length > 0 && MapFiles.Read(place, id, out MapDraft draft, out _) ? draft : null;
            QueueRedraw();
        }

        static readonly Dictionary<Ground, Color> Grounds = new()
        {
            [Ground.Stone] = new Color(0.55f, 0.53f, 0.5f),
            [Ground.Planks] = new Color(0.55f, 0.4f, 0.25f),
            [Ground.Grass] = new Color(0.38f, 0.55f, 0.28f),
            [Ground.Dirt] = new Color(0.5f, 0.4f, 0.28f),
            [Ground.Cobble] = new Color(0.47f, 0.47f, 0.47f),
            [Ground.Sand] = new Color(0.8f, 0.72f, 0.5f),
            [Ground.Snow] = new Color(0.9f, 0.92f, 0.95f),
        };

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.2f, 0.18f, 0.16f));

            if (_map == null || _map.Columns == 0 || _map.Rows == 0) return;

            float side = Mathf.Min(Size.X / _map.Columns, Size.Y / _map.Rows);
            var origin = new Vector2((Size.X - side * _map.Columns) / 2, (Size.Y - side * _map.Rows) / 2);

            for (int x = 0; x < _map.Columns; x++)
                for (int y = 0; y < _map.Rows; y++)
                {
                    var cell = new Cell(x, y);
                    Tile tile = _map.At(cell);
                    Color colour = tile == Tile.Void ? new Color(0.12f, 0.11f, 0.1f)
                                 : Grounds.TryGetValue(_map.GroundAt(cell), out Color c) ? c : Grounds[Ground.Stone];
                    if (_map.IsBlocked(cell)) colour = colour.Darkened(0.4f);
                    DrawRect(new Rect2(origin + new Vector2(x, y) * side, new Vector2(side, side)), colour);
                }

            foreach (Cell spawn in _map.Spawns.Values)
                DrawRect(new Rect2(origin + new Vector2(spawn.X, spawn.Y) * side, new Vector2(side, side)), new Color(0.75f, 0.15f, 0.1f));

            DrawRect(new Rect2(origin + new Vector2(_map.Start.X, _map.Start.Y) * side, new Vector2(side, side)), new Color(0.9f, 0.75f, 0.2f));
        }
    }
}
