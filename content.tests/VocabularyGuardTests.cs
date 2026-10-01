using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Content.Classes;
using Content.Items;
using Content.Monsters;
using Content.Schema;
using Content.Species;
using Content.Spells;
using Core.Characters;
using Core.Magic;

namespace Content.Tests
{
    // THE GUARDS (cc_task_dedupe-effects.md, Phase 6): automatic checks, so that no duplicate setting
    // can come back by somebody forgetting. every key a reader accepts is a row in the vocabulary of
    // docs/spell_effect_reference.md and every row is a key a reader accepts; every key is in a data
    // file unless its row says it is reserved; only BoonSpecReader reads boon keys and only
    // Boon.Of makes a Boon; and no type grows a family of booleans.
    public class VocabularyGuardTests
    {
        // --- 1. the vocabulary is the registry ----------------------------------------------------

        // every key each reader accepts, by where in a file it sits. read from the readers' own
        // declared lists (the same lists they refuse unknown keys with), never from source text
        static Dictionary<string, HashSet<string>> ReaderKeys()
        {
            var keys = new Dictionary<string, HashSet<string>>();

            void Add(string where, IEnumerable<string> list) =>
                keys[where] = new HashSet<string>(list, StringComparer.Ordinal);

            IReadOnlyList<string> linger = LingerSpecReader.Keys;

            Add("spell", SpellReader.SpellKeys);
            Add("effect", SpellReader.CommonKeys.Concat(
                              PrimitiveHandlers.All.SelectMany(h => h.Keys).Where(k => !linger.Contains(k))));
            Add("effect.upcast", SpellReader.UpcastKeys);
            Add("effect.hit_points", SpellReader.HitPointKeys);
            Add("effect.extra_dice", SpellReader.ExtraDiceKeys);
            Add("effect.raises", SpellReader.RaisesKeys);
            Add("effect.item", SpellReader.ItemKeys);
            Add("linger", linger);
            Add("linger.escape", LingerSpecReader.EscapeKeys);
            Add("linger.repeat_save", LingerSpecReader.RepeatSaveKeys);
            Add("linger.gone", LingerSpecReader.GoneKeys);
            Add("boon", BoonSpecReader.Keys);
            Add("boon.mark", BoonSpecReader.MarkKeys);
            Add("boon.rewrite", BoonSpecReader.RewriteKeys);
            Add("class", ClassReader.Keys);
            Add("feature", FeatureReader.Keys);
            Add("feature.stays_up_at", FeatureReader.StaysUpAtKeys);
            Add("item", ItemReader.Keys);
            Add("item.weapon", ItemReader.WeaponKeys);
            Add("item.armor", ItemReader.ArmorKeys);
            Add("item.boon", ItemReader.BoonKeys);
            Add("species", SpeciesReader.Keys);
            Add("monster", MonsterReader.Keys);
            Add("monster.attack", MonsterReader.AttackKeys);
            Add("attack", AttackReader.Keys);
            Add("attack.on_hit", AttackReader.OnHitKeys);
            Add("form", FormReader.Keys);
            Add("form.attack", FormReader.AttackKeys);
            Add("monster.action", MonsterReader.ActionKeys);
            Add("monster.action.recharge", MonsterReader.RechargeKeys);
            Add("monster.spellcasting", MonsterReader.SpellcastingKeys);
            Add("background", BackgroundReader.Keys);
            Add("consequence", ConsequenceReader.Keys);

            return keys;
        }

        sealed class Row
        {
            public string Where;
            public string Key;
            public string UsedBy;
            public string WhyNotExisting;
        }

        static string Here([CallerFilePath] string path = "") => Path.GetDirectoryName(path);

        // the vocabulary's tables: a heading names where the keys sit ("### `effect`: ..."), and each row's
        // first cell is a key in backticks
        static List<Row> DocRows()
        {
            string doc = Path.Combine(Here(), "..", "docs", "spell_effect_reference.md");
            string[] lines = File.ReadAllLines(doc);

            // found by its name, not a number: the doc was renumbered once already (leftovers #9)
            int start = Array.FindIndex(lines, l => l.TrimEnd() == "## The vocabulary");
            Assert.True(start >= 0, "docs/spell_effect_reference.md has no '## The vocabulary' heading");

            var rows = new List<Row>();
            string where = null;

            for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("## "); i++)
            {
                Match heading = Regex.Match(lines[i], @"^###+ `([a-z_.]+)`");

                if (heading.Success)
                {
                    where = heading.Groups[1].Value;
                    continue;
                }

                Match row = Regex.Match(lines[i], @"^\| `([a-z0-9_]+)` \|");

                if (!row.Success || where == null) continue;

                // a cell may hold an escaped pipe, "a\|b"
                string[] cells = Regex.Split(lines[i], @"(?<!\\)\|").Select(c => c.Trim()).ToArray();

                rows.Add(new Row
                {
                    Where = where,
                    Key = row.Groups[1].Value,
                    UsedBy = cells.Length > 5 ? cells[5] : "",
                    WhyNotExisting = cells.Length > 6 ? cells[6] : "",
                });
            }

