using System;
using System.Linq;
using Content.Combat;
using Content.Sheet;

namespace Content.Screens
{
    // WHAT THE CHARACTER SHEET'S TWO BUTTONS DO (cc_task_cs 1.2): Swap and End concentration, one path each.
    //
    // In a fight each is the fight's own option, the very one the HUD's More actions shows (CombatSession.Sheet.cs),
    // and the sheet hands it to the combat director's Pick like any button on the bar: a swap costs its action there,
    // ending concentration costs nothing. Outside a fight Swap is the pack's own Equip (PackView), free, and there is
    // no concentration to end (Hero.FightOver lets it go). Opening the sheet spends nothing either way.
    //
    // The sheet asks here whether a button is live and, when it isn't, why; it decides nothing itself
    public sealed class SheetActs
    {
        readonly PackView _pack;

        public SheetActs(Hero hero, CombatSession session = null)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            Session = session;
            _pack = new PackView(hero, null);
        }

        public Hero Hero { get; }

        public CombatSession Session { get; }

        public bool InAFight => Session != null && Session.Phase != SessionPhase.Over;

        // --- swap ------------------------------------------------------------------------------------------

        // the fight's option for this weapon: what the sheet hands the combat director. null outside a fight, off the
        // hero's turn, or for a weapon that can't be taken in hand
        public ActionOption SwapOption(string itemId) => InAFight ? Session.SwapOption(itemId) : null;

        public bool CanSwap(string itemId) =>
            InAFight
                ? SwapOption(itemId)?.Enabled == true
                : Hero.SpareWeapons.Any(i => i.Id == itemId) && _pack.CanEquip(itemId);

        // why a Swap button is grey, or null when it isn't
        public string SwapWhyNotKey(string itemId) =>
            CanSwap(itemId) ? null
            : !InAFight ? null
            : SwapOption(itemId)?.WhyNotKey ?? CombatSession.Why("not_on_your_turn");

        // outside a fight: the pack's own Equip, free. In a fight the option goes to the director instead
        public bool SwapNow(string itemId) => !InAFight && CanSwap(itemId) && _pack.Equip(itemId);

        // --- end concentration -----------------------------------------------------------------------------

        public ActionOption EndConcentrationOption => InAFight ? Session.EndConcentrationOption : null;

        public bool CanEndConcentration => EndConcentrationOption?.Enabled == true;

        public string EndConcentrationWhyNotKey =>
            CanEndConcentration || !InAFight || !Hero.Actor.IsConcentrating ? null : CombatSession.Why("not_on_your_turn");
    }
}
