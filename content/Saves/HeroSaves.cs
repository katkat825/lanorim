using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Schema;
using Content.Sheet;
using Content.Species;
using Core.Characters;
using Core.Magic;

namespace Content.Saves
{
    // A LIVE HERO TO A SAVED ONE, AND BACK. SaveWriter and SaveReader know what a save looks like
    // on disk; this is what knows what a Hero IS, and so which of its numbers are the character's
    // and which are the rules'.
    //
    // THE RULE IS THE SAME ONE SaveGame STARTS WITH: ids and what happened, never what the rules
    // work out. Armor class, maximum hit points, the slot table, the uses a feature has - none of
    // it is written, all of it comes back off the class and the item when Build runs again. So a
    // retuned fighter reaches an old fighter, and a save cannot hold the rules still.
    //
    // RESTORING READS AS FAR AS IT CAN, the way SaveReader does. An item or a spell this build does
    // not have is a caution and the rest of the hero comes back; only a class or a species that
    // cannot be found is refused, because there is no hero to build without one.
    public static class HeroSaves
    {
        public static SavedHero Capture(Hero hero)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            Actor actor = hero.Actor;

            var saved = new SavedHero
            {
                Name = hero.Name ?? "",
                Class = hero.Class.Id,
                Species = hero.Species.Id,
                Lineage = hero.Lineage?.Id ?? "",
                Background = hero.Background?.Id ?? "",
                Alignment = hero.Alignment.Id(),
                Level = hero.Level,
                HitPoints = actor.Health.Current,
                TemporaryHitPoints = actor.Health.Temporary,
                HitDice = actor.Health.HitDice,
                Resource = hero.Resource,
                Gold = hero.Pack.Gold,
                ExtraActions = hero.Budget.ExtraActionsLeft,
                Form = hero.Form?.Id ?? "",
            };

            // a hero nobody built has no array to go back to, and its base scores are the nearest
            // thing. every hero the game makes has been built, so this is for tests and tools
            foreach (Ability ability in Abilities.All)
                saved.Scores[ability] = hero.Picked != null &&
                                        hero.Picked.TryGetValue(ability, out int picked)
                    ? picked
                    : actor.Scores.Base(ability);

            foreach (KeyValuePair<Ability, int> spend in hero.BackgroundSpend)
                saved.BackgroundSpend[spend.Key] = spend.Value;

            foreach (AbilityImprovement improvement in hero.Improvements)
                saved.Improvements.Add(improvement.Word);

            saved.PendingImprovements = hero.PendingImprovements;
            saved.DiscardWarningDismissed = hero.DiscardWarningDismissed;

            // ALL the training, not only the picks. Build hands the lot back as chosen skills and
            // training never demotes, so what the background and the species gave is given twice
            // and changes nothing - and a skill trained by hand after creation still comes back
            foreach (Skill skill in actor.TrainedSkills)
            {
                saved.Skills.Add(skill);

                if (actor.TrainingIn(skill) == Training.Expert) saved.Expertise.Add(skill);
            }

            // a set iterates in whatever order it was filled, and the save has to diff
            foreach (Condition condition in actor.Conditions.OrderBy(c => (int)c))
                saved.Conditions.Add(condition);

            foreach (KeyValuePair<string, int> spent in hero.Spent.Where(s => s.Value > 0))
                saved.Spent[spent.Key] = spent.Value;

            CaptureMagic(hero, saved);

            foreach (KeyValuePair<Slot, Item> worn in hero.Equipment.Worn)
                saved.Worn[worn.Key] = worn.Value.Id;

            // one line per item however many stacks it fills: stacking is the pack's business,
            // and it stacks them again on the way back in
            foreach (IGrouping<string, Inventory.Stack> item in hero.Pack.Everything
                                                                     .GroupBy(s => s.Item.Id))
                saved.Pack.Add(new SavedStack(item.Key, item.Sum(s => s.Count)));

            return saved;
        }

        static void CaptureMagic(Hero hero, SavedHero saved)
        {
            if (!hero.Casts) return;

            foreach (Spell spell in hero.Caster.Known) saved.Known.Add(spell.Id);

            switch (hero.Caster.Resource)
            {
                case SpellSlots slots:
                    // what is LEFT, up to the highest level the table gives - the maxima are the
                    // table's and come back off it
                    int top = Enumerable.Range(SpellLevels.Lowest, SpellLevels.Highest)
                                        .LastOrDefault(l => slots.Maximum(l) > 0);

                    for (int level = SpellLevels.Lowest; level <= top; level++)
                        saved.Slots.Add(slots.Remaining(level));
                    break;

                case SpellPoints points:
                    saved.Points = points.Remaining;

                    for (int level = SpellLevels.HighLevel; level <= SpellLevels.Highest; level++)
                        if (points.HasSpent(level)) saved.SpentHighLevels.Add(level);
                    break;
            }
        }


        // --- restoring -------------------------------------------------------------------------

