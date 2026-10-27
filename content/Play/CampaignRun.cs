using System;
using System.Collections.Generic;
using Content.Campaigns;
using Content.Dialogue;
using Content.Items;
using Content.Saves;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Core.Tables;
using Library = Content.Schema.Library;
using Content.Inventory;

namespace Content.Play
{
    // THE GAME LOOP A CAMPAIGN RUNS ON (Tier 2.8/2.12 of the 2026-09-24 run): the conversation,
    // the referee settling everything a story can ask that is not a scene, and the two scenes a
    // story hands to the table - a fight and a shop. Autosaves on the events the save model names.
    // Godot-free, so a whole campaign plays headless in a test.
    public sealed partial class CampaignRun
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

            // what the story can read about the hero, live (cc_task_i 4.1)
            _store = new StoryVariables { Live = name => HeroVariables.Read(Hero, name) };
            Talk = new Conversation(Book, _store) { NodeStarted = Entered };
            Talk.Lets(HeroVariables.HasItem, id => HeroVariables.Has(Hero, id));

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

        // A SECRET THE PLAYER HEARS (cc_task_i Part 1): an encounter table, or a <<roll ... heard>>, rolled behind
        // the screen. the table rattles the screen for it - at most once every RattleSeconds of play - and the
        // companion has its Secret bark. raised on the rules thread
        public event Action Heard;

        // the rattle's cooldown, in seconds of play: about 1-3 an hour in the sample campaign (cc_task_i Part 1)
        public const double RattleSeconds = 300;

        // THE COMPANION'S CUES FROM THE STORY (cc_task_i Part 2): a <<bark>>, or Camp for a rest. raised on the rules
        // thread; the table's CompanionMind hears them
        public event Action<Bark> Cued;

        // the hero's own d20 for a story's check or save, landed: a natural 1 or 20 is a Snag or a Maxed
        public event Action<Core.Resolution.Attempt> Tested;

        // the companion this campaign's hero has, speaking from the game's barks and this campaign's
        public BarkBank Barks => Library.Barks.For(Hero.Class.Companion, Pack.Barks);

        // WHAT THE STORY REMEMBERS, SET FROM OUTSIDE IT (cc_task_q 2.3: a Workshop test starting "as if Tavin was brave"):
        // a bool, a number or a word, into the same store the story's own <<set>> writes
        public void Remember(string variable, IConvertible value)
        {
            switch (value)
            {
                case bool flag: _store.SetValue(variable, flag); break;
                case float or double or int: _store.SetValue(variable, Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture)); break;
                default: _store.SetValue(variable, value?.ToString() ?? ""); break;
            }
        }

        // what the story remembers now: a bool, a number or a word, or null for one it hasn't set (the Workshop's live
        // memory list, a test's checks)
        public object Recall(string variable) =>
            _store.Flags.TryGetValue(variable, out bool flag) ? flag
            : _store.Numbers.TryGetValue(variable, out float number) ? number
            : _store.Words.TryGetValue(variable, out string word) ? word
            : null;

        public override string ToString() => $"{Pack.Id} at {Talk.Node}: {Now}";
    }
}