            return rows;
        }

        [Fact]
        public void EveryKeyAReaderAcceptsIsARowInSectionSixAndEveryRowIsAKeyAReaderAccepts()
        {
            Dictionary<string, HashSet<string>> readers = ReaderKeys();
            List<Row> rows = DocRows();

            var wrong = new List<string>();

            foreach ((string where, HashSet<string> keys) in readers)
            {
                var listed = rows.Where(r => r.Where == where).Select(r => r.Key).ToHashSet();

                wrong.AddRange(keys.Except(listed).Select(k => $"{where}: '{k}' is read but has no row"));
                wrong.AddRange(listed.Except(keys).Select(k => $"{where}: '{k}' has a row but no reader takes it"));
            }

            wrong.AddRange(rows.Where(r => !readers.ContainsKey(r.Where))
                               .Select(r => $"'{r.Where}' is not a place any reader reads"));

            wrong.AddRange(rows.GroupBy(r => (r.Where, r.Key)).Where(g => g.Count() > 1)
                               .Select(g => $"{g.Key.Where}: '{g.Key.Key}' has two rows"));

            wrong.AddRange(rows.Where(r => r.WhyNotExisting.Length == 0)
                               .Select(r => $"{r.Where}: '{r.Key}' has no 'why not an existing key' cell"));

            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        }

        [Fact]
        public void TheSpecsKeysAreOnlyTakenByTheirPrimitives()
        {
            // a handler that takes a LingerSpec key takes it from LingerSpecReader's list, never its own
            var linger = new HashSet<string>(LingerSpecReader.Keys);

            foreach (IPrimitiveHandler handler in PrimitiveHandlers.All)
                Assert.DoesNotContain(handler.Keys, k => BoonSpecReader.Keys.Contains(k) && !linger.Contains(k));

            // and no two handlers own the same key: a setting shared by several is a common one
            var owned = PrimitiveHandlers.All.SelectMany(h => h.Keys.Where(k => !linger.Contains(k))
                                                                    .Select(k => (k, h.Kind)))
                                             .GroupBy(p => p.k).Where(g => g.Count() > 1)
                                             .Select(g => $"{g.Key}: {string.Join(", ", g.Select(p => p.Kind))}")
                                             .ToList();

            Assert.True(owned.Count == 0, "keys owned by two handlers:\n" + string.Join("\n", owned));

            Assert.Empty(PrimitiveHandlers.All.SelectMany(h => h.Keys).Intersect(SpellReader.CommonKeys));
        }


        // --- 2 and 3. unknown keys are errors, and every key is used -------------------------------

