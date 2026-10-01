using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Resolution;
using Core.Rules;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- hitting things ------------------------------------------------------------------

        // every rider the hero's features hand to a blow. the fight layer asks once per attack;
        // whether Sneak Attack fires is a question about the attack, not about the Rogue.
        public IReadOnlyList<Rider> RidersFor(bool hadAdvantage, bool spent = false,
                                              Attack attack = null, Turn turn = null) =>
            Features.Where(f => Eligible(f, attack, turn))
                    .Select(f => f.RiderFor(Level, hadAdvantage, spent))
                    .Where(r => r != null)
                    .ToList();

        // SRD 5.2.1: Sneak Attack "once per turn" with "a Finesse or a Ranged weapon" (p.61);
        // Frenzy and Brutal Strike only while their stances are up (p.29-30); Radiant Strikes on a
        // Melee weapon (p.55)
        bool Eligible(Feature feature, Attack attack, Turn turn)
        {
            if (feature.Trait != Trait.Rider) return true;

            if (feature.WhileStances.Any(s => !Actor.Boons.Has(s))) return false;

            switch (feature.NeedsWeapon)
            {
                case "finesse_or_ranged":
                    if (attack == null || !(attack.Finesse || attack.IsRanged)) return false;
                    break;

                case "melee":
                    if (attack == null || attack.IsRanged) return false;
                    break;
            }

            return !(feature.OncePerTurn && turn != null && ReferenceEquals(_turn, turn) &&
                     _firedThisTurn.Contains(feature.Id));
        }

        // the once-a-turn riders already spent, and on which turn
        Turn _turn;
        readonly HashSet<string> _firedThisTurn = new(StringComparer.Ordinal);

        void Fired(Turn turn, IEnumerable<Rider> riders)
        {
            if (turn == null) return;

            if (!ReferenceEquals(_turn, turn))
            {
                _turn = turn;
                _firedThisTurn.Clear();
            }

            foreach (Rider rider in riders ?? Enumerable.Empty<Rider>())
                if (Features.Any(f => f.Id == rider.Id && f.OncePerTurn)) _firedThisTurn.Add(rider.Id);
        }

        public Blow Hit(Encounter fight, Turn turn, Actor target, Attack attack,
                        bool spendResource = false, Spend spend = Spend.Action)
        {
            if (fight == null || turn == null || attack == null) return null;

            // a bear does not hold a sword
            if (Form != null && !Form.Owns(attack)) return null;

            bool close = fight.Field.Distance(Actor, target) <= 1;

            // Brutal Strike: the first Strength attack of a turn, while reckless, gives up Reckless
            // Attack's advantage for the extra die - unless the roll would then be at disadvantage
            Feature brutal = Features.FirstOrDefault(f => f.Forgoes.Length > 0 && Eligible(f, attack, turn) &&
                                                          attack.AbilityFor(Actor) == Ability.Strength);

            if (brutal != null)
            {
                Actor.ForgoingAdvantageFrom = brutal.Forgoes;

                if (Strike.Lean(Actor, target, close, fight.Sees(Actor, target), fight.Sees(target, Actor),
                                fearInSight: fight.FearInSight(Actor), attack: attack) == Advantage.Disadvantage)
                {
                    Actor.ForgoingAdvantageFrom = null;
                    brutal = null;
                }
            }

            // Sneak Attack's setup: whether the attack has advantage is worked out before the
            // roll, so the rider can be handed to it rather than patched on afterwards
            bool advantage = Strike.Lean(Actor, target, close,
                                         fight.Sees(Actor, target), fight.Sees(target, Actor),
                                         fearInSight: fight.FearInSight(Actor), attack: attack) ==
                             Advantage.Advantage;

            IReadOnlyList<Rider> riders = RidersFor(advantage, spendResource, attack, turn)
                .Where(r => brutal != null || !Features.Any(f => f.Id == r.Id && f.Forgoes.Length > 0))
                .ToList();

            Blow blow;

            try
            {
                blow = fight.Hit(turn, target, attack, spend, Advantage.Flat, riders);
            }
            finally
            {
                Actor.ForgoingAdvantageFrom = null;
            }

            if (blow != null && blow.Hit) Fired(turn, blow.Riders);

            // the Light property's bonus attack follows an attack with a Light weapon (p.89)
            if (blow != null && attack.Light && spend == Spend.Action)
            {
                LightTurn = turn;
                LightHand = attack.Hand;
            }

            return blow;
        }

        // the turn a Light weapon was attacked with, and in which hand: the Light weapon in the other hand may then
        // attack with the bonus action, without the ability modifier on its damage. by hand, not by id: a dagger in
        // each hand is two weapons with one id (it was the id until 2026-10-03, when nothing could be held off-hand)
        public Turn LightTurn { get; private set; }

        public Hand LightHand { get; private set; } = Hand.None;

        public Attack LightBonusAttack(Attack attack) =>
            attack.With(addsAbility: false,
                        damageBonus: attack.DamageBonus + Math.Min(0, Actor.AbilityModifier(attack.AbilityFor(Actor))));


        // everything this hero can do with a reaction, handed to a fight that is about to start:
        // an opportunity attack with the weapon in hand, and every reaction spell on the sheet.
        // the chooser is the sensible default - opportunity attacks always, a Shield only when it
        // turns the hit - and the UI replaces it with its own when the player wants to be asked
        public void ReadyFor(Encounter fight, Incantation incantation = null)
        {
            if (fight == null) return;

            Attack swing = Attacks.FirstOrDefault(a => !a.IsRanged) ?? Attacks.FirstOrDefault();

            if (swing != null) fight.ArmOpportunity(Actor, swing);

            if (Caster != null && incantation != null)
                foreach (Spell spell in Caster.Known.Where(s => s.Answers))
                    fight.Arm(Actor, new SpellReaction(Caster, spell, incantation));

            // Uncanny Dodge: halve a hit you see coming
            foreach (Feature halve in Features.Where(f => f.Reaction == "halve"))
                fight.Arm(Actor, new HalveReaction(halve.Id));

            fight.ChooseReactionsWith(Actor, ReactionChoosers.WhenItHelps);
        }
    }
}
