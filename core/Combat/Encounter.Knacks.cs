using System.Linq;
using Core.Characters;
using Core.Resolution;
using Core.Rules;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): a statblock's named traits, each at the one moment the SRD gives it
    // (Knack.cs; cc_task_open-questions-answers.md 2.3). Magic Resistance is on the save itself (Actor.AgainstSpells)
    public sealed partial class Encounter
    {
        // the setting tag a campaign gives a fight in daylight, for Sunlight Sensitivity (v1 has no light)
        public const string Sunlight = "sunlight";

        // what an attack roll leans on from the attacker's traits and where everyone stands
        Advantage KnackLean(Actor attacker, Actor target, Attack attack)
        {
            bool advantage =
                // Pack Tactics: an ally of the attacker within 5 feet of the target, not Incapacitated
                attacker.Has(Knack.PackTactics) && Field.Where(target) is Space.Cell at &&
                Field.Adjacent(at).Any(a => !ReferenceEquals(a, attacker) && a.Side == attacker.Side &&
                                                !a.IsDown && !a.IsIncapacitated) ||
                // Bloodied Fury: a melee attack while Bloodied
                attacker.Has(Knack.BloodiedFury) && attacker.Health.IsBloodied && attack != null && !attack.IsRanged;

            // Sunlight Sensitivity: in a fight the campaign set in sunlight
            bool disadvantage = attacker.Has(Knack.SunlightSensitivity) && Setting.Contains(Sunlight);

            return Advantages.Of(advantage, disadvantage);
        }

        // Undead Fortitude: dropped to 0 by damage that isn't Radiant and isn't a Critical Hit, a Constitution save
        // against 5 plus the damage; on a success it drops to 1 instead, and nothing hears it fell
        void HoldsOn(Actor target, int dealt, DamageType type, bool critical)
        {
            if (!target.Has(Knack.UndeadFortitude) || target.IsDead || type == DamageType.Radiant || critical) return;

            if (Checks.Save(_resolver, target, Ability.Constitution, 5 + dealt).Succeeded) target.StaysUp(1);
        }
    }
}
