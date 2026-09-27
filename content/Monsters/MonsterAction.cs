using System;
using Core.Magic;

namespace Content.Monsters
{
    // one special action on a statblock: what it does (a spell of primitives), and how often
    public sealed class MonsterAction
    {
        public MonsterAction(Spell spell, int recharge = 0, int perDay = 0, int dc = 0)
        {
            Spell = spell ?? throw new ArgumentNullException(nameof(spell));
            Recharge = Math.Clamp(recharge, 0, 6);
            PerDay = Math.Max(0, perDay);
            Dc = Math.Max(0, dc);
        }

        // its own printed DC; 0 is the statblock's spellcasting DC
        public int Dc { get; }

        public Spell Spell { get; }

        // 5 is "Recharge 5-6"
        public int Recharge { get; }

        public int PerDay { get; }

        public override string ToString() =>
            Spell.Id + (Recharge > 0 ? $" (recharge {Recharge}-6)" : "") +
            (PerDay > 0 ? $" ({PerDay}/day)" : "");
    }
}
