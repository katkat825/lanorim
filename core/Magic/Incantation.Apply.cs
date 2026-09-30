using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Core.Magic
{
    // ONE EFFECT ON ONE CREATURE: the checks every primitive shares - can it be touched, is a Globe
    // in the way, the attack roll or the saving throw - and then the primitive's own handler
    // (Primitives/, registered in PrimitiveHandlers). the handler owns what the primitive does
    public sealed partial class Incantation
    {
        Landing Apply(Caster caster, Spell spell, SpellEffect effect, Aim aim, Actor target,
                      int castAt, Encounter fight, Moment answering,
                      Dictionary<Actor, Attempt> saves)
        {
            if (!Touches(effect, target)) return new Landing(effect, target, false);

            // SRD 5.2.1 Globe of Invulnerability: a spell cast from outside the barrier cannot
            // affect anything inside it
            if (fight != null && Shielded(fight, caster.Actor, target, castAt))
                return new Landing(effect, target, false);

            DiceRoll amount = effect.AmountAt(spell.Level, castAt, caster.Actor.Level);

            // Cure Wounds' "+ your spellcasting ability modifier" - once, not per die
            int modifier = effect.AddsModifier ? caster.Actor.AbilityModifier(caster.Ability) : 0;

            // an attack roll, a saving throw, or neither - and never both
            Attempt attempt = null;
            bool landed = true;

            if (effect.AttackRoll)
            {
                bool close = fight == null || fight.Field.Distance(caster.Actor, target) <= 1 ||
                             effect.NearZone > 0;

                // a ranged spell attack beside an enemy that can see you is at disadvantage, the same
                // as a bow (SRD 5.2.1 Ranged Attacks in Close Combat)
                bool ranged = spell.Range > 1 && effect.NearZone == 0;

                Advantage lean = Strike.Lean(caster.Actor, target, close,
                                             fight?.Sees(caster.Actor, target),
                                             fight?.Sees(target, caster.Actor),
                                             ranged && fight != null && fight.Crowded(caster.Actor)
                                                 ? Advantage.Disadvantage
                                                 : Advantage.Flat,
                                             fight?.FearInSight(caster.Actor));

                int cover = fight?.Cover(caster.Actor, target) ?? 0;

                attempt = _resolver.Resolve(RollKind.Attack, caster.AttackModifier,
                                            target.ArmorClass + cover, lean, caster.Actor);

                caster.Actor.Boons.Attacked();
                target.Boons.AttackedAt();
                Unveil(caster.Actor, fight);

                attempt = Strike.Closing(attempt, target, close);

                // a spell attack is an attack roll, and SRD's Shield answers any attack roll that
                // hits - so the same window a sword gets, and the same re-reading afterwards
                if (attempt.Succeeded && fight != null)
                {
                    fight.Offer(Moment.Hit(caster.Actor, target, attempt), target);
                    attempt = attempt.Rejudged(target.ArmorClass + cover);
                }

                attempt = Strike.Decoyed(_resolver, caster.Actor, target, attempt);

                fight?.Observer.Judged(caster.Actor, target, attempt);

                landed = attempt.Succeeded;
            }
            else if (effect.Save.HasValue &&
                     !(effect.SaveIfUnwilling && target.Side == caster.Actor.Side))
            {
                // Charm Person: "with Advantage if you or your allies are fighting it"
                Advantage extra = effect.AdvantageIfFought && fight != null &&
                                  target.Side != caster.Actor.Side
                    ? Advantage.Advantage
                    : Advantage.Flat;

                // Flesh to Stone's Construct: a save it makes without rolling
                bool automatic = effect.TagRules.Give(target, TagOutcome.AutoSave);

                // Blight's Plant: a save it fails without rolling
                bool fails = effect.TagRules.Give(target, TagOutcome.AutoFail);

                // Shatter's Construct: the save at disadvantage
                if (effect.TagRules.Give(target, TagOutcome.SaveDisadvantage))
                    extra = Advantages.Of(extra == Advantage.Advantage, true);

                // Fey Ancestry, Brave, Dwarven Resilience: advantage on a save against the condition
                if (effect.Kind == Primitive.Afflict && target.AdvantageOnSaveAgainst(effect.Condition))
                    extra = extra.And(Advantage.Advantage);

                // SRD cover adds to Dexterity saves against what comes from the far side of it
                int cover = effect.Save == Ability.Dexterity && fight != null
                    ? fight.Cover(caster.Actor, target)
                    : 0;

                attempt = effect.SameSave && saves.TryGetValue(target, out Attempt earlier)
                    ? earlier
                    : automatic
                        ? new Attempt(RollKind.Save, D20Roll.Fixed(20, 0), int.MinValue)
                        : fails
                            ? new Attempt(RollKind.Save, D20Roll.Fixed(1, 0), int.MaxValue)
                            : Checks.Save(_resolver, target, effect.Save.Value, DcFor(caster, spell) - cover,
                                          extra);

                // a save rolled now is said now; one shared with an earlier effect was said then, and
                // one made or failed without a roll has no numbers to say
                if (!automatic && !fails && !ReferenceEquals(attempt, saves.GetValueOrDefault(target)))
                    fight?.Observer.Judged(caster.Actor, target, attempt);

                saves[target] = attempt;

                // what lands on a success rather than a failure lands the other way round
                landed = effect.OnSave == OnSave.OnSuccess ? attempt.Succeeded : attempt.Failed;

                // Sleet Storm: the failed save costs the creature its concentration
                if (landed && effect.BreaksConcentration) Release(target);
            }

            var contact = new Contact(this, _resolver, caster, spell, effect, aim, target, castAt,
                                      fight, answering, amount, modifier, attempt, landed,
                                      DcFor(caster, spell));

            return PrimitiveHandlers.For(effect.Kind).Apply(contact);
        }

        // a sway that only helps its bearer: no penalty, no mark, nothing leaning against it
        static bool Kindly(SpellEffect effect) =>
            effect.Boon.Flat >= 0 && effect.Boon.Mark == null &&
            (effect.Boon.Leans & (Leans.DisadvantageOnAttacks | Leans.AdvantageAgainst |
                                  Leans.DisadvantageOnChecks | Leans.DisadvantageOnSaves)) == 0;

        // the DC: the caster's, or 8 + proficiency + the ability the spell names (a Dragonborn's
        // breath is Constitution's, SRD 5.2.1 p.84)
        static int DcFor(Caster caster, Spell spell) =>
            spell?.DcAbility is Ability own
                ? 8 + caster.Actor.ProficiencyBonus + caster.Actor.AbilityModifier(own)
                : caster.SaveDc;
    }
}
