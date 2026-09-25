using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Dialogue;
using Content.Items;
using Content.Monsters;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Core.Tables;
using Yarn;
using Library = Content.Schema.Library;
using Content.Inventory;

namespace Content.Play
{
    // what the table shows next while a campaign plays
    public enum Scene
    {
        // a line of dialogue; Continue moves on
        Line,

        // options to pick from
        Choice,

        // a fight to run - the combat screen plays it and hands back the outcome
        Fight,

        // a merchant's counter - the shop screen, and back
        Shop,

        // the hero died: the death screen, and reload
        Dead,

        // the campaign's story has run out: the end screen
        Over,
    }

    // A FIGHT THE STORY CALLED FOR: who, where, and what it leaves behind
    public sealed class FightCall
    {
        public EncounterEntry Entry { get; init; }

        public IReadOnlyList<Mustered> Group { get; init; } = Array.Empty<Mustered>();

        public string MapId { get; init; } = "";

        public MapLayout Map { get; init; }

        public string Loot => Entry?.Loot ?? "";
    }

    // THE GAME LOOP A CAMPAIGN RUNS ON (Tier 2.8/2.12 of the 2026-09-24 run): the conversation,
    // the referee settling everything a story can ask that is not a scene, and the two scenes a
    // story hands to the table - a fight and a shop. Autosaves on the events the save model names.
    // Godot-free, so a whole campaign plays headless in a test.
    public sealed class CampaignRun
    {
        readonly StoryVariables _store;
        readonly SaveLibrary _saves;
        readonly IRng _gm;

        public CampaignRun(Library library, Package pack, Hero hero, IResolver heroDice, IRng gm,
                           SaveLibrary saves = null, int slot = 0)
        {
            Library = library ?? throw new ArgumentNullException(nameof(library));
            Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _gm = gm ?? new SeededRng(1);
            _saves = saves;
            Slot = slot;

            Book = DialogueBook.Read(System.IO.Path.Combine(pack.Folder, Package.DialogueFolder), pack.Id);
            Screen = new GmScreen(_gm);
            Referee = new Referee(hero, heroDice ?? new StandardResolver(_gm), Screen, library, pack);

            _store = new StoryVariables();
            Talk = new Conversation(Book, _store) { NodeStarted = Entered };

            Day = new Day(hero.Actor);
        }

        public Library Library { get; }

        public Package Pack { get; }

        public Hero Hero { get; }

        public int Slot { get; }

        public DialogueBook Book { get; }

        public Conversation Talk { get; }

        public GmScreen Screen { get; }

        public Referee Referee { get; }

        // what the day has been, for camp: fights watch it
        public Day Day { get; }

        public Scene Now { get; private set; } = Scene.Over;

        // the map the party is on, by the campaign's id - a fight with no map of its own is here
        public string MapId { get; set; } = "";

        public FightCall Fight { get; private set; }

        public MerchantDef Shop { get; private set; }

        // everything the referee settled since the last Continue - the tray shows the rolls, the
        // pack screen the loot
        public IReadOnlyList<Settled> Settled => _settled;

        readonly List<Settled> _settled = new List<Settled>();

        // the items and gold the last fight left, when it had a loot table
        public Haul Spoils { get; private set; }

        public ItemShelf Items => Library.Items;

        // --- running ---------------------------------------------------------------------------

        public Scene Start(string node = null)
        {
            node ??= Pack.Manifest?.Start ?? "";

            if (!Talk.Start(node))
            {
                Now = Scene.Over;
                return Now;
            }

            Autosave(SaveKind.ChapterStart);

            return Settle();
        }

        // the Continue button
        public Scene Next()
        {
            if (Now != Scene.Line) return Now;

            _settled.Clear();

            if (!Talk.Advance() && !Talk.IsWaiting) return Now = Scene.Over;

            return Settle();
        }

        public Scene Choose(int option)
        {
            if (Now != Scene.Choice) return Now;

            _settled.Clear();

            Choose(Talk, option);

            return Settle();
        }

        Scene Settle()
        {
            for (int guard = 0; guard < 10_000; guard++)
            {
                if (Talk.IsWaiting)
                {
                    Request request = Talk.Pending;

                    if (request.Kind == RequestKind.Fight)
                    {
                        Fight = Call(request.Id);

                        if (Fight == null)
                        {
                            // a fight the campaign does not have reads as won, and the author sees
                            // the complaint in a playthrough
                            Talk.Complained.Add($"no fight '{request.Id}' in this campaign");
                            Reply(new Answer { Outcome = Outcome.HeroesWon });
                            continue;
                        }

                        Autosave(SaveKind.FightStart);
                        return Now = Scene.Fight;
                    }

                    if (request.Kind == RequestKind.Shop)
                    {
                        Shop = Pack.Merchant(request.Id);

                        if (Shop == null)
                        {
                            Talk.Complained.Add($"no merchant '{request.Id}' in this campaign");
                            Reply(new Answer());
                            continue;
                        }

                        return Now = Scene.Shop;
                    }

                    Settled settled = Referee.Settle(request);
                    _settled.Add(settled);

                    if (request.Kind == RequestKind.Encounter && settled.Table?.Fights == true)
                        _lastEncounter = settled.Table;

                    Reply(settled.Answer ?? new Answer());

                    if (request.Kind == RequestKind.Level) Autosave(SaveKind.LevelUp);
                    if (request.Kind == RequestKind.Rest)
                    {
                        Day.Slept();
                        Autosave(SaveKind.Rest);
                    }

                    continue;
                }

                if (Talk.IsOver) return Now = Scene.Over;

                if (Talk.IsChoosing) return Now = Scene.Choice;

                if (Talk.Saying != null) return Now = Scene.Line;

                if (!Talk.Advance() && !Talk.IsWaiting) return Now = Scene.Over;
            }

            return Now = Scene.Over;
        }

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


