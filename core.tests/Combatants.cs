using Core.Characters;
using Core.Dice;

namespace Core.Tests
{
    static class Combatants
    {
        public static Actor Hero(int hp = 40, int ac = 16)
        {
            var hero = new Actor("hero", 5, new AbilityScores(18, 14, 16, 10, 12, 10),
                                 Allegiance.Hero);

            hero.SetHealth(new Health(hp, Die.D10, 5));
            hero.Armor = new ArmorProfile(ArmorCategory.Heavy, ac);

            return hero;
        }

        public static Actor Goblin(string id = "goblin", int hp = 7)
        {
            var goblin = new Actor(id, 1, new AbilityScores(8, 14, 10, 10, 8, 8));

            goblin.SetHealth(new Health(hp, Die.D6, 2));
            goblin.Armor = new ArmorProfile(ArmorCategory.Light, 13);
            goblin.HasShield = true;
            goblin.Speed = 30;

            return goblin;
        }

        public static readonly Attack Scimitar =
            new Attack("scimitar", DiceRoll.Parse("1d6"), DamageType.Slashing,
                       Ability.Dexterity, finesse: true);

        public static readonly Attack Shortbow =
            new Attack("shortbow", DiceRoll.Parse("1d6"), DamageType.Piercing,
                       Ability.Dexterity, range: 16, longRange: 64);

        public static readonly Attack Longsword =
            new Attack("longsword", DiceRoll.Parse("1d8"), DamageType.Slashing);
    }
}
