using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Content.Species;
using Core.Characters;
using Core.Dice;
using Core.Magic;

namespace Content.Sheet
{
    // the whole character: the Actor the rules roll against, plus everything the rules do not
    // need to know about - which class it is, what is in the pack, which spells are on the card
    // rack. the character sheet UI binds to this; nothing here draws anything.
    public sealed partial class Hero
    {
        public Hero(string name, CharacterClass cls, Kind species, Background background,
                    AbilityScores scores, int level = 1, Kind lineage = null,
                    SpellResourceMode resource = SpellResourceMode.Slots)
        {
            Name = name ?? "";
            Class = cls ?? throw new ArgumentNullException(nameof(cls));
            Species = species ?? throw new ArgumentNullException(nameof(species));
            Background = background;
            Lineage = lineage;
            Resource = resource;

            Actor = new Actor(Id(name), level, scores ?? new AbilityScores(), Allegiance.Hero);

            // SRD 5.2.1: every playable species is a Humanoid - what Hold Person and Charm Person
            // read
            Actor.Tag("humanoid");

            // and what kind: "elf" is what a Ghoul's claw reads
            Actor.Tag(Species.Id);

            Pack = new Pack();
            Equipment = new Equipment();
        }

        // WHICH WAY THIS CHARACTER PAYS FOR LEVELED SPELLS, chosen once at creation and kept for
        // life. Slots is the default because it is the SRD's, and because a player who does not
        // care which they have should end up with the faithful one.
        //
        // It is settable so a save can put back the mode it was written with, and so the creation
        // screen can flip it while the character is still being built. Flipping it after that is
        // not a supported move: it would hand the character a full resource of the other shape,
        // which is a free long rest.
        public SpellResourceMode Resource { get; set; }

        // identity only - nothing reads it but the sheet (character_sheet_decisions.md)
        public Alignment Alignment { get; set; } = Alignment.Neutral;

        // the player types a name, so it is not a localization key - it is the one string in the
        // game that is neither authored nor translated
        public string Name { get; set; }

        public Actor Actor { get; }

        public CharacterClass Class { get; }

        public Kind Species { get; }

        public Kind Lineage { get; }

        public Background Background { get; }

        public Pack Pack { get; }

        public Equipment Equipment { get; }

        public Caster Caster { get; private set; }

        public int Level => Actor.Level;

        public bool Casts => Caster != null;

        static string Id(string name)
        {
            // the actor id has to survive being a key segment; the player's own spelling does not
            var id = new System.Text.StringBuilder();

            foreach (char c in (name ?? "").ToLowerInvariant())
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') id.Append(c);
                else if (c == ' ' || c == '_' || c == '-') id.Append('_');

            return id.Length == 0 ? "hero" : id.ToString();
        }

        public IEnumerable<Feature> Features =>
            Class.By(Level).Concat(Species.Features.Where(f => f.Level <= Level))
                 .Concat(Lineage?.Features.Where(f => f.Level <= Level) ??
                         Enumerable.Empty<Feature>());

        // one of them by id, or null
        public Feature FeatureCalled(string id) => Features.FirstOrDefault(f => f.Id == id);

        // what the "Special" section of the sheet lists: the things with a button on them
        public IEnumerable<Feature> Activatable => Features.Where(f => f.IsActive);

        // a borrowed shape swings with its own claws, and the sword stays on the sheet for after.
        // everyone has an Unarmed Strike (SRD 5.2.1 p.190)
        public IEnumerable<Attack> Attacks =>
            Form != null ? Form.Attacks : Equipment.Attacks.Select(Wielded).Append(UnarmedStrike);

        // a weapon as this hero swings it: the proficiency bonus only with a weapon the class
        // trains with, and a Versatile weapon's bigger die with both hands free for it (SRD 5.2.1
        // p.89)
        Attack Wielded(Attack attack)
        {
            bool trained = Class.TrainedWith(attack);
            bool twoHanded = !attack.Versatile.IsNothing && !Actor.HasShield &&
                             Equipment.In(Slot.OffHand) == null;

            if (trained && !twoHanded) return attack;

            return attack.With(proficient: trained && attack.Proficient,
                               damage: twoHanded ? attack.Versatile : attack.Damage);
        }

        // SRD 5.2.1 Unarmed Strike: an attack roll with Strength and Proficiency, 1 + Strength
        // modifier Bludgeoning. no hand to drop it from
        public static readonly Attack UnarmedStrike =
            new Attack("unarmed_strike", DiceRoll.Flat(1), DamageType.Bludgeoning, Ability.Strength,
                       proficient: true, reach: 1, hand: Hand.None);

        public IEnumerable<Spell> Spells => Caster?.Known ?? Enumerable.Empty<Spell>();

        public override string ToString() =>
            $"{Name} the level {Level} {Species.Id} {Class.Id}: {Actor.Health}, " +
            $"ac {Actor.ArmorClass}" +
            (Casts ? $", {Caster.Resource?.Describe()}, {Caster.Known.Count} spells" : "") +
            $", {Pack}";
    }
}
