using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Words;

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

            JsonDocument document = PackJson.ReadObject(json, file, "a save", out ContentProblem bad);

            if (document == null) return Read<SaveGame>.Bad(bad);

            using (document)
            {
                JsonElement root = document.RootElement;

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

                save.Slot = root.Number("slot", 0);
                save.Label = root.Text("label");
                save.Node = root.Text("node");

                if (root.Has("kind") && EnumWords.TryName(root.Text("kind"), out SaveKind kind))
                    save.Kind = kind;

                if (root.Has("story") && root.GetProperty("story").ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty v in root.GetProperty("story").EnumerateObject())
                        switch (v.Value.ValueKind)
                        {
                            case JsonValueKind.Number: save.Numbers[v.Name] = v.Value.GetSingle(); break;
                            case JsonValueKind.String: save.Words[v.Name] = v.Value.GetString(); break;
                            case JsonValueKind.True: save.Flags[v.Name] = true; break;
                            case JsonValueKind.False: save.Flags[v.Name] = false; break;
                        }

                foreach (string step in root.Strings("steps")) save.Steps.Add(step);

                if (save.Format == 0)
                    problems.Add(new ContentProblem(file, "format",
                        "this save does not say what format it is, so nothing can be assumed " +
                        "about the rest of it"));
                else if (!SaveFormat.Range.CanRead(save.Format))
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

        // a hero, section by section as SaveWriter writes it; problems come in the same order as ever
        static SavedHero Hero(JsonElement entry, string file, List<ContentProblem> problems)
        {
            var hero = new SavedHero
            {
                Name = entry.Text("name"),
                Class = entry.Text("class"),
                Species = entry.Text("species"),
                Lineage = entry.Text("lineage"),
                Background = entry.Text("background"),
                Alignment = entry.Text("alignment"),
                SpellAbility = entry.Text("spell_ability"),
                Size = entry.Text("size"),
                ShopsFirst = entry.Flag("shops_first"),
                Level = entry.Number("level", 1),
                HitPoints = entry.Number("hp", -1),
                TemporaryHitPoints = entry.Number("temp_hp", 0),
                HitDice = entry.Number("hit_dice", -1),
                Copper = entry.Has("copper") ? entry.Number("copper", 0)
                                             : Inventory.Coins.FromGold(entry.Number("gold", 0)),
                Points = entry.Number("spell_points", -1),
                ExtraActions = entry.Number("extra_actions", -1),
                Form = entry.Text("form"),
            };

            ReadMagic(entry, hero, file, problems);
            ReadChoices(entry, hero, file, problems);

            foreach (Condition condition in Words<Condition>(entry, "conditions", file,
                                                             "hero.conditions", problems))
                hero.Conditions.Add(condition);

            ReadGear(entry, hero, file, problems);

            (hero.X, hero.Y) = Where(entry);

            return hero;
        }

        static void ReadMagic(JsonElement entry, SavedHero hero, string file, List<ContentProblem> problems)
        {
            // a save that does not say gets slots, which is what a character created without an
            // opinion has - and a mode this build does not know is a caution, not a refusal
            if (entry.Has("spell_resource"))
            {
                if (EnumWords.TryName(entry.Text("spell_resource"), out SpellResourceMode mode))
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

            foreach (string id in entry.Strings("known")) hero.Known.Add(id);
        }

        // what the player chose: scores, the background's spend, skills, improvements - and what
        // its features have spent
        static void ReadChoices(JsonElement entry, SavedHero hero, string file, List<ContentProblem> problems)
        {
            Scores(entry, "scores", 10, hero.Scores, file, problems);
            Scores(entry, "background_spend", 0, hero.BackgroundSpend, file, problems);

            if (entry.Has("spent"))
                foreach (JsonProperty property in entry.GetProperty("spent").EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.Number)
                        hero.Spent[property.Name] = property.Value.GetInt32();

            foreach (Skill skill in Words<Skill>(entry, "skills", file, "hero.skills", problems))
                hero.Skills.Add(skill);

            foreach (Skill skill in Words<Skill>(entry, "expertise", file, "hero.expertise", problems))
                hero.Expertise.Add(skill);

            hero.ImprovementsRecorded = entry.Has("improvements");
            hero.PendingImprovements = entry.Number("improvements_pending", 0);
            hero.DiscardWarningDismissed = entry.Has("discard_warning_dismissed") &&
                                           entry.GetProperty("discard_warning_dismissed").ValueKind == JsonValueKind.True;

            // ["str"] is +2 Strength, ["dex", "con"] +1 to each
            foreach (JsonElement one in entry.Items("improvements"))
            {
                if (one.ValueKind != JsonValueKind.Array)
                {
                    problems.Add(ContentProblem.Caution(file, "hero.improvements",
                        "an improvement is a list of the abilities it raised - one is left to " +
                        "spend again"));
                    continue;
                }

                hero.Improvements.Add(string.Join("+", one.EnumerateArray()
                                                          .Where(a => a.ValueKind == JsonValueKind.String)
                                                          .Select(a => a.GetString())));
            }
        }

        static void ReadGear(JsonElement entry, SavedHero hero, string file, List<ContentProblem> problems)
        {
            if (entry.Has("worn"))
                foreach (JsonProperty property in entry.GetProperty("worn").EnumerateObject())
                {
                    // a save from before cc_task_f 1.6 wore a shield in a slot of its own; it is the off hand now
                    if (!EnumWords.TryName(property.Name == "shield" ? EnumWords.Name(Slot.OffHand) : property.Name, out Slot slot))
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

        // the scores and the background spend are the same shape - a number by ability - and an
        // ability this build does not know is the same caution in both
        static void Scores(JsonElement entry, string field, int otherwise,
                           IDictionary<Ability, int> into, string file,
                           List<ContentProblem> problems)
        {
            if (!entry.Has(field)) return;

            foreach (JsonProperty property in entry.GetProperty(field).EnumerateObject())
            {
                if (!EnumWords.TryName(property.Name, out Ability ability))
                {
                    problems.Add(ContentProblem.Caution(file, "hero." + field,
                        $"'{property.Name}' is not an ability - it is " +
                        Vocabulary.Offer<Ability>()));
                    continue;
                }

                into[ability] = property.Value.ValueKind == JsonValueKind.Number
                    ? property.Value.GetInt32() : otherwise;
            }
        }

        static IEnumerable<T> Words<T>(JsonElement entry, string field, string file, string where,
                                       List<ContentProblem> problems) where T : struct, Enum
        {
            foreach (string word in entry.Strings(field))
            {
                if (EnumWords.TryName(word, out T value))
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
