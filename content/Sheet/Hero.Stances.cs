using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Core.Characters;
using Core.Resolution;
using Core.Rules;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- stances and recovery -------------------------------------------------------------

        readonly Dictionary<string, int> _spent = new Dictionary<string, int>();

        public int UsesLeft(Feature feature)
        {
            if (feature == null) return int.MaxValue;

            Feature pool = PoolOf(feature);
            int uses = pool.UsesAt(Level);

            return uses <= 0
                ? int.MaxValue
                : uses - (_spent.TryGetValue(pool.Id, out int used) ? used : 0);
        }

        // Sacred Weapon and Preserve Life spend Channel Divinity's uses, not their own
        public Feature PoolOf(Feature feature) =>
            feature == null || feature.Spends.Length == 0
                ? feature
                : FeatureCalled(feature.Spends) ?? feature;

        void SpendUse(Feature feature)
        {
            Feature pool = PoolOf(feature);

            if (pool.UsesAt(Level) > 0)
                _spent[pool.Id] = (_spent.TryGetValue(pool.Id, out int used) ? used : 0) + 1;
        }

        // uses spent since the last rest, by feature id. internal because only a save reads the
        // ledger whole - everybody else asks UsesLeft about one feature
        internal IReadOnlyDictionary<string, int> Spent => _spent;

        // a save putting the ledger back. never more than the feature has, because the uses are
        // the class's and a retuned class may give fewer
        internal void Respend(Feature feature, int used)
        {
            if (feature == null || feature.UsesAt(Level) <= 0) return;

            int clamped = Math.Clamp(used, 0, feature.UsesAt(Level));

            if (clamped == 0) _spent.Remove(feature.Id);
            else _spent[feature.Id] = clamped;
        }

        public bool Invoke(Feature feature, IResolver resolver = null)
        {
            if (feature == null || UsesLeft(feature) <= 0) return false;

            switch (feature.Trait)
            {
                case Trait.Stance:
                    Boon boon = feature.BoonFor(Level, Actor);

                    if (boon == null) return false;

                    Actor.Boons.Add(boon);
                    break;

                case Trait.Recovery:
                    {
                        if (resolver == null) return false;

                        // Preserve Life: only a Bloodied creature, and never past half its maximum
                        if (feature.OnlyBloodied && !Actor.Health.IsBloodied) return false;

                        int amount = Math.Max(0, resolver.Roll(feature.AmountAt(Level)));

                        if (feature.CapHalf)
                            amount = Math.Min(amount, Math.Max(0, Actor.Health.Maximum / 2 - Actor.Health.Current));

                        Actor.Mend(amount);
                        break;
                    }

                // the Orc's Adrenaline Rush: temporary hit points equal to the proficiency bonus;
                // the Dash is the turn's (CombatSession)
                case Trait.Boost:
                    Actor.Health.GrantTemporary(Actor.ProficiencyBonus);
                    break;

                default:
                    return false;
            }

            SpendUse(feature);

            return true;
        }

        public bool EndStance(Feature feature)
        {
            if (feature == null) return false;

            return Actor.Boons.EndFrom(feature.Id) > 0;
        }

        // the Orc's Relentless Endurance and the Barbarian's Relentless Rage: at 0 hit points,
        // spend a use and stay up on one instead of taking the death save
        public Feature DeathIntercept =>
            Features.FirstOrDefault(f => f.Trait == Trait.DeathIntercept && UsesLeft(f) > 0 &&
                                         f.WhileStances.All(s => Actor.Boons.Has(s)));

        // SRD 5.2.1 Relentless Rage (p.30): while raging, a DC 10 Constitution save - 5 more each
        // time until a rest - and on a success the hit points are twice the Barbarian's level
        public bool Intercept(IResolver resolver = null)
        {
            Feature intercept = DeathIntercept;

            if (intercept == null || !Actor.IsDown || Actor.IsDead) return false;

            int used = _spent.TryGetValue(intercept.Id, out int u) ? u : 0;

            if (intercept.Dc > 0)
            {
                if (resolver == null) return false;

                int dc = intercept.Dc + intercept.DcStep * used;

                _spent[intercept.Id] = used + 1;

                if (Checks.Save(resolver, Actor, intercept.Ability ?? Ability.Constitution, dc).Failed)
                    return false;
            }
            else
            {
                _spent[intercept.Id] = used + 1;
            }

            int back = intercept.StaysUpAt?.At(Level) ?? 1;

            Actor.StaysUp(back);

            return true;
        }
    }
}
