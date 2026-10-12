using Content.Items;
using Content.SoloPlay;
using Core.Dice;

namespace Content.Sheet
{
    public sealed partial class Hero
    {
        // --- the keepsake (cc_task_h 3.2; Kathleen: "all characters get a token as part of their starting equipment and
        // that token gives one heal per long rest that can be used regardless of condition, even if they're
        // unconscious at 0hp") ---------------------------------------------------------------------------------
        //
        // It works by itself: THE MOMENT THE HERO DROPS TO 0 HIT POINTS, once per long rest, they stay up instead, at
        // their Hit Die's full value plus their level (a level 1 Mage 7, a level 1 Fighter 11, a level 20 Fighter 30).
        // No roll, no action, whatever condition they're under. It happens INSTEAD OF FALLING, so there's no
        // Unconscious, no Prone and no death save that time (Actor.StaysUp); the next drop before a long rest is the
        // death save as ever. Damage beyond 0 is ignored, as it always is. The class's own intercepts (Relentless
        // Rage, Relentless Endurance) go first, and the keepsake is what's left when they don't hold.
        //
        // The item is a quest item ("keepsake", content/srd/items/gear.json): no pack slot, no sale, no discard. Every
        // hero is given one (Build, and HeroSaves.Restore for a save from before it). Its story and look are
        // Kathleen's; the name and words are a working "Keepsake". Behind SoloRules.Keepsake

        public const string KeepsakeId = Content.Sheet.Keepsake.Id;

        // carried, and the switch on
        public bool HasKeepsake => SoloRules.Keepsake && Pack.Has(KeepsakeId);

        // used since the last long rest. its own books (Keepsake.cs); a long rest brings it back, and a save keeps it in the
        // same ledger as a feature's uses (Spent)
        public bool KeepsakeSpent => _keepsake.Spent;

        public bool KeepsakeReady => HasKeepsake && !KeepsakeSpent;

        // what it heals: the Hit Die's full value plus the level
        public int KeepsakeHeals => Class.HitDie.Sides() + Level;

        // every hero carries one: a new hero's kit, and an old save's hero when it loads
        public void GiveKeepsake(ItemShelf shelf)
        {
            Item keepsake = shelf?.Find(KeepsakeId);

            if (keepsake != null && !Pack.Has(KeepsakeId)) Pack.Take(keepsake);
        }

        // a save putting the ledger back
        internal void RespendKeepsake() => _keepsake.Spend();

        // dropped to 0 just now (HoldUp): Keepsake.Catch, with whether it's carried and what it heals
    }
}
