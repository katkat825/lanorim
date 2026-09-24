using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Maps;
using Content.Monsters;
using Content.Schema;
using Content.Spells;
using Core.Magic;
using Core.Space;

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
    public sealed class Package
    {
        public const string MonstersFolder = "monsters";

        public const string ItemsFolder = "items";

        public const string SpellsFolder = "spells";

        public const string MapsFolder = "maps";

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

        public static readonly string[] Folders =
        {
            MonstersFolder, ItemsFolder, SpellsFolder, MapsFolder, LocaleFolder, AssetsFolder,
            DialogueFolder, MinisFolder, ModelsFolder, AudioFolder,
        };

        // read, but nothing is done with them yet
        public static readonly string[] NotLoadedYet =
        {
            DialogueFolder, MinisFolder, ModelsFolder, AudioFolder,
        };

        Package(string folder, string id, Manifest manifest,
                IReadOnlyList<Monster> monsters, IReadOnlyList<Item> items,
                IReadOnlyList<Spell> spells, IReadOnlyDictionary<string, MapLayout> maps,
                List<ContentProblem> problems)
        {
            Folder = folder ?? "";
            Id = id ?? "";
            Manifest = manifest;
            Monsters = monsters ?? Array.Empty<Monster>();
            Items = items ?? Array.Empty<Item>();
            Spells = spells ?? Array.Empty<Spell>();
            Maps = maps ?? new Dictionary<string, MapLayout>();
            Problems = problems ?? new List<ContentProblem>();
        }

        public string Folder { get; }

        public string Id { get; }

        public Manifest Manifest { get; }

        public IReadOnlyList<Monster> Monsters { get; }

        public IReadOnlyList<Item> Items { get; }

        public IReadOnlyList<Spell> Spells { get; }

        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

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
                .Distinct();

        public override string ToString() =>
            Manifest == null
                ? $"{Id}: unreadable, {Problems.Count} problems"
                : $"{Manifest}, {Monsters.Count} monsters, {Items.Count} items, " +
                  $"{Spells.Count} spells, {Maps.Count} maps" +
                  (Sound ? "" : $", {Faults.Count()} faults");


        // --- reading one off disk ---------------------------------------------------------------

        public static Package Read(string folder)
        {
            var problems = new List<ContentProblem>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                problems.Add(new ContentProblem(folder ?? "", "",
                    "there is no folder there to read a campaign out of"));

                return new Package(folder, "", null, null, null, null, null, problems);
            }

            string name = new DirectoryInfo(folder).Name;

            Manifest manifest = ReadManifest(folder, name, problems);

            // without a manifest nothing below can be scoped, so the pack stops here rather than
            // registering a folder full of content under no id at all
            if (manifest == null)
                return new Package(folder, name, null, null, null, null, null, problems);

            UnknownFolders(folder, problems);

            IReadOnlyList<Monster> monsters = ReadMonsters(folder, manifest.Id, problems);
            IReadOnlyList<Item> items = ReadItems(folder, manifest.Id, problems);
            IReadOnlyList<Spell> spells = ReadSpells(folder, manifest.Id, problems);
            IReadOnlyDictionary<string, MapLayout> maps = ReadMaps(folder, manifest.Id, problems);

            ChaptersNameRealMaps(manifest, maps, problems);

            return new Package(folder, manifest.Id, manifest, monsters, items, spells, maps,
                               problems);
        }

        static Manifest ReadManifest(string folder, string name, List<ContentProblem> problems)
        {
            var found = ManifestReader.FileNames
                .Select(f => Path.Combine(folder, f))
                .Where(File.Exists)
                .ToList();

            if (found.Count == 0)
            {
                problems.Add(new ContentProblem(name, "",
                    $"a campaign folder needs a {ManifestReader.PackFileName} in it - that is the " +
                    "file that says what this is"));
                return null;
            }

            // both is ambiguous, and picking one would make the other silently dead
            if (found.Count > 1)
                problems.Add(ContentProblem.Caution(name, "",
                    $"this folder has both {ManifestReader.PackFileName} and " +
                    $"{ManifestReader.FileName} - {ManifestReader.PackFileName} is the one being " +
                    "read, and the other is doing nothing"));

            string file = Path.GetFileName(found[0]);

            string text;

            try
            {
                text = File.ReadAllText(found[0]);
            }
            catch (IOException bad)
            {
                problems.Add(new ContentProblem(file, "", "could not be read - " + bad.Message));
                return null;
            }

            Read<Manifest> read = ManifestReader.Parse(text, file, name);

            problems.AddRange(read.Problems);

            return read.Value;
        }

        static void UnknownFolders(string folder, List<ContentProblem> problems)
        {
            foreach (string each in Directory.EnumerateDirectories(folder))
            {
                string name = new DirectoryInfo(each).Name;

                if (Folders.Contains(name, StringComparer.Ordinal))
                {
                    if (NotLoadedYet.Contains(name, StringComparer.Ordinal) &&
                        Directory.EnumerateFileSystemEntries(each).Any())
                        problems.Add(ContentProblem.Caution(name, "",
                            $"'{name}/' is a folder this build knows about but does not load yet - " +
                            "what is in it is being ignored, and will not be once it does"));

                    continue;
                }

                problems.Add(new ContentProblem(name, "",
                    $"'{name}/' is not something a campaign holds - it holds " +
                    $"{Vocabulary.Offer(Folders.Select(f => f + "/"))}"));
            }
        }

        // --- the content folders ----------------------------------------------------------------

        // every json file in one folder, in name order, so a load is the same every run
        static IEnumerable<(string File, string Text)> Jsons(string folder, string sub,
                                                             List<ContentProblem> problems)
        {
            string path = Path.Combine(folder, sub);

            if (!Directory.Exists(path)) yield break;

            foreach (string each in Directory.EnumerateFiles(path, "*.json")
                                             .OrderBy(f => f, StringComparer.Ordinal))
            {
                string name = sub + "/" + Path.GetFileName(each);
                string text;

                try
                {
                    text = File.ReadAllText(each);
                }
                catch (IOException bad)
                {
                    problems.Add(new ContentProblem(name, "", "could not be read - " + bad.Message));
                    continue;
                }

                yield return (name, text);
            }
        }

        static IReadOnlyList<Monster> ReadMonsters(string folder, string campaign,
                                                   List<ContentProblem> problems)
        {
            var all = new List<Monster>();

            foreach ((string file, string text) in Jsons(folder, MonstersFolder, problems))
            {
                MonsterReader.TryRead(text, out IReadOnlyList<Monster> read,
                                      out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, m => m.Id, MonstersFolder, campaign, problems);
        }

        static IReadOnlyList<Item> ReadItems(string folder, string campaign,
                                             List<ContentProblem> problems)
        {
            var all = new List<Item>();

            foreach ((string file, string text) in Jsons(folder, ItemsFolder, problems))
            {
                ItemReader.TryRead(text, out IReadOnlyList<Item> read,
                                   out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, i => i.Id, ItemsFolder, campaign, problems);
        }

        static IReadOnlyList<Spell> ReadSpells(string folder, string campaign,
                                               List<ContentProblem> problems)
        {
            var all = new List<Spell>();

            foreach ((string file, string text) in Jsons(folder, SpellsFolder, problems))
            {
                SpellReader.TryRead(text, out IReadOnlyList<Spell> read,
                                    out IReadOnlyList<string> trouble);

                foreach (string one in trouble)
                    problems.Add(new ContentProblem(file, "", one));

                all.AddRange(read);
            }

            return Scoped(all, s => s.Id, SpellsFolder, campaign, problems);
        }

        // EVERY ID A CAMPAIGN DEFINES IS ITS OWN. A pack that ships a "goblin" must not shadow the
        // SRD's, so ids are read locally and checked here rather than being written scoped in the
        // file - an author should not have to spell their own campaign's name on every line.
        static IReadOnlyList<T> Scoped<T>(List<T> all, Func<T, string> idOf, string where,
                                          string campaign, List<ContentProblem> problems)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<T>();

            foreach (T one in all)
            {
                string id = idOf(one) ?? "";

                if (!ContentId.IsLocal(id))
                {
                    problems.Add(new ContentProblem(where, id,
                        $"'{id}' is not an id a campaign may define - lowercase a-z, 0-9 and " +
                        "underscore, and no dots. The campaign's own name is added for you"));
                    continue;
                }

                if (!seen.Add(id))
                {
                    problems.Add(new ContentProblem(where, id,
                        $"'{id}' is defined twice in {where}/"));
                    continue;
                }

                kept.Add(one);
            }

            return kept;
        }

        static IReadOnlyDictionary<string, MapLayout> ReadMaps(string folder, string campaign,
                                                               List<ContentProblem> problems)
        {
            var maps = new Dictionary<string, MapLayout>(StringComparer.Ordinal);

            string path = Path.Combine(folder, MapsFolder);

            if (!Directory.Exists(path)) return maps;

            foreach (string each in Directory.EnumerateFiles(path, "*" + MapExtension)
                                             .OrderBy(f => f, StringComparer.Ordinal))
            {
                string file = MapsFolder + "/" + Path.GetFileName(each);
                string id = Path.GetFileNameWithoutExtension(each);

                if (!ContentId.IsLocal(id))
                {
                    problems.Add(new ContentProblem(file, "",
                        $"'{id}' is not a map id - a map is named by its file, so the file name is " +
                        "lowercase a-z, 0-9 and underscore"));
                    continue;
                }

                string text;

                try
                {
                    text = File.ReadAllText(each);
                }
                catch (IOException bad)
                {
                    problems.Add(new ContentProblem(file, "", "could not be read - " + bad.Message));
                    continue;
                }

                if (!MapDraft.TryRead(text, out MapDraft draft, out string problem))
                {
                    problems.Add(new ContentProblem(file, "", problem));
                    continue;
                }

                if (!draft.Sound)
                {
                    foreach (string one in draft.Problems())
                        problems.Add(new ContentProblem(file, "", one));

                    continue;
                }

                if (maps.ContainsKey(id))
                {
                    problems.Add(new ContentProblem(file, "", $"there are two maps called '{id}'"));
                    continue;
                }

                maps[id] = draft.Layout();
            }

            return maps;
        }

        // a chapter that names a map nobody shipped is a campaign that stops mid-play, so it is
        // caught at load rather than at the moment the player walks into it
        static void ChaptersNameRealMaps(Manifest manifest,
                                         IReadOnlyDictionary<string, MapLayout> maps,
                                         List<ContentProblem> problems)
        {
            foreach (Chapter chapter in manifest.Chapters)
                foreach (string map in chapter.Maps)
                    if (!maps.ContainsKey(map))
                        problems.Add(new ContentProblem(
                            ManifestReader.PackFileName, $"chapters.{chapter.Id}",
                            $"chapter '{chapter.Id}' is played on '{map}' and there is no " +
                            $"{MapsFolder}/{map}{MapExtension} in this campaign"));
        }
    }
}
