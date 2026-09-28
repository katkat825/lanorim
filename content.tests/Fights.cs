using System.Linq;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // WHAT THE SPELL AND FIGHT TESTS SHARE: the book, the hall, scripted dice, a field to fight on,
    // a wizard and a goblin. a test file takes them with `using static Content.Tests.Fights;`, and a
    // file that needs its own hall or its own dice keeps that one thing (cc_task_dedupe-methods.md
    // #15: five files each had Field, and Wizard and Goblin were copied between three)
    internal static class Fights
    {
        public static readonly SpellBook Book = SpellBook.Srd();

        // eleven by five, all floor
        public const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        // a script: the two initiative rolls, then whatever else, then ones for ever after
        public static IRng Script(params int[] rolls) =>
            new ScriptedRng(rolls.Concat(Enumerable.Repeat(1, 400)).ToArray());

        public static Encounter Field(IRng rng, string hall = Hall, ICombatObserver observer = null)
        {
            Assert.True(MapReader.TryRead(hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), observer ?? new CombatLog());
        }

        public static Caster Wizard(out Actor actor, int level = 17)
        {
            actor = new Actor("wizard", level, new AbilityScores(10, 10, 14, 18, 10, 10),
                              Allegiance.Hero);
            actor.SetHealth(new Health(80, Die.D6, level));

            var caster = new Caster(actor, Ability.Intelligence,
                                    SpellSlots.For(CasterProgression.Full, level));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        public static Actor Goblin(string id = "goblin", int hp = 100, params string[] tags)
        {
            var goblin = new Actor(id, 1, new AbilityScores());
            goblin.SetHealth(new Health(hp));
            goblin.Armor = new ArmorProfile(ArmorCategory.Heavy, 10);

            foreach (string tag in tags) goblin.Tag(tag);

            return goblin;
        }
    }
}
