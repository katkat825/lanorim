using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Workshop;

namespace Content.Screens
{
    // WHO SOMEONE ON THE MAP IS (cc_task_m3 Part 10): a figure is "Someone" (no actor), or one of the campaign's characters
    // from its Characters tab (CampaignEditor.People), or a new one added there. A map in My maps belongs to no campaign
    // and offers only Someone. A figure naming a character the campaign no longer has is a warning
    public sealed partial class MapBuilderView
    {
        public static readonly string WhoKey = K("who");
        public static readonly string SomeoneKey = K("someone");
        public static readonly string NewPersonKey = K("new_person");
        public static readonly string NewPersonNameKey = K("new_person_name");
        public static readonly string NpcsKey = K("npcs");
        public static readonly string SearchNpcsKey = K("search_npcs");

        static IEnumerable<string> PeopleKeys() =>
            new[] { WhoKey, SomeoneKey, NewPersonKey, NewPersonNameKey, NpcsKey, SearchNpcsKey };

        IReadOnlyList<(string Id, string Name)> _people;

        // the campaign's characters, by id and name, as its files have them now; none for My maps
        public IReadOnlyList<(string Id, string Name)> People =>
            _people ??= Place == null || Place.IsLibrary
                ? Array.Empty<(string, string)>()
                : CampaignEditor.Open(Place.Campaign).Draft.People.Select(p => (p.Id, p.Name)).ToList();

        // the name a figure's actor goes by, or null for someone, or for one the campaign no longer has
        public string NameOf(string actor) =>
            string.IsNullOrEmpty(actor) ? null : People.FirstOrDefault(p => p.Id == actor).Name;

        // A NEW CHARACTER, from the map: added to the campaign's Characters tab (a name; the rest is the tab's) and saved
        // there. Its id, or null when the campaign couldn't be written or there is none
        public string AddPerson(string name)
        {
            if (Place == null || Place.IsLibrary || string.IsNullOrWhiteSpace(name)) return null;

            CampaignEditor campaign = CampaignEditor.Open(Place.Campaign);
            string id = campaign.AddPerson(name.Trim());

            if (campaign.Save() != null) return null;

            _people = null;
            return id;
        }

        IEnumerable<MapProblem> PeopleFindings() =>
            Place == null || Place.IsLibrary
                ? Enumerable.Empty<MapProblem>()
                : Draft.Props.Where(p => p.Actor.Length > 0 && People.All(person => person.Id != p.Actor))
                       .Select(p => new MapProblem("npc_unknown_person", p.Cell,
                                                   $"the {p.Id} at {p.Cell} is '{p.Actor}', who isn't one of the campaign's characters",
                                                   p.Actor) { Warning = true });
    }
}
