using System;
using System.Collections.Generic;

namespace Core.Characters
{
    // a borrowed body: the numbers a Wild Shape puts in place of an actor's own while it is worn.
    // core knows nothing about druids or form cards - the content layer reads one of those and
    // hands over only what the rules roll against. the mind stays put: Intelligence, Wisdom,
    // Charisma, training, saves, level and hit points are the actor's own the whole time.
    public sealed class Shape
    {
        public Shape(string id, string source, int strength, int dexterity, int constitution,
                     int armorClass, int speed, IReadOnlyList<Skill> skills = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? id;
            Strength = strength;
            Dexterity = dexterity;
            Constitution = constitution;
            ArmorClass = armorClass;
            Speed = Math.Max(0, speed);
            Skills = skills ?? Array.Empty<Skill>();
        }

        public string Id { get; }

        // the feature that made it. the boons a shape hands out are stamped with this, so taking
        // the shape off takes exactly those and never a Bless that happened to land meanwhile
        public string Source { get; }

        public int Strength { get; }

        public int Dexterity { get; }

        public int Constitution { get; }

        // the statblock's number, flat. worn armor and a shield do nothing on a bear
        public int ArmorClass { get; }

        // walking speed. a climb or a swim is the campaign's to read off the card, not the grid's
        public int Speed { get; }

        // what the creature is trained in; the wearer gets them while it is worn
        public IReadOnlyList<Skill> Skills { get; }

        public static readonly IReadOnlyList<Ability> Physical = new[]
        {
            Ability.Strength, Ability.Dexterity, Ability.Constitution,
        };

        public int Score(Ability ability) => ability switch
        {
            Ability.Strength => Strength,
            Ability.Dexterity => Dexterity,
            Ability.Constitution => Constitution,
            _ => throw new ArgumentOutOfRangeException(nameof(ability),
                                                       "a shape only lends the body"),
        };

        public override string ToString() =>
            $"{Id}: str {Strength} dex {Dexterity} con {Constitution}, ac {ArmorClass}, " +
            $"speed {Speed}";
    }
}
