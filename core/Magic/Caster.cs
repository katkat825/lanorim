using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Magic
{
    // what an actor needs to cast anything: the ability it casts with, the spells on its sheet,
    // and the mana pool those spells are paid out of. kept beside the Actor rather than inside it
    // so a goblin - which has neither - carries none of it.
    public sealed class Caster
    {
        readonly List<Spell> _known = new List<Spell>();

        public Caster(Actor actor, Ability ability)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Ability = ability;
        }

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
        public int HighestAffordable =>
            Enumerable.Range(1, 9).Where(l => Actor.CanAfford(l)).DefaultIfEmpty(0).Max();

        public bool CanCast(Spell spell, int castAt)
        {
            if (spell == null || !Knows(spell.Id)) return false;

            if (castAt < spell.Level || castAt > 9) return false;

            // a cantrip is at-will and costs nothing, ever
            if (spell.IsCantrip) return true;

            if (!Actor.CanAfford(spell.CostAt(castAt))) return false;

            return true;
        }

        public IEnumerable<Spell> Castable =>
            _known.Where(s => CanCast(s, s.Level));

        public override string ToString() =>
            $"{Actor.Id} casts with {Ability.Id()}, dc {SaveDc}, " +
            $"{Actor.Mana}/{Actor.ManaMax} mana, {_known.Count} spells";
    }

    // the mana pool's arithmetic in one place, so the class data files and the character sheet
    // agree about what a level-7 Cleric has to spend.
    public static class Mana
    {
        // a leveled spell costs its level; upcasting costs the higher level. one line, and it is
        // the whole resource system (decisions_checklist.md section 1).
        public static int CostOf(int spellLevel) => Math.Max(0, spellLevel);

        // the pool a class of this shape has at this level. the two numbers come from the class
        // data file, so a full caster and a half caster differ in data and not in code.
        public static int Pool(int casterLevel, int perLevel, int flat, int abilityModifier) =>
            Math.Max(0, perLevel * Proficiency.Clamp(casterLevel) + flat + abilityModifier);
    }
}
