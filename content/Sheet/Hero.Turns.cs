using System;
using System.Linq;
using Content.Classes;
using Core.Combat;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- the action economy ------------------------------------------------------------

        public ActionBudget Budget { get; private set; } = new ActionBudget();

        ActionBudget BuildBudget()
        {
            var budget = new ActionBudget();

            foreach (Feature feature in Features.Where(f => f.Trait == Trait.ActionGrant))
            {
                int how = Math.Max(1, feature.Count);

                if (feature.Uses > 0)
                {
                    budget.ExtraActionsPerLongRest += feature.Uses;
                    continue;
                }

                switch (feature.Grants)
                {
                    case Spend.Bonus:
                        budget.ExtraBonusActionsEachRound += how;
                        break;

                    case Spend.Reaction:
                        budget.ExtraReactionsEachRound += how;
                        break;

                    default:
                        budget.ExtraActionsEachRound += how;
                        break;
                }
            }

            budget.LongRest();

            return budget;
        }
    }
}