        public static Hero Restore(SavedHero saved, Library library,
                                   out IReadOnlyList<ContentProblem> problems,
                                   string file = "save.json")
        {
            var found = new List<ContentProblem>();
            problems = found;

            if (saved == null || library == null)
            {
                found.Add(new ContentProblem(file, "hero", "there is no hero in this save to restore"));
                return null;
            }

            // the two refusals: without a class or a species there is nothing Build can build, and
            // a hero with a guessed class would be a stranger wearing the player's name
            CharacterClass cls = library.Class(saved.Class);

            if (cls == null)
                found.Add(new ContentProblem(file, "hero.class",
                    $"'{saved.Class}' is not a class this build has, so this hero cannot be built"));

            Kind species = library.Kind(saved.Species);

            if (species == null)
                found.Add(new ContentProblem(file, "hero.species",
                    $"'{saved.Species}' is not a species this build has, so this hero cannot be " +
                    "built"));

            if (cls == null || species == null) return null;

            Kind lineage = null;

            if (!string.IsNullOrEmpty(saved.Lineage))
            {
                lineage = library.Kind(saved.Lineage);

                if (lineage == null)
                    found.Add(ContentProblem.Caution(file, "hero.lineage",
                        $"'{saved.Lineage}' is not a lineage this build has - this " +
                        $"{saved.Species} is being read without one"));
            }

            Background background = null;

            if (!string.IsNullOrEmpty(saved.Background))
            {
                background = library.Background(saved.Background);

                if (background == null)
                    found.Add(ContentProblem.Caution(file, "hero.background",
                        $"'{saved.Background}' is not a background this build has - this hero " +
                        "is being read without one, and without its skills and its +2/+1"));
                else if (saved.BackgroundSpend.Count > 0 &&
                         !background.IsLegalSpend(saved.BackgroundSpend.ToDictionary(p => p.Key,
                                                                                     p => p.Value),
                                                  out string why))
                    // spent as saved all the same: the scores are the player's, and a changed
                    // background is a sentence to read rather than a reason to move them
                    found.Add(ContentProblem.Caution(file, "hero.background_spend",
                        $"{why} - spent as it was saved"));
            }

            Alignment alignment = Alignment.Neutral;

            if (!string.IsNullOrEmpty(saved.Alignment) &&
                !Alignments.TryParse(saved.Alignment, out alignment))
                found.Add(ContentProblem.Caution(file, "hero.alignment",
                    $"'{saved.Alignment}' is not an alignment - this hero is being read as " +
                    "neutral"));

            var hero = new Hero(saved.Name, cls, species, background,
                                AbilityScores.From(saved.Scores.ToDictionary(p => p.Key,
                                                                             p => p.Value)),
                                saved.Level, lineage, saved.Resource)
            {
                Alignment = alignment,
            };

            // BUILT, NOT PATCHED: the species, the background's spend, the class and the
            // improvements all run again, so everything the rules work out is the rules' today.
            // no shelf, because the starting kit is what the pack below replaces
            hero.Build(background == null
                           ? null
                           : saved.BackgroundSpend.ToDictionary(p => p.Key, p => p.Value),
                       saved.Skills, saved.Expertise, null,
                       Spells(saved, library, hero, file, found),
                       Improvements(saved, file, found));

            Improvements(saved, hero, file, found);

            hero.DiscardWarningDismissed = saved.DiscardWarningDismissed;

            RestoreKit(saved, library, hero, file, found);
            RestoreDay(saved, library, hero, file, found);

            return hero;
        }

        static IEnumerable<AbilityImprovement> Improvements(SavedHero saved, string file,
                                                            List<ContentProblem> found)
        {
            var spent = new List<AbilityImprovement>();

            foreach (string word in saved.Improvements)
            {
                if (AbilityImprovement.TryParse(word, out AbilityImprovement one)) spent.Add(one);
                else
                    found.Add(ContentProblem.Caution(file, "hero.improvements",
                        $"'{word}' is not an ability score improvement - it is left to spend again"));
            }

            return spent;
        }

        // what came back and what did not: an old save's improvements are pending, never re-spent
        static void Improvements(SavedHero saved, Hero hero, string file, List<ContentProblem> found)
        {
            if (!saved.ImprovementsRecorded && hero.PendingImprovements > 0)
                found.Add(ContentProblem.Caution(file, "hero.improvements",
                    $"this save is from before the player chose ability score improvements; " +
                    $"{hero.PendingImprovements} are waiting to be spent on the level-up screen"));

            foreach (AbilityImprovement refused in hero.RefusedImprovements)
                found.Add(ContentProblem.Caution(file, "hero.improvements",
                    $"'{refused.Word}' no longer fits (a score would pass 20, or there is no " +
                    "improvement left at this level) - it is left to spend again"));

            if (saved.ImprovementsRecorded && saved.PendingImprovements != hero.PendingImprovements)
                found.Add(ContentProblem.Caution(file, "hero.improvements",
                    $"the save says {saved.PendingImprovements} improvements were waiting and the " +
                    $"sheet makes it {hero.PendingImprovements} - the sheet's number is kept"));
        }

