using Content.Campaigns;

namespace Content.Workshop
{
    // THE CHARACTERS TAB (cc_task_m 4.3): the named people who speak or appear. A name (the locale's actor.<id>.name),
    // a note to self and a portrait (workshop.json); the id is made from the first name and is the story's speaker,
    // so a new name keeps it
    public sealed partial class CampaignEditor
    {
        // the speakers that are always there, whatever the campaign names: the GM and the hero's companion
        // (DialogueBook's "dm" and "companion")
        public static readonly string[] AlwaysSpeak = { "dm", "companion" };

        public string AddPerson(string name)
        {
            string id = ContentId.Free(ContentId.From(name, "someone"),
                                       taken => Draft.Character(taken) != null || System.Array.IndexOf(AlwaysSpeak, taken) >= 0);

            Edit("character", d => d.People.Add(new Person { Id = id, Name = name ?? "" }));
            return id;
        }

        public bool RenamePerson(string id, string name) =>
            Edit("character", d => { if (d.Character(id) is { } person) person.Name = name ?? ""; });

        public bool NoteOn(string id, string note) =>
            Edit("character", d => { if (d.Character(id) is { } person) person.Note = note ?? ""; });

        // a picture already copied into the campaign (assets/portraits/<id>.png), or "" for none
        public bool PortraitOf(string id, string portrait) =>
            Edit("character", d => { if (d.Character(id) is { } person) person.Portrait = portrait ?? ""; });

        public bool RemovePerson(string id) => Edit("character", d => d.People.RemoveAll(p => p.Id == id));

        // where a character's portrait is copied to, inside the campaign
        public static string PortraitPath(string id) => $"{Package.AssetsFolder}/portraits/{id}.png";
    }
}
