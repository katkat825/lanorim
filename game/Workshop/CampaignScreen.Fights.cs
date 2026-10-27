using System;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Combat;
using Core.Tables;
using Core.Words;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE FIGHTS TAB (cc_task_n Part 1): the campaign's set-piece fights, each a card. A fight made here goes into the
    // Workshop's own fights table; one in another table is shown and edited where it is. The fight's form is Fight(),
    // which the Random encounters tab uses inline for a fight entry
    public partial class CampaignScreen
    {
        void Fights(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(TableWords.FightsHintKey));
            Unread(page);

            var fights = Draft.Tables.Fights.ToList();
            if (fights.Count == 0) page.AddChild(Forms.Hint(TableWords.NoFightsKey));

            WhereUsed uses = Editor.Uses;

            foreach ((EncounterTableDraft table, EncounterDraft fight) in fights)
            {
                string id = fight.Id;
                string summary = string.Join(", ", fight.Monsters.Select(m => $"{(m.Count.Length == 0 ? "1" : m.Count)} {Pickers.MonsterName(Things.Bestiary, m.Monster)}"));
                if (table.Id != TablesDraft.FightsTable) summary += "   " + Ui.Say(TableWords.InFileKey, table.File);

                page.AddChild(Card("fight", id, Draft.Tables.NameOf("fight", id), summary, uses.OfFight(id),
                                   () => Editor.DeleteFight(id), body => Fight(body, id)));
            }

            page.AddChild(Maker("NewFight", TableWords.NewFightKey, TableWords.NewFightKey, Editor.NewFight, "fight"));

            TableJson(page, fights.Select(f => f.Fight.ToJson()));
        }

        // a fight in the draft, wherever it is
        static EncounterDraft FightIn(CampaignDraft d, string id) => d.Tables.Fight(id).Fight;

        // THE FIGHT'S FORM: name, where, who, for level and how hard with the live meter, when they give up, if the hero
        // loses, what they leave behind, the placement check, and Try this fight
        void Fight(VBoxContainer form, string id, bool named = true)
        {
            EncounterDraft fight = FightIn(Draft, id);
            if (fight == null) return;

            if (named) form.AddChild(NameField("fight", id));

            // 2. where
            form.AddChild(Forms.Field(TableWords.WhereKey,
                Pickers.Map("FightMap_" + id, Editor.Maps, fight.Map, m => Change(() => Editor.Edit("fight", d => FightIn(d, id).Map = m)), () => MakeMapFor(id)),
                TableWords.WhereHintKey));

            // 3. who
            var meter = Ui.Column(4);
            meter.Name = "Meter_" + id;

            var who = Ui.Column(6);
            for (int i = 0; i < fight.Monsters.Count; i++) who.AddChild(Band(id, i, fight.Monsters[i], () => Meter(meter, id)));
            who.AddChild(Pickers.Monster("AddMonster_" + id, Things.Bestiary,
                m => Change(() => Editor.Edit("fight", d => FightIn(d, id).Monsters.Add(new BandDraft { Monster = m })))));
            form.AddChild(Forms.Field(TableWords.WhoKey, who, TableWords.WhoHintKey));

            // 4. for level, how hard
            SpinBox level = Forms.Number("FightLevel_" + id, 1, 20, Math.Max(1, fight.Level), v => { Edit("fight.level", d => FightIn(d, id).Level = v, true); Meter(meter, id); });
            EnumWords.TryName(fight.Difficulty.Length == 0 ? "moderate" : fight.Difficulty, out EncounterDifficulty difficulty);
            OptionButton hard = Forms.Choice("FightHard_" + id, Enum.GetValues<EncounterDifficulty>(), difficulty, TableWords.Difficulty,
                v => { Edit("fight", d => FightIn(d, id).Difficulty = EnumWords.Name(v)); Meter(meter, id); });
            VBoxContainer forLevel = Forms.Field(TableWords.ForLevelKey, level, null), howHard = Forms.Field(TableWords.HowHardKey, hard, null);
            forLevel.SizeFlagsHorizontal = howHard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            form.AddChild(Ui.Row(12, forLevel, howHard));

            // 5. the meter
            Meter(meter, id);
            form.AddChild(meter);

            // 6. when they give up
            EnumWords.TryName(fight.Morale.Length == 0 ? "none" : fight.Morale, out Morale morale);
            form.AddChild(Forms.Field(TableWords.MoraleKey,
                Forms.Choice("FightMorale_" + id, Enum.GetValues<Morale>(), morale, TableWords.Morale,
                             v => Edit("fight", d => FightIn(d, id).Morale = v == Morale.None ? "" : EnumWords.Name(v))), null));

            // 7. if the hero loses
            EnumWords.TryName(fight.Defeat, out Defeat defeat);
            form.AddChild(Forms.Field(TableWords.DefeatKey,
                Forms.Choice("FightDefeat_" + id, Enum.GetValues<Defeat>(), defeat, TableWords.Defeat,
                             v => Edit("fight", d => FightIn(d, id).Defeat = v == Defeat.Unsaid ? "" : EnumWords.Name(v))), null));

            // 8. what they leave behind
            var loots = new[] { "" }.Concat(Draft.Tables.Loot.Select(t => t.Id)).ToList();
            OptionButton leaves = Forms.Choice("FightLoot_" + id, loots, loots.Contains(fight.Loot) ? fight.Loot : "",
                                               l => l.Length == 0 ? TableWords.LeavesNothingKey : null,
                                               l => Edit("fight", d => FightIn(d, id).Loot = l));
            for (int i = 1; i < loots.Count; i++) leaves.SetItemText(i, Draft.Tables.NameOf("loot", loots[i]));
            Button makeLoot = Ui.Button(TableWords.MakeLootKey, () =>
            {
                string made = Editor.NewLoot(Draft.Tables.NameOf("fight", id) + " " + Ui.Say(TableWords.LeavesKey).ToLowerInvariant());
                Editor.Edit("fight", d => FightIn(d, id).Loot = made);
                _openCard = "loot:" + made;
                GoThere(WorkshopTab.Loot);
                Redraw();
            });
            makeLoot.Name = "MakeLoot_" + id;
            form.AddChild(Forms.Field(TableWords.LeavesKey, Ui.Row(8, leaves, makeLoot), null));

            // the GM's line when it starts
            form.AddChild(Forms.Field(TableWords.LineKey,
                Forms.Line("FightLine_" + id, fight.Line, t => Edit("fight.line", d => FightIn(d, id).Line = t, true)), TableWords.LineHintKey));

            // 9. the placement check
            form.AddChild(Placement(id, fight));

            // Try this fight (L's Play this map, with this fight's monsters and loot: Q's test hero panel)
            Button tryIt = Ui.Button(TableWords.TryFightKey, () => TryFight(id));
            tryIt.Name = "TryFight_" + id;
            form.AddChild(tryIt);
        }

        // one monster in the fight: how many (a number, or dice in Advanced), leader, take it out
        HBoxContainer Band(string fight, int at, BandDraft band, Action changed)
        {
            Label name = Ui.Plain(Pickers.MonsterName(Things.Bestiary, band.Monster));
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Control count = Pickers.Dice($"Count_{fight}_{at}", band.Count, _advanced, t =>
            {
                if (!Pickers.IsDice(t)) return;
                Edit("fight.count", d => FightIn(d, fight).Monsters[at].Count = t == "1" ? "" : t, true);
                changed();
            });

            CheckBox leader = Forms.Tick($"Leader_{fight}_{at}", TableWords.LeaderKey, band.Leader, on => Change(() => Editor.Edit("fight", d =>
            {
                for (int i = 0; i < FightIn(d, fight).Monsters.Count; i++) FightIn(d, fight).Monsters[i].Leader = on && i == at;
            })));

            var row = Ui.Row(8, name, Ui.Label(TableWords.HowManyKey), count, leader,
                             Ui.Button(WorkshopWords.RemoveKey, () => Change(() => Editor.Edit("fight", d => FightIn(d, fight).Monsters.RemoveAt(at)))));
            row.Name = $"Band_{fight}_{at}";
            return row;
        }

        // THE BUDGET METER, drawn again in place: a bar, its colour, the XP and the enemies, and the one sentence
        void Meter(VBoxContainer meter, string id)
        {
            Ui.Clear(meter);

            EncounterDraft fight = FightIn(Draft, id);
            if (fight == null) return;

            FightMeter reading = FightMeter.Of(fight, Things.Bestiary.Find);
            Color colour = reading.Colour switch
            {
                MeterColour.Green => new Color(0.25f, 0.6f, 0.25f),
                MeterColour.Amber => new Color(0.85f, 0.6f, 0.1f),
                _ => new Color(0.75f, 0.15f, 0.1f),
            };

            var bar = new ProgressBar
            {
                Name = "Bar",
                MinValue = 0,
                MaxValue = Math.Max(reading.Budget * 2, reading.Xp),
                Value = reading.Xp,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(Ui.Px(260), Ui.Px(14)),
            };
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = colour });

            Label sentence = Ui.Label(reading.Sentence.Key, reading.Sentence.Args.Select(a => a is string k ? Ui.Say(k) : a).ToArray());
            sentence.Name = "Sentence";
            sentence.AddThemeColorOverride("font_color", colour);

            meter.AddChild(Ui.Row(12, bar, Ui.Label(TableWords.MeterXpKey, reading.Xp, reading.Budget), Ui.Label(TableWords.MeterEnemiesKey, reading.Enemies, reading.Cap)));
            meter.AddChild(sentence);
            meter.SetMeta("colour", reading.Colour.ToString());
        }

        // the map has a place for every monster the fight can bring, or a warning with Open the map
        Control Placement(string id, EncounterDraft fight)
        {
            if (fight.Map.Length == 0) return new Control();

            int spawns = Pickers.Spawns(Editor.Maps, fight.Map);
            int most = FightMeter.Of(fight, Things.Bestiary.Find).Enemies;

            if (spawns >= most)
            {
                Label fine = Forms.Hint(TableWords.SpawnsOkKey, spawns);
                fine.Name = "Placement_" + id;
                return fine;
            }

            Label short_ = Ui.Label(TableWords.SpawnsShortKey, spawns, most);
            short_.AddThemeColorOverride("font_color", new Color(0.85f, 0.6f, 0.1f));
            var row = Ui.Row(8, short_, Ui.Button(TableWords.OpenMapKey, () => OpenMap(fight.Map)));
            row.Name = "Placement_" + id;
            return row;
        }

        // "Make a new map" from a fight: L's builder, on a map named for the fight, and back here after
        void MakeMapFor(string id)
        {
            string made = Editor.MakeMap(Draft.Tables.NameOf("fight", id), Content.Maps.MapTemplates.Standard, out string problem);

            if (made == null)
            {
                GD.PushWarning($"workshop: the map could not be made - {problem}");
                return;
            }

            Editor.Edit("fight", d => FightIn(d, id).Map = made);
            _openCard = "fight:" + id;
            Tab = WorkshopTab.Fights;
            OpenMap(made);
        }
    }
}
