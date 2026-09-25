using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.Saves
{
    // THE SAVE MODEL (decisions_checklist.md section 3, Tier 2.7 of the 2026-09-24 run):
    //
    // - AUTOSAVE ON EVENTS - a chapter starting, a fight starting, a fight won, a rest, a level -
    //   and MANUAL saves whenever the player asks.
    // - "SAVE ALL IF FEASIBLE, ELSE LAST 10": a save is a few kilobytes of JSON, so every manual save
    //   is kept; autosaves are many and nobody reaches for the eleventh-oldest, so each character
    //   keeps its last ten.
    // - RELOAD ON DEATH: the newest save for the character - which, because a fight always autosaves
    //   as it starts, is the moment before the fight that killed them.
    // - FIVE CHARACTERS PER CAMPAIGN (section 2): five slots, 0 to 4.
    //
    // One folder per campaign under the root; a file's name says its slot, its kind and when.
    public sealed class SaveLibrary
    {
        public const int CharactersPerCampaign = 5;

        public const int AutosavesKept = 10;

        readonly Func<DateTime> _clock;

        public SaveLibrary(string root, Func<DateTime> clock = null)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public string Root { get; }

        string FolderOf(string campaign) => Path.Combine(Root, Safe(campaign));

        static string Safe(string id) =>
            string.IsNullOrWhiteSpace(id)
                ? "_no_campaign"
                : new string(id.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_')
                               .ToArray());

        // WRITE ONE. An autosave prunes that character's autosaves to the last ten; a manual save
        // is kept until the player deletes it
        public string Save(SaveGame game, SaveKind kind, string label = "")
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            if (game.Slot < 0 || game.Slot >= CharactersPerCampaign)
                throw new ArgumentOutOfRangeException(nameof(game),
                    $"slot {game.Slot} - a campaign has {CharactersPerCampaign} character slots");

            game.Kind = kind;
            game.Label = kind == SaveKind.Manual ? label ?? "" : "";

            DateTime now = _clock();
            string file = $"{game.Slot}_{Vocabulary(kind)}_{now:yyyyMMdd-HHmmss-fff}";

            string path = SaveShelf.Write(FolderOf(game.Campaign), file, game);

            File.SetLastWriteTimeUtc(path, now);

            if (kind != SaveKind.Manual) Prune(game.Campaign, game.Slot);

            return path;
        }

        static string Vocabulary(SaveKind kind) => Schema.Vocabulary.NameOf(kind);

        void Prune(string campaign, int slot)
        {
            foreach (SaveShelf.Saved old in Of(campaign, slot)
                                             .Where(s => s.Game.Kind != SaveKind.Manual)
                                             .Skip(AutosavesKept)
                                             .ToList())
                File.Delete(old.Path);
        }

        // every save for one character in one campaign, newest first
        public IReadOnlyList<SaveShelf.Saved> Of(string campaign, int slot) =>
            SaveShelf.Read(FolderOf(campaign)).Saves.Where(s => s.Game.Slot == slot).ToList();

        // the character slots in use in a campaign, each with its newest save
        public IReadOnlyDictionary<int, SaveShelf.Saved> Characters(string campaign)
        {
            var slots = new SortedDictionary<int, SaveShelf.Saved>();

            foreach (SaveShelf.Saved saved in SaveShelf.Read(FolderOf(campaign)).Saves)
                if (!slots.ContainsKey(saved.Game.Slot))
                    slots[saved.Game.Slot] = saved;

            return slots;
        }

        // the first empty slot, or -1 when all five are taken
        public int FreeSlot(string campaign)
        {
            IReadOnlyDictionary<int, SaveShelf.Saved> used = Characters(campaign);

            for (int slot = 0; slot < CharactersPerCampaign; slot++)
                if (!used.ContainsKey(slot)) return slot;

            return -1;
        }

        // the save to go back to when the hero dies: the newest for that character
        public SaveShelf.Saved ForReload(string campaign, int slot) => Of(campaign, slot).FirstOrDefault();

        // the continue button: the newest save anywhere
        public SaveShelf.Saved Newest()
        {
            if (!Directory.Exists(Root)) return null;

            return Directory.EnumerateDirectories(Root)
                            .SelectMany(d => SaveShelf.Read(d).Saves)
                            .OrderByDescending(s => s.Written)
                            .FirstOrDefault();
        }

        // a character retired: every save it has, gone - the slot is free again
        public int Forget(string campaign, int slot)
        {
            List<SaveShelf.Saved> all = Of(campaign, slot).ToList();

            foreach (SaveShelf.Saved saved in all) File.Delete(saved.Path);

            return all.Count;
        }

        public bool Delete(SaveShelf.Saved saved)
        {
            if (saved == null || !File.Exists(saved.Path)) return false;

            File.Delete(saved.Path);
            return true;
        }
    }
}
