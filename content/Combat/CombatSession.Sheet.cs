using System.Collections.Generic;
using System.Linq;
using Core.Combat;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- swapping a weapon and letting a spell go (cc_task_cs Part 1) --------------------------------
        //
        // Options like any other, so the HUD's More actions shows them and the character sheet presses the very same
        // option (SwapOption, EndConcentrationOption): one path each. A swap costs an action in a fight
        // (character_sheet_decisions.md, weapons) and goes through Hero.Wear, the pack's own Equip; ending
        // concentration is free (SRD 5.2.1) and goes through the Incantation's Release, which takes down whatever the
        // spell held up

        // ANY CHANGE TO WHAT'S WORN OR HELD COSTS AN ACTION IN A FIGHT, from any source (Kathleen, 2026-11-03): the
        // sheet's Swap, the HUD's, and the pack's Equip, Off hand and Unequip all come through ChangeGear. Why it can't
        // happen now, or null when it can
        public string WhyNotChangeGear =>
            !MyTurn ? Why("not_on_your_turn")
            : !Hero.Actor.CanAct ? Why("cannot_act")
            : !Turn.Can(Spend.Action) ? Why("no_action")
            : null;

        // the change made and its action spent, or nothing when it can't be made now or the change itself refuses
        public bool ChangeGear(System.Func<bool> change)
        {
            if (WhyNotChangeGear != null || change == null || !change()) return false;

            Turn.Take(Spend.Action);
            return true;
        }

        IEnumerable<ActionOption> SwapOptions()
        {
            string why = WhyNotChangeGear;

            return Hero.SpareWeapons.Select(item => new ActionOption
            {
                Id = "swap:" + item.Id,
                Kind = OptionKind.Swap,
                NameKey = item.NameKey,
                Cost = Spend.Action,
                Item = item,
                Enabled = why == null,
                WhyNotKey = why,
            }).ToList();
        }

        IEnumerable<ActionOption> LetGoOptions()
        {
            if (Hero.Actor.IsConcentrating) yield return Free("end_concentration", OptionKind.EndConcentration);
        }

        // "Swap to {0}": the More actions menu names the weapon after it
        public static readonly string SwapKey = UiName("swap");

        public static readonly string EndConcentrationKey = UiName("end_concentration");

        // the option the sheet's Swap presses for this weapon, or null off the hero's turn or with no such weapon
        public ActionOption SwapOption(string itemId) =>
            MyTurn ? SwapOptions().FirstOrDefault(o => o.Item.Id == itemId) : null;

        // the option the sheet's End concentration presses, or null off the hero's turn or holding nothing
        public ActionOption EndConcentrationOption => MyTurn ? LetGoOptions().FirstOrDefault() : null;

        ActionResult DoSwap(ActionOption option) =>
            Done(Hero.SpareWeapons.Any(i => i.Id == option.Item?.Id) && ChangeGear(() => Hero.Wear(option.Item)));

        ActionResult DoLetGo()
        {
            if (!Hero.Actor.IsConcentrating) return ActionResult.No(Why("no_target"));

            Battle.Magic.Release(Hero.Actor);
            return Done(true);
        }
    }
}