        static IEnumerable<Spell> Spells(SavedHero saved, Library library, Hero hero, string file,
                                         List<ContentProblem> found)
        {
            var spells = new List<Spell>();

            foreach (string id in saved.Known)
            {
                Spell spell = library.Spells.Find(id);

                if (spell == null)
                {
                    found.Add(ContentProblem.Caution(file, "hero.known",
                        $"'{id}' is not a spell this build has - it is left off the sheet"));
                    continue;
                }

                spells.Add(spell);
            }

            if (spells.Count > 0 && !hero.Class.Casts)
                found.Add(ContentProblem.Caution(file, "hero.known",
                    $"a {hero.Class.Id} does not cast, so the spells in this save are left off"));

            return spells;
        }

        // the pack before what is worn, and what is worn straight onto the body rather than
        // through Hero.Wear - Wear takes one out of the pack, and a spare sword in the pack is a
        // spare sword, not the one in the hand
        static void RestoreKit(SavedHero saved, Library library, Hero hero, string file,
                               List<ContentProblem> found)
        {
            foreach (SavedStack stack in saved.Pack)
            {
                Item item = library.Items.Find(stack.Item);

                if (item == null)
                {
                    found.Add(ContentProblem.Caution(file, "hero.pack",
                        $"'{stack.Item}' is not an item this build has - it is left out of the " +
                        "pack"));
                    continue;
                }

                int took = hero.Pack.Take(item, stack.Count);

                if (took < stack.Count)
                    found.Add(ContentProblem.Caution(file, "hero.pack",
                        $"{stack.Count - took} of '{stack.Item}' did not fit in the pack"));
            }

            hero.Pack.Earn(saved.Gold);

            foreach (Slot slot in saved.Worn.Keys.OrderBy(s => (int)s))
            {
                string id = saved.Worn[slot];
                Item item = library.Items.Find(id);

                if (item == null)
                {
                    found.Add(ContentProblem.Caution(file, "hero.worn",
                        $"'{id}' is not an item this build has - nothing is worn in its place"));
                    continue;
                }

                string refused = hero.Equipment.Refuses(item, hero.Actor, hero.Class.Id);

                if (refused != null)
                {
                    // a thing that may no longer be worn is still owned, so it goes in the pack
                    found.Add(ContentProblem.Caution(file, "hero.worn",
                        $"'{id}' cannot be worn ({refused}) - it is in the pack instead"));
                    hero.Pack.Take(item);
                    continue;
                }

                hero.Equipment.Wear(item, hero.Actor, hero.Class.Id);
            }
        }

        // what the day has done to the hero: the shape, the hit points, the conditions, the uses
        // spent and the magic left. all of it after Build, which hands everything back full
        static void RestoreDay(SavedHero saved, Library library, Hero hero, string file,
                               List<ContentProblem> found)
        {
            Actor actor = hero.Actor;

            // the card first, so an incapacitating condition below takes it off again the way
            // the rules say it would have
            if (!string.IsNullOrEmpty(saved.Form))
            {
                Form form = library.Forms.Find(saved.Form);

                if (form == null || !hero.Resume(form))
                    found.Add(ContentProblem.Caution(file, "hero.form",
                        $"'{saved.Form}' is not a shape this hero can wear - it is back in its " +
                        "own body"));
            }

            // -1 is an untouched hero, and anything over the maximum is a retuned class
            if (saved.HitPoints >= 0)
                actor.Health.Take(Math.Max(0, actor.Health.Maximum - saved.HitPoints));

            actor.Health.GrantTemporary(saved.TemporaryHitPoints);

            if (saved.HitDice >= 0) actor.Health.SetHitDice(saved.HitDice);

            foreach (Condition condition in saved.Conditions) actor.Apply(condition);

            foreach (KeyValuePair<string, int> spent in saved.Spent
                                                          .OrderBy(s => s.Key,
                                                                   StringComparer.Ordinal))
            {
                Feature feature = hero.Features.FirstOrDefault(f => f.Id == spent.Key &&
                                                                    f.Uses > 0);

                if (feature == null)
                {
                    found.Add(ContentProblem.Caution(file, "hero.spent",
                        $"'{spent.Key}' is not a feature with uses this hero has - its uses are " +
                        "forgotten"));
                    continue;
                }

                hero.Respend(feature, spent.Value);
            }

            // Build banks every per-rest extra; drawing is the only way one leaves the bank
            if (saved.ExtraActions >= 0)
                while (hero.Budget.ExtraActionsLeft > saved.ExtraActions &&
                       hero.Budget.DrawExtraAction())
                {
                }

            RestoreMagic(saved, hero, file, found);
        }

        static void RestoreMagic(SavedHero saved, Hero hero, string file,
                                 List<ContentProblem> found)
        {
            switch (hero.Caster?.Resource)
            {
                case SpellSlots slots:
                    for (int i = 0; i < saved.Slots.Count; i++)
                        slots.SetRemaining(SpellLevels.Lowest + i, saved.Slots[i]);
                    break;

                case SpellPoints points:
                    points.Resume(saved.Points >= 0 ? saved.Points : points.Maximum,
                                  saved.SpentHighLevels);
                    break;
            }
        }
    }
}
