using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Content.Species;
using Core.Characters;
using Core.Localization;
using Core.Magic;
using Core.Words;

namespace Content.Creation
{
    public sealed partial class Creation
    {
        // --- the steps --------------------------------------------------------------------------

        public IEnumerable<CharacterClass> ClassChoices =>
            Library.Classes.OrderBy(c => Roster.Classes.ToList().IndexOf(c.Id));

        public IEnumerable<Kind> SpeciesChoices =>
            Library.Playable.OrderBy(s => Roster.Species.ToList().IndexOf(s.Id));

        public IEnumerable<Kind> LineageChoices =>
            Species == null ? Enumerable.Empty<Kind>() : Library.LineagesOf(Species.Id);

        public bool NeedsLineage => LineageChoices.Any();

        public IEnumerable<Skill> SkillChoices =>
            Class?.SkillChoices.Where(s => !_skills.Contains(s) && !_traitSkills.Contains(s)) ?? Enumerable.Empty<Skill>();

        // the class's picks, and one more for a feature that grants it by this level (SRD 5.2.1
        // Primal Knowledge, p.29)
        public int SkillPicks =>
            (Class?.SkillPicks ?? 0) +
            (Class?.Features.Where(f => f.Level <= Level).Sum(f => f.SkillPicks) ?? 0);

        public int SkillPicksLeft => Math.Max(0, SkillPicks - _skills.Count);

        public int ExpertisePicks =>
            Class?.Features.Where(f => f.Trait == Trait.Expertise && f.Level <= Level)
                           .Sum(f => f.Count) ?? 0;

        public int ExpertisePicksLeft => Math.Max(0, ExpertisePicks - _expertise.Count);

        // the spells a fresh caster may put on the sheet: its class's list, cantrips and level 1
        // WHICH WAY THIS CHARACTER WILL PAY FOR LEVELED SPELLS. Slots is pre-selected because it
        // is the SRD's own answer, and a player who does not care which they have should end up
        // holding the faithful one rather than the variant.
        //
        // Both labels are keys, not words: the screen shows "Spell slots (classic D&D)" against
        // "Spell points (simpler bookkeeping)" in whatever language it is being read in.
        public SpellResourceMode Resource { get; private set; } = SpellResourceMode.Slots;

        public Alignment Alignment { get; private set; } = Alignment.Neutral;

        public void Pick(Alignment alignment) => Alignment = alignment;

        // a non-caster is never asked, and answering for one is refused rather than ignored
        public bool ChoosesResource => Class != null && Class.Casts;

        public bool Pick(SpellResourceMode mode)
        {
            if (!ChoosesResource) return false;

            Resource = mode;
            return true;
        }

        public static string LabelKey(SpellResourceMode mode) =>
            KeyConventions.Key(KeyConventions.UiNs, "spell_resource",
                               EnumWords.Name(mode), "name");

        public static string BlurbKey(SpellResourceMode mode) =>
            KeyConventions.Key(KeyConventions.UiNs, "spell_resource",
                               EnumWords.Name(mode), "description");

        public static IEnumerable<string> ResourceKeys() =>
            System.Enum.GetValues<SpellResourceMode>()
                       .SelectMany(m => new[] { LabelKey(m), BlurbKey(m) });

        public IEnumerable<Spell> SpellChoices =>
            Offered.Where(s => s.Level <= HighestSpellLevel)
                   .Where(s => !_spells.Any(k => k.Id == s.Id));

        // the class's list as a solo hero sees it: no spell a party of one can never cast (Solo)
        IEnumerable<Spell> Offered =>
            Class == null || !Class.Casts
                ? Enumerable.Empty<Spell>()
                : Library.Spells.For(Class.Id).Where(s => s.Solo != Solo.Unavailable);

        // the top of the class's own slot table at this level: a level 5 Paladin has 2nd-level
        // slots, not 3rd (SRD 5.2.1 p.53)
        public int HighestSpellLevel =>
            Class == null || !Class.Casts
                ? 1
                : Math.Clamp(SpellPoints.HighestLevelFor(Class.Progression, Level), 1, 9);

        // SRD 5.2.1's Cantrips column: a Wizard or Cleric 3, 4 at 4th level, 5 at 10th; a Druid
        // one fewer; a Paladin none. read off the spellcasting feature, and off the class's own
        // list for a class that says nothing; capped at the cantrips offered
        public int CantripPicks
        {
            get
            {
                int offered = Offered.Count(s => s.IsCantrip);

                if (offered == 0) return 0;

                int table = Class.Spellcasting.CantripsAt(Level);

                // never more than are on offer: without Spare the Dying a 10th-level Cleric has four
                // cantrips to learn where the SRD's column says five (cc_task_ui-issues-10-01.md 2.2)
                return Math.Min(offered, table >= 0 ? table : 2);
            }
        }

        // SRD 5.2.1's Prepared Spells column - the flat known/equipped model spends it as the
        // spells on the sheet. always-prepared spells come on top (Caster.Prepare)
        public int SpellPicks
        {
            get
            {
                if (Class == null || !Class.Casts) return 0;

                int table = Class.Spellcasting.KnownAt(Level);

                return table >= 0 ? table : 2 + Level;
            }
        }

        public int CantripPicksLeft =>
            Math.Max(0, CantripPicks - _spells.Count(s => s.IsCantrip));

        public int SpellPicksLeft =>
            Math.Max(0, SpellPicks - _spells.Count(s => !s.IsCantrip));

        public int Level { get; private set; } = 1;

        public void StartAt(int level)
        {
            Level = Proficiency.Clamp(level);

            // a lower starting level has fewer to spend
            while (_improvements.Count > ImprovementPicks) _improvements.RemoveAt(_improvements.Count - 1);
        }
    }
}