        // --- saving ----------------------------------------------------------------------------

        public SaveGame Capture(SaveKind kind = SaveKind.Manual)
        {
            var game = new SaveGame
            {
                Campaign = Pack.Id,
                CampaignFormat = Pack.Manifest?.Format ?? 0,
                Chapter = "",
                Map = MapId ?? "",
                Hero = HeroSaves.Capture(Hero),
                Node = Talk.Node ?? "",
                Kind = kind,
                Slot = Slot,
            };

            // the variables as the node found them, and the steps taken in it since
            foreach (KeyValuePair<string, float> n in _entryNumbers) game.Numbers[n.Key] = n.Value;
            foreach (KeyValuePair<string, string> w in _entryWords) game.Words[w.Key] = w.Value;
            foreach (KeyValuePair<string, bool> f in _entryFlags) game.Flags[f.Key] = f.Value;
            foreach (string step in _steps) game.Steps.Add(step);

            return game;
        }

        public string Save(SaveKind kind = SaveKind.Manual, string label = "") =>
            _saves?.Save(Capture(kind), kind, label);

        void Autosave(SaveKind kind)
        {
            if (_saves != null) _saves.Save(Capture(kind), kind);
        }

        // BACK FROM A SAVE: the hero rebuilt, the variables put back, and the story restarted at
        // the node it was on (a node is where a story can be entered; a save mid-node replays from
        // the node's start, which is what an autosave at a chapter or a fight is anyway)
        public static CampaignRun Resume(SaveGame save, Library library, Package pack,
                                         IResolver heroDice, IRng gm, SaveLibrary saves,
                                         out IReadOnlyList<ContentProblem> problems)
        {
            Hero hero = HeroSaves.Restore(save.Hero, library, out problems);

            if (hero == null) return null;

            var run = new CampaignRun(library, pack, hero, heroDice, gm, saves, save.Slot)
            {
                MapId = save.Map,
            };

            foreach (KeyValuePair<string, float> n in save.Numbers) run._store.SetValue(n.Key, n.Value);
            foreach (KeyValuePair<string, string> w in save.Words) run._store.SetValue(w.Key, w.Value);
            foreach (KeyValuePair<string, bool> f in save.Flags) run._store.SetValue(f.Key, f.Value);

            run._resumeAt = save.Node;
            run._replay = save.Steps.ToList();

            return run;
        }

        string _resumeAt;

        List<string> _replay = new List<string>();

        // the node a resumed run starts at, replayed to the spot it was saved at
        public Scene Continue()
        {
            if (string.IsNullOrEmpty(_resumeAt)) return Start();

            if (!Talk.Start(_resumeAt)) return Now = Scene.Over;

            foreach (string step in _replay)
            {
                // lines already read are not read again
                while (!Talk.IsOver && !Talk.IsWaiting && !Talk.IsChoosing) Talk.Advance();

                if (!Replay(step))
                {
                    // the story is not the one that was saved (it was edited since): stop here and
                    // play on from wherever this is, rather than guess
                    Talk.Complained.Add($"a saved step '{step}' no longer fits node '{Talk.Node}'");
                    break;
                }
            }

            _replay.Clear();

            return Settle();
        }


        // --- where in the node ----------------------------------------------------------------

        readonly List<string> _steps = new List<string>();

        readonly Dictionary<string, float> _entryNumbers = new Dictionary<string, float>();
        readonly Dictionary<string, string> _entryWords = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _entryFlags = new Dictionary<string, bool>();

        void Entered(string node)
        {
            _steps.Clear();

            _entryNumbers.Clear();
            _entryWords.Clear();
            _entryFlags.Clear();

            foreach (KeyValuePair<string, float> n in _store.Numbers) _entryNumbers[n.Key] = n.Value;
            foreach (KeyValuePair<string, string> w in _store.Words) _entryWords[w.Key] = w.Value;
            foreach (KeyValuePair<string, bool> f in _store.Flags) _entryFlags[f.Key] = f.Value;
        }

        const string ChoseStep = "choose";
        const string AnsweredStep = "answer";

        void Choose(Conversation talk, int option)
        {
            _steps.Add($"{ChoseStep}:{option}");
            talk.Choose(option);
        }

        // the story's answer, noted so a load can give it again without rolling it again
        void Reply(Answer answer)
        {
            _steps.Add(string.Join(":", AnsweredStep, answer.Passed ? 1 : 0, answer.Total,
                                   answer.Natural, answer.DrawsConsequence ? 1 : 0, answer.Entry ?? "",
                                   answer.StartsAFight ? 1 : 0, (int)answer.Outcome, answer.Gold));
            Talk.Answer(answer);
        }

        bool Replay(string step)
        {
            string[] parts = step.Split(':');

            if (parts[0] == ChoseStep && parts.Length == 2 && Talk.IsChoosing &&
                int.TryParse(parts[1], out int option))
            {
                Choose(Talk, option);
                return true;
            }

            if (parts[0] == AnsweredStep && parts.Length == 9 && Talk.IsWaiting)
            {
                int Int(int i) => int.TryParse(parts[i], out int v) ? v : 0;

                Reply(new Answer
                {
                    Passed = Int(1) == 1,
                    Total = Int(2),
                    Natural = Int(3),
                    DrawsConsequence = Int(4) == 1,
                    Entry = parts[5],
                    StartsAFight = Int(6) == 1,
                    Outcome = (Outcome)Int(7),
                    Gold = Int(8),
                });

                return true;
            }

            return false;
        }

        public override string ToString() => $"{Pack.Id} at {Talk.Node}: {Now}";
    }
}
