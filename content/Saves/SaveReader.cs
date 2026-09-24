using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Magic;

namespace Content.Saves
{
    // READS A SAVE AS FAR AS IT CAN, AND SAYS WHAT IT DID NOT UNDERSTAND.
    //
    // THIS IS THE OPPOSITE POLICY TO A CAMPAIGN, DELIBERATELY. A campaign with a fault is refused,
    // because half a campaign fails in the middle of a fight and the author is there to fix it. A
    // save is somebody's afternoon and there is nobody to fix it - so a field this build does not
    // recognise is a sentence, not a refusal, and the save comes back with everything that DID
    // read. `Read<T>.Partial` is exactly that shape and is why it exists.
    //
    // LIFTED from the old build's SaveReader, with the homebrew fields replaced by SRD's.
    public static class SaveReader
    {
        public static Read<SaveGame> Parse(string json, string file = "save.json")
        {
            var problems = new List<ContentProblem>();

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json ?? "", Json.Options);
            }
            catch (JsonException bad)
            {
                return Read<SaveGame>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<SaveGame>.Bad(new ContentProblem(
                        file, "", "a save is a JSON object and this is a " +
                                  root.ValueKind.ToString().ToLowerInvariant()));

                var save = new SaveGame
                {
                    Format = root.Number("format", 0),
                    Campaign = root.Text("campaign"),
                    CampaignFormat = root.Number("campaign_format", 0),
                    Chapter = root.Text("chapter"),
                    Map = root.Text("map"),
                    Round = root.Number("round", 0),
                    Turn = root.Number("turn", -1),
                    ActionsLeft = root.Number("actions", 0),
                };

                if (save.Format == 0)
                    problems.Add(new ContentProblem(file, "format",
                        "this save does not say what format it is, so nothing can be assumed " +
                        "about the rest of it"));
                else if (!SaveFormat.CanRead(save.Format))
                    problems.Add(ContentProblem.Caution(file, "format",
                        SaveFormat.Unfamiliar(save.Format)));

                if (Version.TryParse(root.Text("engine"), out Version engine))
                    save.Engine = engine;
                else
                    problems.Add(ContentProblem.Caution(file, "engine",
                        "this save does not say which engine wrote it"));

                if (root.Has("hero")) save.Hero = Hero(root.GetProperty("hero"), file, problems);

                if (root.Has("foes"))
                    foreach (JsonElement one in root.GetProperty("foes").EnumerateArray())
                        save.Foes.Add(Actor(one, file, problems));

                if (root.Has("felt"))
                    foreach (JsonElement one in root.GetProperty("felt").EnumerateArray())
                    {
                        if (!Vocabulary.TryDie(one.Text("die"), out Die die))
                        {
                            problems.Add(ContentProblem.Caution(file, "felt",
                                $"'{one.Text("die")}' is not a die this build rolls"));
                            continue;
                        }

                        save.Felt.Add(new SavedDie(die, one.Number("value", 0)));
                    }

                // a save always comes back; the problems say what was not understood about it
                return Read<SaveGame>.Partial(save, problems);
            }
        }

        static SavedHero Hero(JsonElement entry, string file, List<ContentProblem> problems)
        {
            var hero = new SavedHero
            {
                Name = entry.Text("name"),
                Class = entry.Text("class"),
                Species = entry.Text("species"),
                Lineage = entry.Text("lineage"),
                Background = entry.Text("background"),
                Level = entry.Number("level", 1),
                HitPoints = entry.Number("hp", -1),
                TemporaryHitPoints = entry.Number("temp_hp", 0),
                HitDice = entry.Number("hit_dice", -1),
                Gold = entry.Number("gold", 0),
                Points = entry.Number("spell_points", -1),
            };

            // a save that does not say gets slots, which is what a character created without an
            // opinion has - and a mode this build does not know is a caution, not a refusal
            if (entry.Has("spell_resource"))
            {
                if (Vocabulary.TryWord(entry.Text("spell_resource"), out SpellResourceMode mode))
                    hero.Resource = mode;
                else
                    problems.Add(ContentProblem.Caution(file, "hero.spell_resource",
                        $"'{entry.Text("spell_resource")}' is not a way of paying for spells - " +
                        "it is " + Vocabulary.Offer<SpellResourceMode>() +
                        ". This character is being read as using slots"));
            }

            foreach (JsonElement one in entry.Items("spell_slots"))
                hero.Slots.Add(one.ValueKind == JsonValueKind.Number ? one.GetInt32() : 0);

            foreach (JsonElement one in entry.Items("spent_high_levels"))
                if (one.ValueKind == JsonValueKind.Number)
                    hero.SpentHighLevels.Add(one.GetInt32());

            if (entry.Has("scores"))
                foreach (JsonProperty property in entry.GetProperty("scores").EnumerateObject())
                {
                    if (!Vocabulary.TryWord(property.Name, out Ability ability))
                    {
                        problems.Add(ContentProblem.Caution(file, "hero.scores",
                            $"'{property.Name}' is not an ability - it is " +
                            Vocabulary.Offer<Ability>()));
                        continue;
                    }

                    hero.Scores[ability] = property.Value.ValueKind == JsonValueKind.Number
                        ? property.Value.GetInt32() : 10;
                }

            foreach (Skill skill in Words<Skill>(entry, "skills", file, "hero.skills", problems))
                hero.Skills.Add(skill);

            foreach (Skill skill in Words<Skill>(entry, "expertise", file, "hero.expertise", problems))
                hero.Expertise.Add(skill);

            foreach (string id in entry.Strings("known")) hero.Known.Add(id);

            foreach (Condition condition in Words<Condition>(entry, "conditions", file,
                                                             "hero.conditions", problems))
                hero.Conditions.Add(condition);

            if (entry.Has("worn"))
                foreach (JsonProperty property in entry.GetProperty("worn").EnumerateObject())
                {
                    if (!Vocabulary.TryWord(property.Name, out Slot slot))
                    {
                        problems.Add(ContentProblem.Caution(file, "hero.worn",
                            $"'{property.Name}' is not a slot anything is worn in"));
                        continue;
                    }

                    hero.Worn[slot] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() : "";
                }

            if (entry.Has("pack"))
                foreach (JsonElement one in entry.GetProperty("pack").EnumerateArray())
                    hero.Pack.Add(new SavedStack(one.Text("item"), one.Number("count", 1)));

            (hero.X, hero.Y) = Where(entry);

            return hero;
        }

        static SavedActor Actor(JsonElement entry, string file, List<ContentProblem> problems)
        {
            var actor = new SavedActor
            {
                Id = entry.Text("id"),
                Ordinal = entry.Number("ordinal", 0),
                HitPoints = entry.Number("hp", -1),
                TemporaryHitPoints = entry.Number("temp_hp", 0),
                Seat = entry.Number("seat", -1),
                Initiative = entry.Number("initiative", 0),
            };

            foreach (Condition condition in Words<Condition>(entry, "conditions", file,
                                                             "foes.conditions", problems))
                actor.Conditions.Add(condition);

            (actor.X, actor.Y) = Where(entry);

            return actor;
        }

        static IEnumerable<T> Words<T>(JsonElement entry, string field, string file, string where,
                                       List<ContentProblem> problems) where T : struct, Enum
        {
            foreach (string word in entry.Strings(field))
            {
                if (Vocabulary.TryWord(word, out T value))
                {
                    yield return value;
                    continue;
                }

                problems.Add(ContentProblem.Caution(file, where,
                    $"'{word}' is not something this build knows - it knows " +
                    Vocabulary.Offer<T>()));
            }
        }

        static (int?, int?) Where(JsonElement entry)
        {
            if (!entry.Has("at")) return (null, null);

            JsonElement at = entry.GetProperty("at");

            if (at.ValueKind != JsonValueKind.Array || at.GetArrayLength() != 2)
                return (null, null);

            return (at[0].GetInt32(), at[1].GetInt32());
        }
    }
}
