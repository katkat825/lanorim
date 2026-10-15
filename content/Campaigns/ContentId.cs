using System;
using System.Linq;
using Core.Localization;

namespace Content.Campaigns
{
    public static class ContentId
    {
        public const char Separator = '.';

        // held to one key segment so a bad folder name is refused at load, not in a later locale audit
        public static bool IsCampaign(string campaign) => IsLocal(campaign);

        // THE ID RULE, everywhere (Json.IsId is this): one key segment, lowercase a-z, 0-9 and underscore
        public static bool IsLocal(string id) => !string.IsNullOrEmpty(id) && IsSegment(id);

        static bool IsSegment(string s) =>
            s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        public static string Scoped(string campaign, string id)
        {
            if (!IsCampaign(campaign))
                throw new ArgumentException($"'{campaign}' is not a campaign id.", nameof(campaign));

            if (!IsLocal(id))
                throw new ArgumentException($"'{id}' is not a content id.", nameof(id));

            return campaign + Separator + id;
        }

        public static bool IsScoped(string id) =>
            !string.IsNullOrEmpty(id) && id.IndexOf(Separator) > 0;

        public static string LocalOf(string id)
        {
            if (!IsScoped(id)) return id ?? "";

            return id.Substring(id.IndexOf(Separator) + 1);
        }

        public static string ActorName(string id) => KeyConventions.ActorName(id);

        // AN ID MADE FROM A NAME (cc_task_m 3.1; WORKSHOP_DESIGN.md principle 2, "ids are made from names"): lowercase,
        // accents dropped, every run of anything else one underscore, none at either end; `otherwise` when nothing is left
        public static string From(string name, string otherwise)
        {
            var id = new System.Text.StringBuilder();

            foreach (char c in (name ?? "").Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;

                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) id.Append(c);
                else if (id.Length > 0 && id[^1] != '_') id.Append('_');
            }

            string made = id.ToString().TrimEnd('_');

            return made.Length > 0 ? made : otherwise;
        }

        // THE FIRST FREE ONE: the id itself, else id_2, id_3... - what a copy, a new campaign or a new chapter is called
        // when its name is taken (MapFiles.FreeId, the wizard, the chapter list)
        public static string Free(string id, Func<string, bool> taken)
        {
            if (!taken(id)) return id;

            for (int n = 2; ; n++)
                if (!taken($"{id}_{n}")) return $"{id}_{n}";
        }
    }
}
