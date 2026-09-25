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

        // ALWAYS PREPARED (SRD 5.2.1): a feature's spells - a Life Domain's, Paladin's Smite's
        // Divine Smite. on the sheet like any other, and not counted against the picks
        readonly HashSet<string> _prepared = new(StringComparer.Ordinal);

        public void Prepare(Spell spell)
        {
            if (spell == null) return;

            Learn(spell);
            _prepared.Add(spell.Id);
        }

        public bool IsPrepared(string id) => id != null && _prepared.Contains(id);

        public IEnumerable<Spell> Picked => _known.Where(s => !_prepared.Contains(s.Id));

        // casts at the spell's own level that cost no slot or points, so many each long rest:
        // Paladin's Smite's one Divine Smite (SRD 5.2.1 p.54)
        readonly Dictionary<string, (int PerDay, int Left)> _free = new(StringComparer.Ordinal);

        public void GrantFree(string spellId, int perLongRest)
        {
            if (string.IsNullOrEmpty(spellId) || perLongRest <= 0) return;

            _free[spellId] = (perLongRest, perLongRest);
        }

        public bool HasFree(string spellId) => spellId != null && _free.ContainsKey(spellId);

        public int FreeLeft(string spellId) =>
            spellId != null && _free.TryGetValue(spellId, out var f) ? f.Left : 0;

        public bool Knows(string id) => _known.Any(s => s.Id == id);

        public Spell Find(string id) => _known.FirstOrDefault(s => s.Id == id);

        public IEnumerable<Spell> Cantrips => _known.Where(s => s.IsCantrip);

        public IEnumerable<Spell> Leveled => _known.Where(s => !s.IsCantrip);

        // SRD: 8 + proficiency + the casting ability's modifier - or, for a monster, the number its
        // statblock prints (FixedDc), because a statblock's DC is data, not a formula
        public int SaveDc => FixedDc ?? 8 + Actor.ProficiencyBonus + Actor.AbilityModifier(Ability);

        public int AttackModifier =>
            FixedAttack ?? Actor.ProficiencyBonus + Actor.AbilityModifier(Ability);

        public int? FixedDc { get; set; }

        public int? FixedAttack { get; set; }

        // PER-SPELL LIMITS, for a statblock: "Fire Breath (Recharge 5-6)", "1/day each". a spell
        // with no entry here is limited only by the resource, the way a hero's spells are
        readonly Dictionary<string, SpellUse> _uses = new Dictionary<string, SpellUse>();

        public void Limit(string spellId, SpellUse use)
        {
            if (!string.IsNullOrEmpty(spellId) && use != null) _uses[spellId] = use;
        }

        public SpellUse UseOf(string spellId) =>
            spellId != null && _uses.TryGetValue(spellId, out SpellUse use) ? use : null;

        // the start of its turn: each spent recharge rolls its d6
        public void Recharge(Core.Resolution.IResolver resolver)
        {
            foreach (SpellUse use in _uses.Values.Where(u => u.Recharge > 0 && !u.Ready))
                if (resolver.Roll(new Core.Dice.DiceRoll(1, Core.Dice.Die.D6), Actor) >= use.Recharge)
                    use.Ready = true;
        }

        // the highest level it can pay for right now; 0 when only cantrips are left
        public int HighestAffordable => Resource?.Highest ?? 0;

        public bool CanCast(Spell spell, int castAt)
        {
            if (spell == null || !Knows(spell.Id)) return false;

            if (castAt < spell.Level || castAt > 9) return false;

            // a borrowed shape casts nothing, cantrips included: SRD 5.2.1's Wild Shape
            if (Actor.IsShifted) return false;

            // Gaseous Form: a misty cloud casts nothing
            if (Actor.Boons.NoCasting) return false;

            // a statblock's recharge or daily use
            if (UseOf(spell.Id) is SpellUse use && !use.CanUse) return false;

            // a cantrip is at-will and costs nothing, ever
            if (spell.IsCantrip) return true;

            if (castAt == spell.Level && FreeLeft(spell.Id) > 0) return true;

            return Resource != null && Resource.CanPay(castAt);
        }

        public IEnumerable<Spell> Castable =>
            _known.Where(s => CanCast(s, s.Level));

        // pay for a cast at this level. Cantrips are at-will in BOTH modes and never reach a
        // resource, which is why this is asked about a spell rather than about a level
        public bool Pay(Spell spell, int castAt)
        {
            if (spell == null) return false;

            SpellUse use = UseOf(spell.Id);

            if (use != null && !use.CanUse) return false;

            // a free cast is spent first: it is the one that would otherwise go to waste
            if (!spell.IsCantrip && castAt == spell.Level && FreeLeft(spell.Id) > 0)
            {
                var f = _free[spell.Id];
                _free[spell.Id] = (f.PerDay, f.Left - 1);
                use?.Spend();
                return true;
            }

            bool paid = spell.IsCantrip || Resource != null && Resource.Pay(castAt);

            if (paid) use?.Spend();

            return paid;
        }

        public void Rested(Rest rest)
        {
            Resource?.Restore(rest);

            if (rest == Rest.Long)
            {
                foreach (SpellUse use in _uses.Values) use.Refill();

                foreach (string id in _free.Keys.ToList()) _free[id] = (_free[id].PerDay, _free[id].PerDay);
            }
        }

        public override string ToString() =>
            $"{Actor.Id} casts with {Ability.Id()}, dc {SaveDc}, " +
            (Resource?.Describe() ?? "no resource") + $", {_known.Count} spells";
    }


    // one statblock limit on one spell or action: a recharge on a d6, or so many a day
    public sealed class SpellUse
    {
        public SpellUse(int recharge = 0, int perDay = 0)
        {
            Recharge = Math.Clamp(recharge, 0, 6);
            PerDay = Math.Max(0, perDay);
            Left = PerDay;
            Ready = true;
        }

        // 5 is "Recharge 5-6": spent, it comes back on a d6 of 5 or 6 at the start of a turn
        public int Recharge { get; }

        // 0 is not limited by the day
        public int PerDay { get; }

        public int Left { get; private set; }

        public bool Ready { get; set; }

        public bool CanUse => Ready && (PerDay == 0 || Left > 0);

        public void Spend()
        {
            if (Recharge > 0) Ready = false;
            if (PerDay > 0) Left--;
        }

        public void Refill()
        {
            Left = PerDay;
            Ready = true;
        }
    }

    // a statblock's spells cost nothing but their limits: at will, or so many a day (SpellUse)
    public sealed class AtWill : ISpellResource
    {
        public SpellResourceMode Mode => SpellResourceMode.Slots;

        public bool CanPay(int castLevel) => SpellLevels.IsLeveled(castLevel);

        public bool Pay(int castLevel) => CanPay(castLevel);

        public void Restore(Rest rest)
        {
        }

        public int Highest => SpellLevels.Highest;

        public string Describe() => "at will";
    }
}
