using System;
using System.Collections.Generic;
using System.Linq;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Space;

namespace Content.Combat
{
    public sealed partial class CombatSession
    {
        // --- picking one ----------------------------------------------------------------------------

        public ActionOption Selected { get; private set; }

        public string Mode { get; private set; }

        public DamageType DamageType { get; private set; }

        public int CastAt { get; private set; }

        public Facing Facing { get; private set; } = Facing.East;

        public Side Side { get; private set; } = Side.Left;

        public bool Select(ActionOption option, string mode = null,
                           DamageType damageType = DamageType.None, int castAt = -1)
        {
            if (!MyTurn || option == null || !option.Enabled) return false;

            // a thing with nothing to aim at happens straight away
            if (option.Targeting == Targeting.None && option.Kind != OptionKind.Spell &&
                option.Kind != OptionKind.Again)
            {
                Selected = option;
                return true;
            }

            Selected = option;
            Mode = mode ?? option.Modes.FirstOrDefault();
            DamageType = damageType != DamageType.None
                ? damageType
                : option.DamageChoices.FirstOrDefault();
            CastAt = option.Spell == null ? 0 : Math.Max(option.Spell.Level, castAt);
            Phase = SessionPhase.Targeting;

            return true;
        }

        public void Cancel()
        {
            Selected = null;
            Mode = null;
            DamageType = DamageType.None;

            if (MyTurn) Phase = SessionPhase.Choosing;
        }

        // Q and E, or the scroll wheel
        public Facing Rotate(int quarters)
        {
            int f = ((int)Facing + quarters) % 4;
            Facing = (Facing)(f < 0 ? f + 4 : f);
            return Facing;
        }

        public void Point(Facing facing) => Facing = facing;

        public void Pick(Side side) => Side = side;

        // who the selected option may go at
        public IReadOnlyList<Actor> LegalTargets()
        {
            if (Selected == null || !MyTurn) return Array.Empty<Actor>();

            Actor me = Hero.Actor;

            if (Selected.Kind == OptionKind.Attack)
                return Fight.Field.Enemies(me)
                            .Where(e => !e.IsDown && Fight.Field.InRange(me, e, Selected.Attack.Reaches) &&
                                        !me.HasFrom(Condition.Charmed, e))
                            .OrderBy(e => Fight.Field.Distance(me, e))
                            .ThenBy(e => e.Id, StringComparer.Ordinal)
                            .ToList();

            if (Selected.Spell == null) return Array.Empty<Actor>();

            int range = Math.Max(1, Selected.Spell.RangeAt(me.Level));
            bool kind = Selected.Spell.Kindly(Mode);

            // a kindly spell goes on friends (and the caster); a harmful one on foes, and never on one
            // who charmed you (the cast would refuse it, as an attack is never offered at them)
            return Fight.Actors.Where(a => kind ? a.Side == me.Side
                                                : a.Side != me.Side && !me.HasFrom(Condition.Charmed, a))
                        .Where(a => ReferenceEquals(a, me) && kind ||
                                    Fight.Field.InRange(me, a, range) && Fight.Sees(me, a))
                        .Where(a => kind || !a.IsDown)
                        .OrderBy(a => Fight.Field.Distance(me, a))
                        .ThenBy(a => a.Id, StringComparer.Ordinal)
                        .ToList();
        }

        // A PARTY OF ONE (cc_task_ui-issues-10-01.md 2.1): a creature-aimed option whose only legal target is
        // the caster lands on the caster, with no aiming step - Cure Wounds, Healing Word or Mage Armor with
        // no ally on the board. Not a spell list: any option, whenever this is true. With anyone else to
        // aim at (a summoned creature on your side), it is aimed as usual
        public bool OnlyTargetIsYou =>
            Selected?.Targeting is Targeting.Creature or Targeting.Creatures &&
            LegalTargets() is { Count: 1 } only && ReferenceEquals(only[0], Hero.Actor);

        // where a square-aimed option may be put
        public IReadOnlyList<Cell> LegalSquares()
        {
            if (Selected?.Spell == null || !MyTurn || !(Fight.Field.Where(Hero.Actor) is Cell here))
                return Array.Empty<Cell>();

            int range = Math.Max(1, Selected.Spell.RangeAt(Hero.Actor.Level));

            return Fight.Field.Map.Cells
                        .Where(c => Battlefield.Distance(here, c) <= range && Fight.Field.CanSee(here, c))
                        .ToList();
        }
    }
}