        [Fact]
        public void AKeyNoReaderTakesIsRefusedWithTheNearestOneNamed()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""damage"",""amount"":""1d6"",""damage_type"":""fire"",""attack_rol"":true}]}]}",
                                out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'attack_rol' is not a key here") && p.Contains("'attack_roll'"));

            // and a key that belongs to another primitive is refused on this one
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""heal"",""amount"":""1d6"",""dust"":true}]}]}",
                                out _, out problems);

            Assert.Contains(problems, p => p.Contains("'dust' is not a key here"));

            ItemReader.TryRead(@"{""items"":[{""id"":""rock"",""kind"":""trinket"",""cots"":3}]}", out _, out problems);

            Assert.Contains(problems, p => p.Contains("'cots'") && p.Contains("'cost'"));

            MonsterReader.TryRead(@"{""monsters"":[{""id"":""rat"",""hit_points"":1,""spede"":20,
              ""attacks"":[{""id"":""bite"",""damage"":""1"",""damage_type"":""piercing""}]}]}",
                                  out _, out problems);

            Assert.Contains(problems, p => p.Contains("'spede'") && p.Contains("'speed'"));
        }

        // where in a data file each place's objects sit
        static bool At(string where, IReadOnlyList<string> path, JsonElement o)
        {
            string last = path.Count > 0 ? path[^1] : "";
            string owner = path.Count > 1 ? path[^2] : "";
            bool inArray(string name) => last == "[]" && owner == name;
            bool under(string name) => last == name;

            bool effect = inArray("effects");
            bool feature = inArray("features");

            return where switch
            {
                "spell" => o.TryGetProperty("effects", out _) && o.TryGetProperty("level", out _),
                "effect" => effect,
                "linger" => effect,
                "boon" => effect && o.TryGetProperty("primitive", out JsonElement p) && p.GetString() == "sway" ||
                          inArray("boons"),
                "class" => path.Count == 2 && inArray("classes"),
                "feature" => feature,
                "item" => path.Count == 2 && inArray("items"),
                "item.boon" => inArray("boons"),
                "species" => path.Count == 2 && inArray("species"),
                "monster" => path.Count == 2 && inArray("monsters"),
                "monster.attack" => inArray("attacks"),
                "attack" => inArray("attacks") || under("weapon"),
                "form" => path.Count == 2 && inArray("forms"),
                "form.attack" => inArray("attacks"),
                "background" => path.Count == 2 && inArray("backgrounds"),
                "consequence" => path.Count == 2 && inArray("consequences"),
                "monster.action" => inArray("actions"),
                _ => under(where.Substring(where.LastIndexOf('.') + 1)) &&
                     (where.StartsWith("effect.") || where.StartsWith("linger.") || where.StartsWith("boon.") ||
                      where.StartsWith("item.") || where.StartsWith("monster.") || where.StartsWith("feature.") ||
                      where.StartsWith("attack.")),
            };
        }

        static IEnumerable<string> DataFiles()
        {
            string lanorim = Path.Combine(Here(), "..");

            foreach (string folder in new[] { "content/srd", "campaigns" })
                foreach (string file in Directory.GetFiles(Path.Combine(lanorim, folder), "*.json", SearchOption.AllDirectories))
                    yield return file;
        }

        static void Walk(JsonElement e, List<string> path, Action<IReadOnlyList<string>, JsonElement> visit)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                visit(path, e);

                foreach (JsonProperty p in e.EnumerateObject())
                {
                    path.Add(p.Name);
                    Walk(p.Value, path, visit);
                    path.RemoveAt(path.Count - 1);
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in e.EnumerateArray())
                {
                    path.Add("[]");
                    Walk(item, path, visit);
                    path.RemoveAt(path.Count - 1);
                }
        }

        [Fact]
        public void EveryKeyIsInADataFileUnlessItsRowSaysItIsReserved()
        {
            List<Row> rows = DocRows();
            var used = new HashSet<(string, string)>();

            foreach (string file in DataFiles())
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file), Json.Options);

                Walk(doc.RootElement, new List<string>(), (path, o) =>
                {
                    foreach (Row row in rows)
                        if (!used.Contains((row.Where, row.Key)) && o.TryGetProperty(row.Key, out _) &&
                            At(row.Where, path, o))
                            used.Add((row.Where, row.Key));
                });
            }

            List<string> unused = rows.Where(r => !used.Contains((r.Where, r.Key)) &&
                                                  !r.UsedBy.StartsWith("reserved", StringComparison.OrdinalIgnoreCase))
                                      .Select(r => $"{r.Where}: '{r.Key}'").ToList();

            Assert.True(unused.Count == 0,
                        "in no data file - use it, delete it, or mark its row 'reserved: <why>':\n" +
                        string.Join("\n", unused));

            List<string> stale = rows.Where(r => used.Contains((r.Where, r.Key)) &&
                                                 r.UsedBy.StartsWith("reserved", StringComparison.OrdinalIgnoreCase))
                                     .Select(r => $"{r.Where}: '{r.Key}'").ToList();

            Assert.True(stale.Count == 0, "marked reserved but used:\n" + string.Join("\n", stale));
        }


        // --- 4. one door for boons --------------------------------------------------------------------

        static string Source([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path), "..");

        static IEnumerable<string> Code(params string[] folders) =>
            folders.SelectMany(f => Directory.GetFiles(Path.Combine(Source(), f), "*.cs", SearchOption.AllDirectories))
                   .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                               !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                               !f.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar));

        [Fact]
        public void OnlyBoonOfMakesABoon()
        {
            List<string> makers = Code("core", "content", "game", "sim", "core.tests", "content.tests")
                .Where(f => Path.GetFileName(f) != "Boon.cs" && Path.GetFileName(f) != "VocabularyGuardTests.cs")
                .Where(f => File.ReadAllText(f).Contains("new Boon("))
                .ToList();

            Assert.True(makers.Count == 0, "a Boon made outside Boon.Of:\n" + string.Join("\n", makers));
        }

        [Fact]
        public void OnlyBoonSpecReaderReadsABoonsKeys()
        {
            // the keys only a boon has: its reader's, less any another reader also takes
            var others = new HashSet<string>(ReaderKeys().Where(p => !p.Key.StartsWith("boon"))
                                                         .SelectMany(p => p.Value));
            List<string> boonOnly = BoonSpecReader.Keys.Where(k => !others.Contains(k)).ToList();

            Assert.NotEmpty(boonOnly);

            var readers = new List<string>();

            foreach (string file in Code("content"))
            {
                string name = Path.GetFileName(file);

                if (name == "BoonSpecReader.cs") continue;

                string text = File.ReadAllText(file);

                foreach (string key in boonOnly.Where(k => text.Contains($"\"{k}\"")))
                    readers.Add($"{name} reads '{key}'");
            }

            Assert.True(readers.Count == 0, "boon keys read outside BoonSpecReader:\n" + string.Join("\n", readers));
        }


        // --- 5. no boolean families ---------------------------------------------------------------------

        // a pair of booleans that start with the same word is how most of the duplicates began:
        // SpeedDoubled beside SpeedHalved, NoActions beside NoReactions. each exception is here with
        // its reason
        static readonly Dictionary<string, string> AllowedFamilies = new()
        {
        };

        // every class, record, struct and interface in core/ and content/ (cc_task_dedupe-leftovers.md
        // #7): the families began in the spell records, but a flag pair can start anywhere
        static IEnumerable<Type> Records() =>
            new[] { typeof(BoonSpec).Assembly, typeof(Library).Assembly }
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsEnum && !t.Name.StartsWith("<") && !typeof(Delegate).IsAssignableFrom(t));

        // what a type sets rather than works out: its auto-properties, or everything an interface
        // asks for (a get-only property there is a setting the implementer answers)
        static IEnumerable<PropertyInfo> Settings(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(bool) &&
                            (type.IsInterface ||
                             type.GetField($"<{p.Name}>k__BackingField",
                                           BindingFlags.NonPublic | BindingFlags.Instance) != null));

        static string FirstWord(string name) => Regex.Match(name, "^[A-Z][a-z]*").Value;

        [Fact]
        public void NoRecordHasTwoBooleansThatStartWithTheSameWord()
        {
            var families = new List<string>();

            foreach (Type type in Records())
            {
                foreach (IGrouping<string, PropertyInfo> family in Settings(type).GroupBy(p => FirstWord(p.Name)))
                    if (family.Count() > 1 && !AllowedFamilies.ContainsKey($"{type.Name}.{family.Key}"))
                        families.Add($"{type.Name}: {string.Join(", ", family.Select(p => p.Name))}");
            }

            Assert.True(families.Count == 0,
                        "booleans that are one idea - make them one enum or flags:\n" + string.Join("\n", families));
        }

        // a content file that reads keys and never checks them loads a retired key as nothing. the
        // ones left out are written by the game itself, not by an author
        static readonly Dictionary<string, string> UncheckedReaders = new()
        {
            ["BoonSpecReader.cs"] = "reads a boon inside an object its caller has checked (Keyed.OnlyKnown with BoonSpecReader.Keys)",
            ["Json.cs"] = "the helpers every reader calls on an object it has checked",
            ["TableReader.cs"] = "reads a GM table its caller has checked (the encounter and loot readers)",
            ["SaveReader.cs"] = "a save file, written by SaveWriter; its versions are SaveFormat's business",
            ["GameSettings.cs"] = "the player's settings, written by the game",
            ["MapDraft.cs"] = "the map editor's own file, written by MapDraft.Save",
            ["EffectWords.cs"] = "reads an effect SpellReader.ReadEffect has checked (Keyed.OnlyKnown with KeysFor)",
            ["SrdSpellNames.cs"] = "one list of names, no keys beside it",
        };

        [Fact]
        public void EveryContentReaderChecksItsKeys()
        {
            string content = Path.Combine(Here(), "..", "content");
            var unchecked_ = new List<string>();

            // a partial class is one reader however many files it is in: SpellReader.cs and its
            // SpellReader.Effect.cs are read, and checked, as one
            foreach (IGrouping<string, string> reader in
                     Directory.GetFiles(content, "*.cs", SearchOption.AllDirectories)
                              .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                          !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                              .GroupBy(f => Path.Combine(Path.GetDirectoryName(f), Path.GetFileName(f).Split('.')[0])))
            {
                string text = string.Concat(reader.Select(File.ReadAllText));
                string name = Path.GetFileName(reader.Key) + ".cs";

                if (Regex.IsMatch(text, @"\.(Text|Number|Flag|Strings|Items|Dice)\(""[a-z_]+""") &&
                    !text.Contains("Keyed.OnlyKnown") && !text.Contains("new EntryList<") && !text.Contains(".Record(") &&
                    !UncheckedReaders.ContainsKey(name))
                    unchecked_.Add(name);
            }

            Assert.True(unchecked_.Count == 0, "readers that never check their keys:\n" + string.Join("\n", unchecked_));
        }
    }
}
