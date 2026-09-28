using System;
using System.Collections.Generic;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Core.Characters;

namespace Content.Creation
{
    public sealed partial class Creation
    {
        // --- where it is up to --------------------------------------------------------------------

        public Step Next =>
            Class == null ? Step.Class
          : Species == null ? Step.Species
          : NeedsLineage && Lineage == null ? Step.Lineage
          : Background == null ? Step.Background
          : !Scores.IsLegalPointBuy(out _) ? Step.Abilities
          : ImprovementPicksLeft > 0 ? Step.Improvements
          : SkillPicksLeft > 0 || ExpertisePicksLeft > 0 ? Step.Skills
          : CantripPicksLeft > 0 || SpellPicksLeft > 0 ? Step.Spells
          : Name.Length == 0 ? Step.Name
          : Step.Done;

        public bool Ready => Next == Step.Done;

        public IReadOnlyList<string> Problems
        {
            get
            {
                var problems = new List<string>();

                if (Class == null) problems.Add("no class picked");
                if (Species == null) problems.Add("no species picked");
                if (NeedsLineage && Lineage == null) problems.Add($"{Species.Id} needs a lineage");
                if (Background == null) problems.Add("no background picked");

                if (!Scores.IsLegalPointBuy(out string spend)) problems.Add(spend);

                if (Background != null && !Background.IsLegalSpend(_backgroundSpend, out string bg))
                    problems.Add(bg);

                if (ImprovementPicksLeft > 0)
                    problems.Add($"{ImprovementPicksLeft} ability score improvements still to spend");

                if (SkillPicksLeft > 0) problems.Add($"{SkillPicksLeft} skills still to pick");

                if (ExpertisePicksLeft > 0)
                    problems.Add($"{ExpertisePicksLeft} expertises still to pick");

                if (CantripPicksLeft > 0) problems.Add($"{CantripPicksLeft} cantrips still to pick");

                if (SpellPicksLeft > 0) problems.Add($"{SpellPicksLeft} spells still to pick");

                if (Name.Length == 0) problems.Add("no name");

                return problems;
            }
        }

        public Hero Finish()
        {
            if (!Ready) return null;

            var hero = new Hero(Name, Class, Species, Background, Scores.Copy(), Level, Lineage,
                                Resource)
            {
                Alignment = Alignment,
            };

            hero.Build(_backgroundSpend, _skills, _expertise, Library.Items, _spells, _improvements);

            return hero;
        }

        // the standard array, dealt into the class's priority order. 15 14 13 12 10 8 is exactly
        // the 27-point budget, so a guided creation is always legal
        public static AbilityScores Standard(CharacterClass cls)
        {
            int[] array = { 15, 14, 13, 12, 10, 8 };

            var order = new List<Ability>(cls?.Priority ?? Array.Empty<Ability>());

            foreach (Ability ability in Abilities.All)
                if (!order.Contains(ability))
                    order.Add(ability);

            var scores = new AbilityScores();

            for (int i = 0; i < order.Count && i < array.Length; i++)
                scores.SetBase(order[i], array[i]);

            return scores;
        }
    }
}
