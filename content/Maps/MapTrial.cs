using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Encounters;
using Content.Monsters;
using Content.Screens;
using Core.Space;
using Core.Tables;

namespace Content.Maps
{
    // PLAY THIS MAP (cc_task_l Part 5; the sketch in docs/deferred.md, under cc_task_f): a test fight on the map being
    // built. The hero (one of the classes at a level, made with every default: QuickHero), who stands at each spawn (a
    // monster from the roster, a goblin unless changed), and the solo fight budget's readout for that level as warnings,
    // never refusals (FightBudget). Write puts a throwaway campaign in a folder of its own: the map, one fight of those
    // monsters on it, and a one-line story that starts the fight. Nothing is written into a real campaign
    public sealed class MapTrial
    {
        public const string CampaignId = "map_trial";
        public const string ChapterId = "trial";
        public const string MapId = "trial_map";
        public const string FightId = "trial_fight";

        public const string Standard = "goblin";

        readonly Dictionary<int, string> _at = new();

        public MapTrial(MapDraft draft, Schema.Library library)
        {
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            Library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public MapDraft Draft { get; }

        public Schema.Library Library { get; }

        public string Class { get; set; } = "fighter";

        int _level = 1;

        public int Level
        {
            get => _level;
            set => _level = Math.Clamp(value, 1, 20);
        }

        // the map's spawn slots, in order: who stands at each is this trial's to say
        public IReadOnlyList<int> Slots => Draft.Spawns.Keys.OrderBy(s => s).ToList();

        public string At(int slot) => _at.TryGetValue(slot, out string monster) ? monster : Standard;

        public bool Put(int slot, string monster)
        {
            if (!Draft.Spawns.ContainsKey(slot) || Library.Bestiary.Find(monster) == null) return false;

            _at[slot] = monster;
            return true;
        }

        // every monster that can be put on a spawn, the weakest first
        public IEnumerable<Monster> Roster => Library.Bestiary.All.OrderBy(m => m.Challenge).ThenBy(m => m.Id, StringComparer.Ordinal);

        // --- the readout --------------------------------------------------------------------------------------------

        public int Xp => Slots.Select(At).Select(Library.Bestiary.Find).Where(m => m != null).Sum(FightBudget.Xp);

        public int Enemies => Slots.Count;

        public int Budget(EncounterDifficulty difficulty) => FightBudget.BudgetFor(Level, difficulty);

        public int Cap => FightBudget.Cap(Level);

        // the readout, and what it warns of: over a high fight's budget for one hero of this level, over the solo cap
        public Said Readout => new(ReadoutKey, Xp, Budget(EncounterDifficulty.Low), Budget(EncounterDifficulty.Moderate),
                                   Budget(EncounterDifficulty.High), Level);

        public IEnumerable<Said> Warnings
        {
            get
            {
                if (Xp > Budget(EncounterDifficulty.High)) yield return new Said(OverBudgetKey, Xp, Level);
                if (Enemies > Cap) yield return new Said(OverCapKey, Enemies, Cap, Level);
            }
        }

        // what stops it: a map with problems (its campaign wouldn't load it), or no spawn to put anyone on
        public string Refusal =>
            !Draft.Sound ? ProblemsKey
            : Slots.Count == 0 ? NoSpawnsKey
            : null;

        // --- the throwaway campaign ---------------------------------------------------------------------------------

        // the campaign, written into `folder` (made empty first): null when it was, or why not
        public string Write(string folder)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);

                Directory.CreateDirectory(Path.Combine(folder, Campaigns.Package.MapsFolder));
                Directory.CreateDirectory(Path.Combine(folder, "encounters"));
                Directory.CreateDirectory(Path.Combine(folder, "dialogue"));

                File.WriteAllText(Path.Combine(folder, "pack.json"), Manifest());
                File.WriteAllText(Path.Combine(folder, Campaigns.Package.MapsFolder, MapId + Campaigns.Package.MapExtension),
                                  Numbered().Save());
                File.WriteAllText(Path.Combine(folder, "encounters", "encounters.json"), Encounter());
                File.WriteAllText(Path.Combine(folder, "dialogue", "story.yarn"), Story);

                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }

        static string Manifest() =>
            $"{{ \"id\": \"{CampaignId}\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
            $"\"chapters\": [ {{ \"id\": \"{ChapterId}\", \"maps\": [ \"{MapId}\" ] }} ], \"start\": \"{ChapterId}\", \"starting_shop\": false }}";

        // a fight's monsters stand on spawns 1, 2, 3... in order (CampaignRun.BattleFor), so the trial's map has its
        // spawns numbered that way, each with the monster its slot was given
        MapDraft Numbered()
        {
            MapDraft.TryRead(Draft.Save(), out MapDraft copy, out _);
            copy.Palette = Draft.Palette;

            var slots = Slots;
            foreach (int slot in slots) copy.ClearSpawn(slot);
            for (int i = 0; i < slots.Count; i++) copy.PlaceSpawn(i + 1, Draft.Spawns[slots[i]]);

            return copy;
        }

        string Encounter()
        {
            var monsters = string.Join(", ", Slots.Select(s => $"{{ \"monster\": {JsonSerializer.Serialize(At(s))}, \"count\": \"1\" }}"));

            return new StringBuilder()
                .Append("{ \"tables\": [ { \"id\": \"trial\", \"entries\": [ { ")
                .Append($"\"id\": \"{FightId}\", \"kind\": \"fight\", \"map\": \"{MapId}\", ")
                .Append($"\"level\": {Level}, \"difficulty\": \"moderate\", \"monsters\": [ {monsters} ] ")
                .Append("} ] } ] }")
                .ToString();
        }

        // the one line of story: the fight
        static string Story => $"title: {ChapterId}\nspeaker: dm\n---\n<<fight {FightId}>>\n===\n";

        static readonly Func<string, string> K = ScreenKeys.For("map");

        public static readonly string PlayKey = K("play");
        public static readonly string TrialKey = K("trial");
        public static readonly string HeroKey = K("trial_hero");
        public static readonly string LevelKey = K("trial_level");
        public static readonly string AtSpawnKey = K("trial_at_spawn");
        public static readonly string ReadoutKey = K("trial_readout");
        public static readonly string OverBudgetKey = K("trial_over_budget");
        public static readonly string OverCapKey = K("trial_over_cap");
        public static readonly string ProblemsKey = K("trial_problems");
        public static readonly string NoSpawnsKey = K("trial_no_spawns");
        public static readonly string FightKey = K("trial_fight");
        public static readonly string CouldNotKey = K("trial_could_not");

        public static IEnumerable<string> Keys() => new[]
        {
            PlayKey, TrialKey, HeroKey, LevelKey, AtSpawnKey, ReadoutKey, OverBudgetKey, OverCapKey, ProblemsKey,
            NoSpawnsKey, FightKey, CouldNotKey,
        };
    }
}
