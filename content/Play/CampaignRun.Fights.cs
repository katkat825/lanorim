using System;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Dialogue;
using Content.Monsters;
using Content.Saves;
using Content.Sheet;
using Core.Combat;
using Core.Resolution;
using Core.Space;
using Core.Tables;
using Library = Content.Schema.Library;
using Content.Inventory;

namespace Content.Play
{
    public sealed partial class CampaignRun
    {
        TableRoll _lastEncounter;

        // <<fight X>>: an encounter table called X is consulted behind the screen; otherwise X is a
        // fight entry in one of the campaign's tables (or the one an <<encounter>> just picked)
        FightCall Call(string id)
        {
            EncounterTable table = Pack.Encounter(id);

            TableRoll rolled = table != null ? Screen.Consult(table) : null;

            if (rolled == null || !rolled.Fights)
            {
                if (_lastEncounter != null && (_lastEncounter.Entry?.Id == id || _lastEncounter.Table.Id == id))
                    rolled = _lastEncounter;
            }

            if (rolled == null || !rolled.Fights)
            {
                EncounterEntry entry = Pack.Encounters.SelectMany(t => t.Entries)
                                           .FirstOrDefault(e => e.Id == id && e.Kind == EntryKind.Fight);

                if (entry == null) return null;

                var group = entry.Monsters
                                 .Select(b => new Mustered(b.Monster, Math.Max(1, b.Count.Roll(_gm))))
                                 .ToList();

                return Framed(entry, group);
            }

            _lastEncounter = null;

            return Framed(rolled.Entry, rolled.Group);
        }

        FightCall Framed(EncounterEntry entry, IReadOnlyList<Mustered> group)
        {
            string map = entry.Map.Length > 0 ? entry.Map : MapId;

            Pack.Maps.TryGetValue(map ?? "", out MapLayout layout);

            if (layout == null && Pack.Maps.Count > 0)
            {
                map = Pack.Maps.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
                layout = Pack.Maps[map];
            }

            return new FightCall { Entry = entry, Group = group, MapId = map, Map = layout };
        }

        Monster Find(string id) =>
            Pack.Monsters.FirstOrDefault(m => m.Id == id) ?? Library.Bestiary.Find(id);

        // the battle for the fight the story called: the group on the map's spawns
        public Battle BattleFor(IResolver resolver, IReactionChooser chooser = null,
                                ICombatObserver observer = null)
        {
            if (Fight?.Map == null) return null;

            var foes = new List<Battle.Foe>();
            int slot = 1;

            foreach (Mustered band in Fight.Group)
            {
                Monster monster = Find(band.Monster);

                if (monster == null) continue;

                for (int i = 0; i < band.Count; i++, slot++)
                    foes.Add(new Battle.Foe(monster,
                                            Fight.Map.SpawnAt(slot) ?? Fight.Map.SpawnAt(1) ??
                                            new Cell(Fight.Map.Columns - 2, 1)));
            }

            var observers = new Observers(observer, Day);

            Battle battle = Battle.Set(Library, Fight.Map, Hero, foes, resolver, chooser, observers);

            Day.Knows(battle.What);

            return battle;
        }

        // the combat screen is done: the story reads $fight, a win opens the fight's loot, and a
        // loss is the death screen - it does not go back into the story
        public Scene EndFight(Outcome outcome)
        {
            if (Now != Scene.Fight) return Now;

            MapId = Fight?.MapId ?? MapId;
            Hero.FightOver();

            if (outcome == Outcome.HeroesLost)
                return Now = Scene.Dead;

            Spoils = null;

            if (outcome == Outcome.HeroesWon && Fight?.Loot.Length > 0 &&
                Pack.LootTable(Fight.Loot) is LootTable table)
            {
                LootRoll roll = Screen.Open(table, Pack.Loot, Items, Hero.Class.Id, Hero.Level);
                Spoils = Content.Inventory.Spoils.Hand(Hero.Pack, roll, Items);
            }

            Fight = null;
            _settled.Clear();

            Reply(new Answer { Outcome = outcome });

            if (outcome == Outcome.HeroesWon) Autosave(SaveKind.FightWon);

            return Settle();
        }

        // the shop screen was closed
        public Scene LeaveShop()
        {
            if (Now != Scene.Shop) return Now;

            Shop = null;
            Reply(new Answer());

            return Settle();
        }
    }
}
