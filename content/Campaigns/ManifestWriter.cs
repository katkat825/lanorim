using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Core.Words;

namespace Content.Campaigns
{
    // WRITES pack.json (cc_task_m Part 3; WORKSHOP_DESIGN.md principle 6), beside the reader that has always read it
    // (ManifestReader). Its fields in the reader's order, and only what a hand-written pack would write: a list that is
    // empty, the blank GM screen and a rule left at the game's default are left out, the way the reader takes them when
    // they're missing. read -> write -> read gives the same manifest (ManifestWriterTests), and sample_millbrook's own
    // file comes back byte for byte
    public static class ManifestWriter
    {
        public static string Write(Manifest manifest)
        {
            using var text = new MemoryStream();

            using (var json = new Utf8JsonWriter(text, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();

                json.WriteString("id", manifest.Id);
                json.WriteString("kind", EnumWords.Name(manifest.Kind));
                json.WriteNumber("format", manifest.Format);
                json.WriteString("engine", manifest.Engine?.ToString() ?? Core.EngineVersion.Current.ToString());

                if (manifest.Author.Length > 0) json.WriteString("author", manifest.Author);

                Words(json, "tags", manifest.Tags);

                if (manifest.Preview.Length > 0) json.WriteString("preview", manifest.Preview);

                Words(json, "dependencies", manifest.Dependencies);

                if (manifest.IsPlayable)
                {
                    json.WriteStartArray("chapters");

                    foreach (Chapter chapter in manifest.Chapters)
                    {
                        json.WriteStartObject();
                        json.WriteString("id", chapter.Id);
                        Words(json, "maps", chapter.Maps, always: true);
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();

                    if (manifest.Start.Length > 0) json.WriteString("start", manifest.Start);
                }

                if (manifest.GmScreen != Manifest.GmScreens[0]) json.WriteString("gm_screen", manifest.GmScreen);

                Rules(json, manifest.Rules);

                json.WriteEndObject();
            }

            // the hand-written packs' line ending, whatever the machine's
            return Encoding.UTF8.GetString(text.ToArray()).Replace("\r\n", "\n") + "\n";
        }

        static void Words(Utf8JsonWriter json, string name, IReadOnlyList<string> words, bool always = false)
        {
            if (words.Count == 0 && !always) return;

            json.WriteStartArray(name);
            foreach (string word in words) json.WriteStringValue(word);
            json.WriteEndArray();
        }

        // the play rules the campaign changed from the game's (CampaignRules.Default), each under the reader's own name
        static void Rules(Utf8JsonWriter json, CampaignRules rules)
        {
            CampaignRules game = CampaignRules.Default;

            if (rules.Defeat != game.Defeat) json.WriteString("defeat", EnumWords.Name(rules.Defeat));
            if (rules.Leveling != game.Leveling) json.WriteString("leveling", EnumWords.Name(rules.Leveling));
            if (rules.Characters != game.Characters) json.WriteNumber("characters", rules.Characters);
            if (rules.StartingShop != game.StartingShop) json.WriteBoolean("starting_shop", rules.StartingShop);
            if (rules.Consequences != game.Consequences) json.WriteString("consequences", EnumWords.Name(rules.Consequences));
        }
    }
}
