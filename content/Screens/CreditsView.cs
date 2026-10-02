using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Words;

namespace Content.Screens
{
    // THE CREDITS PAGE, AS LOGIC (cc_task_f Part 3): content/srd/credits/credits.json, read with EntryList, in the
    // order the page shows it - the SRD's notice first, then that Lanorim isn't affiliated with Wizards of the Coast,
    // who made it, the assets, the fonts, the code, and the campaigns' contributors when there are any. Only what ships
    public sealed class CreditsView
    {
        public const string File = "credits/credits.json";

        public static readonly IReadOnlyList<string> EntryKeys = new[]
        {
            "id", "kind", "name", "by", "role", "licence", "url", "ships", "packs", "files", "text", "note",
        };

        static readonly EntryList<Credit> Reader = new EntryList<Credit>("credits", "credit", EntryKeys, ReadOne);

        CreditsView(IReadOnlyList<Credit> all) => All = all;

        static CreditsView _srd;

        public static CreditsView Srd() => _srd ??= Read(Schema.Srd.Read(File));

        public static CreditsView Read(string text)
        {
            if (!Reader.TryRead(text, out IReadOnlyList<Credit> all, out IReadOnlyList<string> problems))
                throw new InvalidOperationException("the credits do not read: " + string.Join("; ", problems));

            return new CreditsView(all);
        }

        public static bool TryRead(string text, out IReadOnlyList<Credit> all, out IReadOnlyList<string> problems) =>
            Reader.TryRead(text, out all, out problems);

        static Credit ReadOne(JsonElement entry, string id, List<string> problems)
        {
            if (!EnumWords.TryParse(entry.Text("kind"), out CreditKind kind))
                problems.Add($"{id}: kind is one of {string.Join(", ", EnumWords.Names<CreditKind>())}");

            if (!EnumWords.TryParse(entry.Text("role"), out CreditRole role))
                problems.Add($"{id}: role is one of {string.Join(", ", EnumWords.Names<CreditRole>())}");

            CreditLicence? licence = null;

            if (entry.Has("licence"))
            {
                if (EnumWords.TryParse(entry.Text("licence"), out CreditLicence read)) licence = read;
                else problems.Add($"{id}: licence is one of {string.Join(", ", EnumWords.Names<CreditLicence>())}");
            }

            if (string.IsNullOrWhiteSpace(entry.Text("name"))) problems.Add($"{id}: every credit has a name");

            return new Credit
            {
                Id = id,
                Kind = kind,
                Name = entry.Text("name"),
                By = entry.Text("by"),
                Role = role,
                Licence = licence,
                Url = entry.Text("url"),
                Ships = entry.Flag("ships", true),
                Packs = entry.Strings("packs"),
                Files = entry.Strings("files"),
                Text = entry.Text("text"),
            };
        }

        public IReadOnlyList<Credit> All { get; }

        public IEnumerable<Credit> Shipping => All.Where(c => c.Ships);

        public Credit Ruleset => All.First(c => c.Kind == CreditKind.Ruleset);

        // the page's sections after the ruleset, in order, each with what ships in it; an empty one is left off
        public IEnumerable<(CreditKind Kind, IReadOnlyList<Credit> Credits)> Sections =>
            new[] { CreditKind.MadeBy, CreditKind.Asset, CreditKind.Font, CreditKind.Code, CreditKind.Contributor }
                .Select(k => (k, (IReadOnlyList<Credit>)Shipping.Where(c => c.Kind == k).ToList()))
                .Where(s => s.Item2.Count > 0);

        public IReadOnlyList<Credit> Contributors => All.Where(c => c.Kind == CreditKind.Contributor).ToList();

        // the credit that covers a file in game/, by its path from res:// - every one that does
        public IEnumerable<Credit> Covering(string path) =>
            All.Where(c => c.Files.Any(f => path.StartsWith(f, StringComparison.Ordinal)));

        static string K(string thing) => ScreenKeys.Key("credits", thing);

        public static readonly string TitleKey = K("title");

        // "Lanorim isn't affiliated with, endorsed by or sponsored by Wizards of the Coast": ours, under the notice
        public static readonly string NotAffiliatedKey = K("not_affiliated");

        public static readonly string ByKey = K("by");

        public static string SectionKey(CreditKind kind) => K("section_" + EnumWords.Id(kind));

        public static string RoleKey(CreditRole role) => K("role_" + EnumWords.Id(role));

        public static string LicenceKey(CreditLicence licence) => K("licence_" + EnumWords.Id(licence));

        public static IEnumerable<string> Keys() =>
            new[] { TitleKey, NotAffiliatedKey, ByKey }
                .Concat(Enum.GetValues<CreditKind>().Select(SectionKey))
                .Concat(Enum.GetValues<CreditRole>().Select(RoleKey))
                .Concat(Enum.GetValues<CreditLicence>().Select(LicenceKey));
    }
}
