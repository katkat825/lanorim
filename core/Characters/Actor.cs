using System;
using System.Collections.Generic;

namespace Core.Characters
{
    public enum Allegiance
    {
        Hero,
        Enemy,

        // an ally the campaign runs, and the companion - neither is controlled by the player
        Friendly,
    }

    // anything that can be rolled for: the hero, a goblin, a summoned spirit. core knows nothing
    // about classes, species or items - the content layer assembles one of these and hands it over.
    public sealed partial class Actor
    {
        public Actor(string id, int level = 1, AbilityScores scores = null, Allegiance side = Allegiance.Enemy)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Boons.Bearer = this;
            Level = Proficiency.Clamp(level);
            Scores = scores ?? new AbilityScores();
            Side = side;
            SetHealth(new Health(1));
        }

        public string Id { get; }

        public Allegiance Side { get; }

        public int Level { get; private set; }

        public AbilityScores Scores { get; }

        public Health Health { get; private set; }

        // SRD 5.2.1 default; a species or an effect moves it. what is added only out of heavy
        // armor (the Barbarian's Fast Movement) rides on top, so "Speed += n" keeps working
        public int Speed
        {
            get => _speed + LightFootedBonus - ArmorDrag;
            set => _speed = value - LightFootedBonus + ArmorDrag;
        }

        int _speed = 30;

        // SRD 5.2.1 Fast Movement (p.30): "while you aren't wearing Heavy armor"
        public int SpeedOutOfHeavyArmor { get; set; }

        int LightFootedBonus => Armor.Category == ArmorCategory.Heavy ? 0 : SpeedOutOfHeavyArmor;

        // SRD 5.2.1 armor (p.92): armor with a Strength score costs 10 feet of Speed to a wearer
        // below it. a statblock's armor names none, so a monster never pays it
        int ArmorDrag =>
            Armor.StrengthRequirement > 0 && Scores.Score(Ability.Strength) < Armor.StrengthRequirement ? 10 : 0;

        // what kind of creature it is and what it is by nature: "humanoid", "undead", "construct"
        // from a statblock, "sleepless" from an elf's Trance. a spell that only works on some
        // creatures reads these (Hold Person, Divine Smite's fiends and undead)
        readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Tags => _tags;

        public bool Is(string tag) => !string.IsNullOrEmpty(tag) && _tags.Contains(tag);

        public void Tag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag)) _tags.Add(tag.Trim());
        }

        // SRD size category. every creature is one square on the v1 board whatever its size (the
        // grid has no multi-square creatures); size is read by the spells that limit what they
        // can move (Telekinesis' "Huge or smaller")
        public Size Size { get; set; } = Size.Medium;
    }
}
