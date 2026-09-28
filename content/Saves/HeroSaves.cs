using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Content.Sheet;
using Core.Characters;
using Core.Magic;
using Core.Words;

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
    public static partial class HeroSaves
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
    }
}
