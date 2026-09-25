using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Magic;

namespace Content.Saves
{
    // A SAVE, AS JSON A PERSON COULD READ. Indented and ordered on purpose: when a save goes wrong
    // the first thing anyone does is open it, and a wall of one-line JSON helps nobody.
    //
    // EVERYTHING WRITTEN HERE IS ORDINAL. Dictionaries iterate in whatever order they were filled,
    // so two saves of the same state would differ byte-for-byte and no diff would be readable.
    // Sorting costs nothing at this size and makes the file a thing you can compare.
    //
    // LIFTED from the old build's SaveWriter, with the homebrew fields replaced by SRD's.
    public static class SaveWriter
    {
        public static string Write(SaveGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();

                json.WriteNumber("format", save.Format);
                json.WriteString("engine", (save.Engine ?? Core.EngineVersion.Current).ToString());

                json.WriteString("campaign", save.Campaign ?? "");
                json.WriteNumber("campaign_format", save.CampaignFormat);
                json.WriteString("chapter", save.Chapter ?? "");
                json.WriteString("map", save.Map ?? "");

                json.WriteString("kind", Vocabulary.NameOf(save.Kind));
                json.WriteNumber("slot", save.Slot);

                if (!string.IsNullOrEmpty(save.Label)) json.WriteString("label", save.Label);

                json.WriteString("node", save.Node ?? "");

                json.WritePropertyName("story");
                json.WriteStartObject();

                foreach (KeyValuePair<string, float> n in save.Numbers) json.WriteNumber(n.Key, n.Value);
                foreach (KeyValuePair<string, string> w in save.Words) json.WriteString(w.Key, w.Value ?? "");
                foreach (KeyValuePair<string, bool> f in save.Flags) json.WriteBoolean(f.Key, f.Value);

                json.WriteEndObject();

                if (save.Steps.Count > 0)
                {
                    json.WritePropertyName("steps");
                    json.WriteStartArray();
                    foreach (string step in save.Steps) json.WriteStringValue(step);
                    json.WriteEndArray();
                }

                json.WriteNumber("round", save.Round);
                json.WriteNumber("turn", save.Turn);
                json.WriteNumber("actions", save.ActionsLeft);

                if (save.Hero != null)
                {
                    json.WritePropertyName("hero");
                    WriteHero(json, save.Hero);
                }

                json.WritePropertyName("foes");
                json.WriteStartArray();

                foreach (SavedActor foe in save.Foes) WriteActor(json, foe);

                json.WriteEndArray();

                json.WritePropertyName("felt");
                json.WriteStartArray();

                foreach (SavedDie die in save.Felt)
                {
                    json.WriteStartObject();
                    json.WriteString("die", Vocabulary.NameOf(die.Die));
                    json.WriteNumber("value", die.Value);
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        static void WriteHero(Utf8JsonWriter json, SavedHero hero)
        {
            json.WriteStartObject();

            json.WriteString("name", hero.Name ?? "");
            json.WriteString("class", hero.Class ?? "");
            json.WriteString("species", hero.Species ?? "");

            if (!string.IsNullOrEmpty(hero.Lineage)) json.WriteString("lineage", hero.Lineage);

            json.WriteString("background", hero.Background ?? "");

            if (!string.IsNullOrEmpty(hero.Alignment)) json.WriteString("alignment", hero.Alignment);
            json.WriteNumber("level", hero.Level);

            json.WritePropertyName("scores");
            json.WriteStartObject();

            foreach (Ability ability in Enum.GetValues<Ability>().OrderBy(a => (int)a))
                if (hero.Scores.TryGetValue(ability, out int score))
                    json.WriteNumber(Vocabulary.NameOf(ability), score);

            json.WriteEndObject();

            if (hero.BackgroundSpend.Count > 0)
            {
                json.WritePropertyName("background_spend");
                json.WriteStartObject();

                foreach (Ability ability in Enum.GetValues<Ability>().OrderBy(a => (int)a))
                    if (hero.BackgroundSpend.TryGetValue(ability, out int spend))
                        json.WriteNumber(Vocabulary.NameOf(ability), spend);

                json.WriteEndObject();
            }

            Words(json, "skills", hero.Skills.Select(Vocabulary.NameOf));
            Words(json, "expertise", hero.Expertise.Select(Vocabulary.NameOf));

            if (hero.DiscardWarningDismissed) json.WriteBoolean("discard_warning_dismissed", true);

            // always written, even empty: its presence is how a reader tells a save that records
            // the player's improvements from one that predates them
            // each one a list of the abilities it raised: ["str"] is +2, ["dex", "con"] +1 each
            json.WritePropertyName("improvements");
            json.WriteStartArray();

            foreach (string word in hero.Improvements)
            {
                json.WriteStartArray();
                foreach (string ability in word.Split('+')) json.WriteStringValue(ability);
                json.WriteEndArray();
            }

            json.WriteEndArray();

            json.WriteNumber("improvements_pending", hero.PendingImprovements);

            json.WriteNumber("hp", hero.HitPoints);

            if (hero.TemporaryHitPoints != 0)
                json.WriteNumber("temp_hp", hero.TemporaryHitPoints);

            json.WriteNumber("hit_dice", hero.HitDice);
            json.WriteNumber("gold", hero.Gold);

            // the mode first, then only the state that mode has: writing both shapes every time
            // would put a slot grid in every points save and mean nothing by it
            json.WriteString("spell_resource", Vocabulary.NameOf(hero.Resource));

            if (hero.Resource == SpellResourceMode.Points)
            {
                json.WriteNumber("spell_points", hero.Points);

                if (hero.SpentHighLevels.Count > 0)
                {
                    json.WritePropertyName("spent_high_levels");
                    json.WriteStartArray();

                    foreach (int level in hero.SpentHighLevels.OrderBy(l => l))
                        json.WriteNumberValue(level);

                    json.WriteEndArray();
                }
            }
            else if (hero.Slots.Count > 0)
            {
                json.WritePropertyName("spell_slots");
                json.WriteStartArray();

                foreach (int left in hero.Slots) json.WriteNumberValue(left);

                json.WriteEndArray();
            }

            Words(json, "known", hero.Known);
            Words(json, "conditions", hero.Conditions.Select(Vocabulary.NameOf));

            if (hero.Spent.Count > 0)
            {
                json.WritePropertyName("spent");
                json.WriteStartObject();

                foreach (string feature in hero.Spent.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    json.WriteNumber(feature, hero.Spent[feature]);

                json.WriteEndObject();
            }

            if (hero.ExtraActions >= 0) json.WriteNumber("extra_actions", hero.ExtraActions);

            if (!string.IsNullOrEmpty(hero.Form)) json.WriteString("form", hero.Form);

            if (hero.Worn.Count > 0)
            {
                json.WritePropertyName("worn");
                json.WriteStartObject();

                foreach (Slot slot in hero.Worn.Keys.OrderBy(s => (int)s))
                    json.WriteString(Vocabulary.NameOf(slot), hero.Worn[slot] ?? "");

                json.WriteEndObject();
            }

            if (hero.Pack.Count > 0)
            {
                json.WritePropertyName("pack");
                json.WriteStartArray();

                foreach (SavedStack stack in hero.Pack.OrderBy(s => s.Item, StringComparer.Ordinal))
                {
                    json.WriteStartObject();
                    json.WriteString("item", stack.Item ?? "");
                    json.WriteNumber("count", stack.Count);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            }

            Where(json, hero.X, hero.Y);

            json.WriteEndObject();
        }

        static void WriteActor(Utf8JsonWriter json, SavedActor actor)
        {
            json.WriteStartObject();

            json.WriteString("id", actor.Id ?? "");

            if (actor.Ordinal > 0) json.WriteNumber("ordinal", actor.Ordinal);

            json.WriteNumber("hp", actor.HitPoints);

            if (actor.TemporaryHitPoints != 0)
                json.WriteNumber("temp_hp", actor.TemporaryHitPoints);

            json.WriteNumber("seat", actor.Seat);
            json.WriteNumber("initiative", actor.Initiative);

            Words(json, "conditions", actor.Conditions.Select(Vocabulary.NameOf));

            Where(json, actor.X, actor.Y);

            json.WriteEndObject();
        }

        static void Where(Utf8JsonWriter json, int? x, int? y)
        {
            if (!x.HasValue || !y.HasValue) return;

            json.WritePropertyName("at");
            json.WriteStartArray();
            json.WriteNumberValue(x.Value);
            json.WriteNumberValue(y.Value);
            json.WriteEndArray();
        }

        // written only when there is something in it: an empty array in every save is noise in
        // every diff
        static void Words(Utf8JsonWriter json, string name, System.Collections.Generic.IEnumerable<string> words)
        {
            var all = words.Where(w => !string.IsNullOrEmpty(w)).ToList();

            if (all.Count == 0) return;

            json.WritePropertyName(name);
            json.WriteStartArray();

            foreach (string word in all) json.WriteStringValue(word);

            json.WriteEndArray();
        }
    }
}
