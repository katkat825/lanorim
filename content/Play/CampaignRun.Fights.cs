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

            if (table?.Visibility == Visibility.Hidden) Heard?.Invoke();

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

        // XP LEVELLING (cc_task_i 3.4): a won fight's XP is its monsters', the SRD's Experience Points by Challenge
        // Rating; a solo hero takes it all
        public int FightXp =>
            Fight?.Group.Sum(band => Find(band.Monster) is Monster monster ? Encounters.FightBudget.Xp(monster) * band.Count : 0) ?? 0;

        Monster Find(string id) =>
            Pack.Monsters.FirstOrDefault(m => m.Id == id) ?? Library.Bestiary.Find(id);

        // the battle being fought, for what EndFight asks of it (did they break?)
        Battle _battle;

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

                // the group is mustered from the entry's bands in order, so the leader's band is found by its place
                bool leads = Fight.Entry?.Monsters.ElementAtOrDefault(Fight.Group.ToList().IndexOf(band))?.Leader == true;

                for (int i = 0; i < band.Count; i++, slot++)
                    foes.Add(new Battle.Foe(monster,
                                            Fight.Map.SpawnAt(slot) ?? Fight.Map.SpawnAt(1) ??
                                            new Cell(Fight.Map.Columns - 2, 1), leads && i == 0));
            }

            var observers = new Observers(observer, Day);

            Battle battle = Battle.Set(Library, Fight.Map, Hero, foes, resolver, chooser, observers)
                                  .Holding(Fight.Entry?.Morale ?? Morale.None);

            // a bout that continues after defeat and has no way off the map can be yielded (cc_task_i 3.2)
            battle.Fight.Yieldable = DefeatHere == Defeat.Continues && !battle.Fight.Field.Exits.Any();

            _battle = battle;

            Day.Knows(battle.What);

            return battle;
        }

        // what losing the fight being fought means: its own word, else the campaign's (cc_task_i 3.1)
        public Defeat DefeatHere => (Pack.Manifest?.Rules ?? Campaigns.CampaignRules.Default).For(Fight?.Entry);

        // the combat screen is done: the story reads $fight, a win opens the fight's loot and (in a campaign that
        // levels by XP) gives its monsters' XP, and a loss is the death screen - it does not go back into the story -
        // unless defeat continues here (cc_task_i 3.1): then the hero comes round and the story hears "lost"
        public Scene EndFight(Outcome outcome)
        {
            if (Now != Scene.Fight) return Now;

            MapId = Fight?.MapId ?? MapId;
            Hero.FightOver();

            if (outcome == Outcome.HeroesLost && DefeatHere != Defeat.Continues)
                return Now = Scene.Dead;

            // a knockout, not death: brought round on 1 hit point, and what the story does next is the story's
            if (outcome == Outcome.HeroesLost && (Hero.Actor.IsDead || Hero.Actor.IsDown)) Hero.Actor.Raise();

            if (outcome == Outcome.HeroesWon && Pack.Manifest?.Rules.Leveling == Campaigns.Leveling.Xp)
                Hero.Earn(FightXp);

            Spoils = null;

            if (outcome == Outcome.HeroesWon && Fight?.Loot.Length > 0 &&
                Pack.LootTable(Fight.Loot) is LootTable table)
            {
                LootRoll roll = Screen.Open(table, Pack.Loot, Items, Hero.Class.Id, Hero.Level);
                Spoils = Content.Inventory.Spoils.Hand(Hero.Pack, roll, Items);
            }

            Fight = null;
            _settled.Clear();

            // won because they broke: the story reads $fight_broke (cc_task_h 3.4)
            Reply(new Answer { Outcome = outcome, Broke = outcome == Outcome.HeroesWon && _battle?.Fight.Broken == true });
            _battle = null;

            if (outcome == Outcome.HeroesWon) Autosave(SaveKind.FightWon);

            return Settle();
        }

        // the shop screen was closed
        public Scene LeaveShop()
        {
            if (Now != Scene.Shop) return Now;

            Shop = null;

            // the starting shop: what was bought goes on where nothing is worn yet, and the story begins
            if (_startAfterShop != null)
            {
                string node = _startAfterShop;

                _startAfterShop = null;
                Hero.ShopsFirst = false;
                Hero.WearTheBest();

                return Start(node);
            }

            Reply(new Answer());

            return Settle();
        }
    }
}
