using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Magic
{
    // WHAT AN ACTOR NEEDS TO CAST ANYTHING: the ability it casts with, the spells on its sheet,
    // and the resource those spells are paid out of. Kept beside the Actor rather than inside it
    // so a goblin - which has none of them - carries none of it.
    //
    // THE RESOURCE IS AN INTERFACE AND THIS CLASS NEVER ASKS WHICH ONE IT IS. A character is
    // created with slots or with points and keeps that for life; everything below works the same
    // either way, because the only questions asked of it are can-you-pay and pay.
    public sealed class Caster
    {
        readonly List<Spell> _known = new List<Spell>();

        public Caster(Actor actor, Ability ability, ISpellResource resource = null)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Ability = ability;
            Resource = resource;
        }

        // HOW THIS CHARACTER PAYS. Null is a caster who has been handed spells but no
        // resource yet - a test hero, or a class still being outfitted - and it can cast
        // cantrips and nothing else, which is exactly what a caster with no magic left can do.
        public ISpellResource Resource { get; set; }

        public SpellResourceMode? Mode => Resource?.Mode;

        public Actor Actor { get; }

        // INT for a Mage, WIS for a Cleric or Druid, CHA for a Paladin
        public Ability Ability { get; }

        // the flat known/equipped model: the spells on your sheet are the ones you can cast. no
        // slot table, no daily preparation (decisions_checklist.md section 1).
        public IReadOnlyList<Spell> Known => _known;

        public void Learn(Spell spell)
        {
            if (spell != null && !_known.Any(s => s.Id == spell.Id)) _known.Add(spell);
        }

        public bool Forget(string id) => _known.RemoveAll(s => s.Id == id) > 0;

        public bool Knows(string id) => _known.Any(s => s.Id == id);

        public Spell Find(string id) => _known.FirstOrDefault(s => s.Id == id);

        public IEnumerable<Spell> Cantrips => _known.Where(s => s.IsCantrip);

        public IEnumerable<Spell> Leveled => _known.Where(s => !s.IsCantrip);

        // SRD: 8 + proficiency + the casting ability's modifier
        public int SaveDc => 8 + Actor.ProficiencyBonus + Actor.AbilityModifier(Ability);

        public int AttackModifier => Actor.ProficiencyBonus + Actor.AbilityModifier(Ability);

        // the highest level it can pay for right now; 0 when only cantrips are left
        public int HighestAffordable => Resource?.Highest ?? 0;

        public bool CanCast(Spell spell, int castAt)
        {
            if (spell == null || !Knows(spell.Id)) return false;

            if (castAt < spell.Level || castAt > 9) return false;

            // a cantrip is at-will and costs nothing, ever
            if (spell.IsCantrip) return true;

            return Resource != null && Resource.CanPay(castAt);
        }

        public IEnumerable<Spell> Castable =>
            _known.Where(s => CanCast(s, s.Level));

        // pay for a cast at this level. Cantrips are at-will in BOTH modes and never reach a
        // resource, which is why this is asked about a spell rather than about a level
        public bool Pay(Spell spell, int castAt)
        {
            if (spell == null) return false;

            if (spell.IsCantrip) return true;

            return Resource != null && Resource.Pay(castAt);
        }

        public void Rested(Rest rest) => Resource?.Restore(rest);

        public override string ToString() =>
            $"{Actor.Id} casts with {Ability.Id()}, dc {SaveDc}, " +
            (Resource?.Describe() ?? "no resource") + $", {_known.Count} spells";
    }

}
