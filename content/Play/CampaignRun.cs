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

        public override string ToString() => $"{Pack.Id} at {Talk.Node}: {Now}";
    }
}
