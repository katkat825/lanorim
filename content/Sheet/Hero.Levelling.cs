using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Core.Characters;
using Core.Magic;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // Indomitable (SRD 5.2.1 p.48): so many rerolls a long rest, each adding the class level
        void Rerolls()
        {
            Feature reroll = Features.LastOrDefault(f => f.Trait == Trait.Reroll);

            Actor.SaveRerolls = reroll?.UsesAt(Level) ?? 0;
            Actor.SaveRerollBonus = reroll == null ? 0 : Level;
        }

        // the spells a feature always has prepared, and its free casts (SRD 5.2.1: a Life Domain's
        // spells, Paladin's Smite). only the ones v1 builds - the rest are reference cards
        static Content.Spells.SpellBook SrdSpells => Content.Spells.SpellBook.Srd();

        void Prepare()
        {
            // a species' spells arriving at 3rd level on a hero whose class casts nothing
            if (Caster == null && Features.Any(f => f.Spells.Count > 0 || f.InnateSpell != null))
                Caster = new Caster(Actor, InnateAbility());

            if (Caster == null) return;

            foreach (Feature feature in Features)
            {
                foreach (string id in feature.SpellsAt(Level))
                    if (SrdSpells.Find(id) is Spell spell) Caster.Prepare(spell);

                // a species' free cast comes with its spell's level (SRD 5.2.1 p.84), not before
                foreach (KeyValuePair<string, int> free in feature.FreeCasts)
                    if (!Caster.HasFree(free.Key) &&
                        (feature.Spells.Count == 0 || feature.SpellsAt(Level).Contains(free.Key)))
                        Caster.GrantFree(free.Key, free.Value);

                // the Dragonborn's Breath Weapon: its own spell, so many a long rest
                if (feature.InnateSpell != null)
                {
                    int uses = Math.Max(1, feature.UsesAt(Level));

                    Caster.Prepare(feature.InnateSpell);

                    if (Caster.UseOf(feature.InnateSpell.Id)?.Uses != uses)
                        Caster.Limit(feature.InnateSpell.Id, new SpellUse(0, uses));
                }
            }
        }

        // the best of the abilities a species lets its spells use (SRD 5.2.1: "Intelligence,
        // Wisdom, or Charisma"); Charisma when it names none
        Ability InnateAbility()
        {
            List<Ability> allowed = Features.SelectMany(f => f.SpellAbilities).Distinct().ToList();

            return allowed.Count == 0
                ? Ability.Charisma
                : allowed.OrderByDescending(a => Actor.AbilityModifier(a)).First();
        }

        // the class's hit points, and Dwarven Toughness's one more a level (SRD 5.2.1 p.84)
        int MaxHitPointsAt(int level) =>
            Class.HitPointsAt(level, Actor.AbilityModifier(Ability.Constitution)) +
            Features.Sum(f => f.MaxHitPointsPerLevel) * level;

        public void FightOver()
        {
            Actor.Boons.FightOver();
            Actor.EndConcentration();
            Equipment.Apply(Actor);
        }

        // milestone levelling: the campaign says when, and the sheet is rebuilt at the new level
        public void LevelTo(int level)
        {
            if (level <= Level) return;

            // the new level is written onto the druid's own body, never onto the bear's
            Revert();

            int was = Level;

            Actor.SetLevel(level);

            // an ASI level reached here is pending: the story does not wait on it, and the level-up
            // screen spends it when the player says (PendingImprovements, Improve)

            // only what this level brings. Grant is not idempotent - a speed bonus or a flat bonus
            // to a save adds on every call - and granting everything again on every level-up gave
            // a wood elf five more feet of speed each time
            foreach (Feature feature in Features.Where(f => f.Level > was))
                feature.Grant(Actor, level);

            int wasCurrent = Actor.Health.Current;
            int wasMax = Actor.Health.Maximum;

            Actor.SetHealth(new Health(MaxHitPointsAt(level), Class.HitDie, level));

            // levelling is not healing: the damage already taken comes with you
            Actor.Health.Take(Math.Max(0, wasMax - wasCurrent));

            // LEVELLING REBUILDS THE RESOURCE AND FILLS IT. A new level is a bigger table or a
            // bigger pool, and there is no sensible way to carry "three quarters spent" across a
            // change of shape - so a level-up is a rest for spells, the way it is for hit points.
            if (Class.Casts && Caster != null)
                Caster.Resource = Class.ResourceAt(level, Resource);

            Prepare();

            Budget = BuildBudget();
            Rerolls();
            Equipment.Apply(Actor);
        }
    }
}
