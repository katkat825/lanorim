using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Core.Magic;
using Core.Space;
using Core.Tables;

namespace Content.Campaigns
{
    // A CAMPAIGN FOLDER, READ. One manifest and a folder per kind of content; everything in it is
    // scoped to the campaign's id, so two campaigns may both have a "goblin" and neither wins.
    //
    // ADAPTED from the old build's Package. The vessel is the same - name the folders as constants,
    // read each with the reader that owns that kind, collect problems rather than throwing, and
    // refuse the pack rather than half-load it. What went is the World layer: places/, entities/,
    // quests/, roads/ and companions/ were the abandoned direction, and kits/ was the homebrew
    // ability system that core/Magic replaced. What a chapter walks through now is maps.
    //
    // A FOLDER THIS DOES NOT KNOW IS A PROBLEM, NOT A SHRUG. A Workshop author who writes
    // "monster/" instead of "monsters/" has a campaign with no monsters in it and nothing to say
    // why, so the names it does know are listed back at them.
    public sealed partial class Package
    {
        public const string MonstersFolder = "monsters";

        public const string ItemsFolder = "items";

        public const string SpellsFolder = "spells";

        public const string MapsFolder = "maps";

        public const string EncountersFolder = "encounters";

        public const string LootFolder = "loot";

        public const string LocaleFolder = "locale";

        public const string AssetsFolder = "assets";

        // named though not read yet, so a pack that ships them is told "not loaded yet" rather than
        // "there is no such thing" - the difference between a feature that is coming and a typo.
        // Dialogue is Phase 7; minis, models and audio are the campaign mini-pack, Phase 7 as well.
        public const string DialogueFolder = "dialogue";

        public const string MinisFolder = "minis";

        public const string ModelsFolder = "models";

        public const string AudioFolder = "audio";

        public const string MapExtension = ".map";

        // shops: an id, what it stocks, and optionally what it pays (inventory_decisions.md)
        public const string MerchantsFolder = "merchants";

        public static readonly string[] Folders =
        {
            MonstersFolder, ItemsFolder, SpellsFolder, MapsFolder, EncountersFolder, LootFolder,
            LocaleFolder, AssetsFolder, DialogueFolder, MinisFolder, ModelsFolder, AudioFolder,
            MerchantsFolder,
        };

        // read, but nothing is done with them yet. (dialogue/ is read by DialogueBook when a campaign
        // is played, so it is not in here)
        public static readonly string[] NotLoadedYet =
        {
            MinisFolder, ModelsFolder, AudioFolder,
        };

        Package(string folder, string id, Manifest manifest,
                IReadOnlyList<Monster> monsters, IReadOnlyList<Item> items,
                IReadOnlyList<Spell> spells, IReadOnlyDictionary<string, MapLayout> maps,
                IReadOnlyList<EncounterTable> encounters, IReadOnlyList<LootTable> loot,
                List<ContentProblem> problems)
        {
            Folder = folder ?? "";
            Id = id ?? "";
            Manifest = manifest;
            Monsters = monsters ?? Array.Empty<Monster>();
            Items = items ?? Array.Empty<Item>();
            Spells = spells ?? Array.Empty<Spell>();
            Maps = maps ?? new Dictionary<string, MapLayout>();
            Encounters = encounters ?? Array.Empty<EncounterTable>();
            Loot = new LootTables(loot);
            Problems = problems ?? new List<ContentProblem>();
        }

        public string Folder { get; }

        public string Id { get; }

        public Manifest Manifest { get; }

        public IReadOnlyList<Monster> Monsters { get; }

        public IReadOnlyList<Item> Items { get; }

        public IReadOnlyList<Spell> Spells { get; }

        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

        // what stands on each map's squares, by map id: the builder's props, which a MapLayout (the
        // rules' map) has no use for and the board draws
        public IReadOnlyDictionary<string, IReadOnlyList<Prop>> Props { get; private set; } =
            new Dictionary<string, IReadOnlyList<Prop>>();

        public IReadOnlyList<Prop> PropsOn(string map) =>
            map != null && Props.TryGetValue(map, out IReadOnlyList<Prop> props) ? props : Array.Empty<Prop>();

        public IReadOnlyList<EncounterTable> Encounters { get; }

        public EncounterTable Encounter(string id) =>
            Encounters.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        // every loot table, together, because one may roll another from a different file
        public LootTables Loot { get; }

        public LootTable LootTable(string id) => Loot.Find(id);

        public IReadOnlyList<ContentProblem> Problems { get; }

        public IEnumerable<ContentProblem> Faults => Problems.Where(p => p.IsAFault);

        public IEnumerable<ContentProblem> Cautions => Problems.Where(p => p.IsACaution);

        // a caution is a sentence about a pack that loaded, so it cannot be what makes it refused
        public bool Sound => Manifest != null && !Problems.Any(p => p.IsAFault);

        // every key this pack promises its locale answers, so the audit walks the pack rather than
        // a list somebody keeps in step by hand
        public IEnumerable<string> Keys() =>
            (Manifest?.Keys() ?? Enumerable.Empty<string>())
                .Concat(Monsters.SelectMany(m => m.Keys()))
                .Concat(Items.SelectMany(i => i.Keys()))
                .Concat(Spells.SelectMany(s => s.Keys()))
                .Concat(Encounters.SelectMany(t => t.Keys()))
                .Concat(Loot.All.SelectMany(t => t.Keys()))
                .Concat(Merchants.Select(m => m.NameKey))
                .Distinct();

        // the campaign's shops, by id
        public IReadOnlyList<MerchantDef> Merchants { get; private set; } = Array.Empty<MerchantDef>();

        public MerchantDef Merchant(string id) =>
            Merchants.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));

        public override string ToString() =>
            Manifest == null
                ? $"{Id}: unreadable, {Problems.Count} problems"
                : $"{Manifest}, {Monsters.Count} monsters, {Items.Count} items, " +
                  $"{Spells.Count} spells, {Maps.Count} maps" +
                  (Encounters.Count > 0 ? $", {Encounters.Count} encounter tables" : "") +
                  (Loot.Count > 0 ? $", {Loot.Count} loot tables" : "") +
                  (Sound ? "" : $", {Faults.Count()} faults");
    }
}
