using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Workshop
{
    // workshop.json: WHAT THE WORKSHOP KEEPS THAT THE GAME NEVER READS (cc_task_m 4.3): each character's note to self and
    // portrait. The game doesn't load it (a campaign plays the same without it) and a hand-made campaign has none, so
    // it's written only when there's something in it, and taken away when there isn't.
    //
    //   { "based_on": "sample_millbrook",
    //     "characters": [ { "id": "oda", "note": "the miller; owes Hesk", "portrait": "assets/portraits/oda.png" } ] }
    //
    // based_on (cc_task_m2 Part 6): the game's own campaign this one was copied from, by id; said on its About tab.
    // names (cc_task_n): a fight's, a table's or a shop's name as the author typed it, where it's more than its id
    // ("fight:goblin_camp": "The goblin camp"; TablesDraft.Names). story (cc_task_o Part 1.7): the story board - where each
    // card sits, its colour and name, and where the board was looking - never in the Yarn
    public sealed class WorkshopNotes
    {
        public const string FileName = "workshop.json";

        static readonly string[] Fields = { "based_on", "characters", "names", "story" };

        static readonly string[] CharacterFields = { "id", "note", "portrait" };

        public Dictionary<string, (string Note, string Portrait)> People { get; } = new(StringComparer.Ordinal);

        public string BasedOn { get; set; } = "";

        public Dictionary<string, string> Names { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Story.CardLook> Looks { get; } = new(StringComparer.Ordinal);

        public Story.BoardView View { get; set; } = new();

        public Dictionary<string, string> Phrases { get; } = new(StringComparer.Ordinal);

        static readonly JsonSerializerOptions Board = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };

        // the board's part of the file: { "view": {...}, "cards": { "old_mill": { "x", "y", "placed", "colour", "name" } } }
        sealed class StoryNotes
        {
            public Story.BoardView View { get; set; } = new();

            public Dictionary<string, Story.CardLook> Cards { get; set; } = new();

            public Dictionary<string, string> Memory { get; set; } = new();
        }

        public string Note(string id) => People.TryGetValue(id, out var person) ? person.Note : "";

        public string Portrait(string id) => People.TryGetValue(id, out var person) ? person.Portrait : "";

        public static WorkshopNotes Of(CampaignDraft draft)
        {
            var notes = new WorkshopNotes { BasedOn = draft.BasedOn ?? "" };

            foreach (Person person in draft.People.Where(p => p.Note.Length > 0 || p.Portrait.Length > 0))
                notes.People[person.Id] = (person.Note, person.Portrait);

            foreach (KeyValuePair<string, string> name in draft.Tables.Names) notes.Names[name.Key] = name.Value;

            foreach (KeyValuePair<string, Story.CardLook> look in draft.Story.Looks.Where(l => !l.Value.IsPlain && draft.Story.Card(l.Key) != null))
                notes.Looks[look.Key] = look.Value;

            notes.View = draft.Story.View;

            foreach (KeyValuePair<string, string> phrase in draft.Story.Phrases) notes.Phrases[phrase.Key] = phrase.Value;

            return notes;
        }

        // a folder's notes; none when it has no file, and what's wrong said as a caution (the game never reads it)
        public static WorkshopNotes Read(string folder, List<ContentProblem> problems)
        {
            var notes = new WorkshopNotes();
            string file = Path.Combine(folder, FileName);

            if (!File.Exists(file)) return notes;

            JsonDocument document = PackJson.ReadObject(File.ReadAllText(file), FileName, "the Workshop's notes", out ContentProblem bad);

            if (document == null)
            {
                problems.Add(ContentProblem.Caution(bad.File, bad.Where, bad.What, bad.Line));
                return notes;
            }

            using (document)
            {
                foreach (JsonProperty field in document.RootElement.EnumerateObject().Where(f => !Fields.Contains(f.Name)))
                    problems.Add(ContentProblem.Caution(FileName, field.Name, $"the Workshop's notes have no '{field.Name}' - they have {Vocabulary.Offer(Fields)}"));

                if (document.RootElement.TryGetProperty("based_on", out JsonElement based) &&
                    based.ValueKind == JsonValueKind.String && Campaigns.ContentId.IsLocal(based.GetString()))
                    notes.BasedOn = based.GetString();

                if (document.RootElement.TryGetProperty("names", out JsonElement names) && names.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty name in names.EnumerateObject().Where(n => n.Value.ValueKind == JsonValueKind.String))
                        notes.Names[name.Name] = name.Value.GetString();

                if (document.RootElement.TryGetProperty("story", out JsonElement story) && story.ValueKind == JsonValueKind.Object)
                {
                    try
                    {
                        StoryNotes board = story.Deserialize<StoryNotes>(Board);
                        foreach (KeyValuePair<string, Story.CardLook> look in board?.Cards ?? new()) notes.Looks[look.Key] = look.Value;
                        notes.View = board?.View ?? new Story.BoardView();
                        foreach (KeyValuePair<string, string> phrase in board?.Memory ?? new()) notes.Phrases[phrase.Key] = phrase.Value;
                    }
                    catch (JsonException wrong)
                    {
                        problems.Add(ContentProblem.Caution(FileName, "story", "the story board's notes couldn't be read - " + wrong.Message));
                    }
                }

                if (!PackJson.OptionalList(document.RootElement, "characters", FileName, "characters is a list of { \"id\", \"note\", \"portrait\" }",
                                           problems, out JsonElement list))
                    return notes;

                foreach (JsonElement one in list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
                {
                    foreach (JsonProperty field in one.EnumerateObject().Where(f => !CharacterFields.Contains(f.Name)))
                        problems.Add(ContentProblem.Caution(FileName, "characters." + field.Name,
                            $"a character's note has no '{field.Name}' - it has {Vocabulary.Offer(CharacterFields)}"));

                    string id = Text(one, "id");
                    if (Campaigns.ContentId.IsLocal(id)) notes.People[id] = (Text(one, "note"), Text(one, "portrait"));
                }
            }

            return notes;
        }

        static string Text(JsonElement one, string field) =>
            one.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

        // written beside pack.json when there's anything to keep; taken away when there isn't
        public void Write(string folder)
        {
            string file = Path.Combine(folder, FileName);

            if (People.Count == 0 && BasedOn.Length == 0 && Names.Count == 0 && Looks.Count == 0 && Phrases.Count == 0)
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }

            File.WriteAllText(file, Text());
        }

        // the file's text (and what the Characters tab's Advanced shows)
        public string Text()
        {
            var characters = People.OrderBy(p => p.Key, StringComparer.Ordinal)
                                   .Select(p => new Dictionary<string, string> { ["id"] = p.Key, ["note"] = p.Value.Note, ["portrait"] = p.Value.Portrait });

            var file = new Dictionary<string, object>();

            if (BasedOn.Length > 0) file["based_on"] = BasedOn;

            file["characters"] = characters;

            if (Names.Count > 0) file["names"] = Names.OrderBy(n => n.Key, StringComparer.Ordinal).ToDictionary(n => n.Key, n => n.Value);

            if (Looks.Count > 0 || Phrases.Count > 0)
                file["story"] = JsonSerializer.SerializeToElement(new StoryNotes
                {
                    View = View,
                    Cards = Looks.OrderBy(l => l.Key, StringComparer.Ordinal).ToDictionary(l => l.Key, l => l.Value),
                    Memory = Phrases.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value),
                }, Board);

            return JsonSerializer.Serialize(file,
                                            new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        }
    }
}
