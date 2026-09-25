using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Campaigns
{
    // WHICH GAME THIS BUILD IS. The demo is the same program with less on the shelf
    // (v1_build_order.md, Phase D): one tutorial and one one-shot, no achievements, and saves the
    // full game can pick up. Everything that differs is decided here, from data, so there is no
    // second codebase to keep in step - the Godot export for the demo sets a feature tag, the game
    // reads it into an Edition, and that is the whole of the fork.
    public enum Edition
    {
        Full,

        Demo,
    }

    // WHAT THE DEMO SHIPS AND WHERE IT STOPS - `demo.json` beside the demo's campaigns, so choosing
    // the demo content is Kathleen editing two ids, not a code change.
    //
    //   { "campaigns": ["first_steps", "the_goat_of_greyhollow"],
    //     "ends": [ { "campaign": "the_goat_of_greyhollow", "chapter": "the_bridge" } ] }
    public sealed class DemoCut
    {
        public DemoCut(IReadOnlyList<string> campaigns,
                       IReadOnlyList<(string Campaign, string Chapter)> ends)
        {
            Campaigns = campaigns ?? Array.Empty<string>();
            Ends = ends ?? Array.Empty<(string, string)>();
        }

        // the only campaigns on the demo's shelf. a Workshop pack is never one of them, which is
        // what keeps the demo to the content it was cut from
        public IReadOnlyList<string> Campaigns { get; }

        // finishing one of these chapters is the end of the demo: the wishlist screen comes up
        public IReadOnlyList<(string Campaign, string Chapter)> Ends { get; }

        public bool Ships(string campaign) =>
            campaign != null && Campaigns.Contains(campaign, StringComparer.Ordinal);

        public bool IsTheEnd(string campaign, string chapter) =>
            Ends.Any(e => e.Campaign == campaign && e.Chapter == chapter);

        public static readonly DemoCut Nothing = new DemoCut(null, null);

        public static bool TryRead(string text, out DemoCut cut, out IReadOnlyList<string> problems)
        {
            var trouble = new List<string>();

            problems = trouble;
            cut = Nothing;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                List<string> campaigns = root.Strings("campaigns").ToList();

                if (campaigns.Count == 0)
                    trouble.Add("a demo with no campaigns in it - list them under 'campaigns'");

                foreach (string id in campaigns.Where(id => !Json.IsId(id)))
                    trouble.Add($"'{id}' is not a campaign id");

                var ends = new List<(string, string)>();

                foreach (JsonElement end in root.Items("ends"))
                {
                    string campaign = end.Text("campaign");
                    string chapter = end.Text("chapter");

                    if (!campaigns.Contains(campaign))
                        trouble.Add($"the demo ends in '{campaign}', which is not one of its campaigns");
                    else
                        ends.Add((campaign, chapter));
                }

                // a demo that never ends never shows the wishlist screen, which is the point of it
                if (ends.Count == 0)
                    trouble.Add("a demo with no end - say which chapter ends it under 'ends'");

                cut = new DemoCut(campaigns, ends);
            }

            return trouble.Count == 0;
        }
    }

    public static class Editions
    {
        // the export feature tag the demo build carries; the game layer asks Godot for it
        public const string DemoFeature = "demo";

        public static Edition From(bool demoFeature) => demoFeature ? Edition.Demo : Edition.Full;

        // Steam counts achievements on the full game only (v1_build_order.md, Phase D)
        public static bool Achievements(this Edition edition) => edition == Edition.Full;

        // the same folder in both, on purpose: "save to shared Steam Cloud" means a player who
        // buys the game after the demo carries on from where the demo stopped. the save format is
        // already the same in both, because there is only one
        public const string SaveFolder = "saves";

        // what the shelf offers in this edition. the full game offers everything playable; the
        // demo offers its cut and nothing else, however much is installed
        public static IEnumerable<Package> Offered(this Shelf shelf, Edition edition, DemoCut cut) =>
            edition == Edition.Full
                ? shelf.Playable
                : shelf.Playable.Where(p => (cut ?? DemoCut.Nothing).Ships(p.Id));

        // finishing a chapter in the demo may be the end of it
        public static bool EndsTheDemo(this Edition edition, DemoCut cut, string campaign,
                                       string chapter) =>
            edition == Edition.Demo && (cut ?? DemoCut.Nothing).IsTheEnd(campaign, chapter);
    }
}
