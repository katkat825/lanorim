using System;
using System.Collections.Generic;
using System.Linq;
using Content.Challengers;
using Content.Monsters;
using Content.Schema;
using Core.Space;
using Core.Tables;

namespace Content.Campaigns
{
    public sealed partial class Package
    {
        // --- the arena's challengers (cc_task_cg) --------------------------------------------------------------

        // the challenger types (challengers/*.json, written by tools/import_challengers.py from Kathleen's vault)
        public const string ChallengersFolder = "challengers";

        public IReadOnlyList<ChallengerType> Challengers { get; private set; } = Array.Empty<ChallengerType>();

        public ChallengerType KindOfChallenger(string id) => Challengers.FirstOrDefault(t => t.Id == id);

        // the hand-written fights marked "challenger" (cc_task_cg 3.4), in the order the tables have them
        public IEnumerable<EncounterEntry> SpecialChallengers =>
            Encounters.SelectMany(t => t.Entries).Where(e => e.Challenger != null);

        // the arena's keys: every type's numbered names and descriptions, and each special's own two
        IEnumerable<string> ChallengerKeys() =>
            Challengers.SelectMany(t => t.Keys())
                       .Concat(SpecialChallengers.SelectMany(e => new[]
                       {
                           Challenger.SpecialKey(e.Id, "name"), Challenger.SpecialKey(e.Id, "description"),
                       }));

        // read after the encounters, because a type names monsters and maps the way a fight does; the solo budget
        // isn't checked here, because a challenger is fitted to the hero when the roster is drawn (ChallengerFit)
        static IReadOnlyList<ChallengerType> ReadChallengers(string folder, IReadOnlyList<Monster> monsters,
                                                             IReadOnlyDictionary<string, MapLayout> maps,
                                                             List<ContentProblem> problems)
        {
            IReadOnlyList<ChallengerType> types =
                ReadFolder(folder, ChallengersFolder, ChallengerReader.Entries, t => t.Id, problems);

            var own = new HashSet<string>(monsters.Select(m => m.Id), StringComparer.Ordinal);
            Bestiary srd = Library.Srd().Bestiary;

            foreach (ChallengerType type in types)
            {
                string where = $"challengers.{type.Id}";

                foreach (Band band in type.Monsters.Where(b => !own.Contains(b.Monster) && !srd.Has(b.Monster)))
                    problems.Add(new ContentProblem(ChallengersFolder, where,
                        $"'{band.Monster}' is not a monster in this campaign's {MonstersFolder}/ or the SRD's"));

                if (type.Map.Length > 0 && !maps.ContainsKey(type.Map))
                    problems.Add(new ContentProblem(ChallengersFolder, where,
                        $"'{type.Id}' is fought on '{type.Map}' and there is no {MapsFolder}/{type.Map}{MapExtension} " +
                        "in this campaign"));
            }

            return types;
        }
    }
}
