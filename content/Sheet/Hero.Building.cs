using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Core.Characters;
using Core.Magic;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- building -------------------------------------------------------------------------

        // everything the class, species, background and gear do to the actor, applied in the SRD
        // order: species and background before the class, because the class's hit points read
        // Constitution after the background has raised it.
        public void Build(IReadOnlyDictionary<Ability, int> backgroundSpend = null,
                          IEnumerable<Skill> chosenSkills = null,
                          IEnumerable<Skill> expertise = null,
                          ItemShelf shelf = null,
                          IEnumerable<Spell> spells = null,
                          IEnumerable<AbilityImprovement> improvements = null)
        {
            // what Build was handed, kept so a save can hand it over again. the finished scores
            // are not the character: they are the array plus whatever the rules did to it, and
            // saving the array is how a retuned species or improvement reaches an old save
            Picked = Abilities.All.ToDictionary(a => a, a => Actor.Scores.Base(a));
            BackgroundSpend = new Dictionary<Ability, int>(
                backgroundSpend ?? new Dictionary<Ability, int>());

            Species.Outfit(Actor, Level);
            Lineage?.Outfit(Actor, Level);
            Background?.Outfit(Actor, backgroundSpend);

            Class.Outfit(Actor, Level, chosenSkills);

            foreach (Skill skill in expertise ?? Enumerable.Empty<Skill>())
                Actor.Train(skill, Training.Expert);

            // THE ASI LEVELS ARE THE PLAYER'S (decisions_checklist.md section 1, 2026-09-24): only
            // the improvements handed in - chosen at creation, or read back from a save - are
            // spent. the rest wait on the sheet as PendingImprovements for the level-up screen
            _improvements.Clear();
            RefusedImprovements = Respend(improvements);

            // hit points are read after the bumps, so they have to be set again
            Actor.SetHealth(new Health(MaxHitPointsAt(Level), Class.HitDie, Level));

            if (Class.Casts)
            {
                Caster = new Caster(Actor, Class.CastingAbility.Value,
                                    Class.ResourceAt(Level, Resource));

                foreach (Spell spell in spells ?? Enumerable.Empty<Spell>()) Caster.Learn(spell);
            }

            // a species' own spells, for a hero whose class casts nothing: cantrips, the free
            // casts a day and nothing else (SRD 5.2.1: the High Elf's, the Tiefling's legacy)
            else if (Features.Any(f => f.Spells.Count > 0 || f.InnateSpell != null))
            {
                Caster = new Caster(Actor, InnateAbility());
            }

            Prepare();

            if (shelf != null) Kit(shelf);

            Budget = BuildBudget();
            Rerolls();
        }

        // the scores as picked, before the species, the background or an improvement touched
        // them; null on a hero nobody has built
        public IReadOnlyDictionary<Ability, int> Picked { get; private set; }

        // the +2/+1 or +1/+1/+1 the player spent. the background only says where it MAY go
        public IReadOnlyDictionary<Ability, int> BackgroundSpend { get; private set; } =
            new Dictionary<Ability, int>();

        // SRD grants one at 4, 8, 12, 16 and 19
        public static int AbilityScoreImprovements(int level) =>
            CharacterClass.UsualImprovementLevels.Count(l => level >= l);

        // the class's own levels: the Fighter's 6 and 14, the Rogue's 10
        public static int AbilityScoreImprovements(int level, CharacterClass cls) =>
            cls?.ImprovementsBy(level) ?? AbilityScoreImprovements(level);

        // improvements Build was handed that no longer fit the rule (a save from before a change,
        // a score that is now over 20): left pending rather than forced
        public IReadOnlyList<AbilityImprovement> RefusedImprovements { get; private set; } =
            Array.Empty<AbilityImprovement>();

        void Kit(ItemShelf shelf)
        {
            foreach (string id in Class.StartingGear.Concat(Background?.Gear ??
                                                            Enumerable.Empty<string>()))
            {
                Item item = shelf.Find(id);

                if (item == null) continue;

                Pack.Take(item);
            }

            Pack.Earn((Background?.Gold ?? 0) + Class.Gold);

            // put the best of it on: the highest armor class body armor it may wear, a shield if
            // the class trains with one, and the biggest weapon
            foreach (Item armor in Pack.Stacks.Select(s => s.Item)
                                      .Where(i => i.Kind == ItemKind.Armor &&
                                                  i.Armor.HasValue &&
                                                  Class.ArmorTraining.Contains(i.Armor.Value.Category))
                                      .OrderByDescending(i => i.Armor.Value.BaseArmorClass)
                                      .Take(1))
                Wear(armor);

            // the biggest weapon it trains with, melee before ranged; a one-handed one only when
            // there is a shield to carry with it (the Barbarian's greataxe, not a handaxe)
            List<Item> arms = Pack.Stacks.Select(s => s.Item)
                                  .Where(i => i.Attack != null)
                                  .OrderByDescending(i => Class.TrainedWith(i.Attack))
                                  .ThenByDescending(i => i.Attack.Damage.Average)
                                  .ThenBy(i => i.Attack.IsRanged)
                                  .ToList();

            bool shielded = Class.Shields && Pack.Stacks.Any(s => s.Item.Kind == ItemKind.Shield);

            Item weapon = (shielded ? arms.FirstOrDefault(i => i.Slot != Slot.TwoHand) : null)
                       ?? arms.FirstOrDefault();

            if (weapon != null) Wear(weapon);

            if (Class.Shields)
                foreach (Item shield in Pack.Stacks.Select(s => s.Item)
                                            .Where(i => i.Kind == ItemKind.Shield).Take(1))
                    Wear(shield);

            foreach (Item trinket in Pack.Stacks.Select(s => s.Item)
                                         .Where(i => i.Slot == Slot.Trinket).Take(1))
                Wear(trinket);
        }
    }
}
