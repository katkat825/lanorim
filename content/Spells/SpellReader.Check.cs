using System.Collections.Generic;
using Core.Combat;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    public static partial class SpellReader
    {
        // the rules every primitive's data shares: sizes, shapes, pulses, what a common setting
        // needs. what each primitive needs is its handler's Check
        static void Check(SpellEffect effect, string spellId, int level, bool radiusSaid,
                          List<string> problems)
        {
            IPrimitiveHandler handler = effect.Handler;

            if (handler.CoversGround &&
                effect.AimKind != AimKind.Square && effect.AimKind != AimKind.Wall && effect.Radius <= 0 &&
                !radiusSaid)
                // a square zone is sized by its side; everything else by its radius. a zone of one
                // square says 'radius': 0 on purpose: Spiritual Weapon's force
                problems.Add($"{spellId}: a {effect.Kind.Id()} with no 'radius'");

            if ((effect.AimKind == AimKind.Burst || effect.AimKind == AimKind.Around) && effect.Radius <= 0)
                problems.Add($"{spellId}: a burst needs a 'radius' in squares");

            // the shapes that come out of the caster carry their own size, and none of them
            // borrows a radius: a 100-foot line is not a 100-foot burst
            if (effect.AimKind.IsDirected() && effect.Length <= 0)
                problems.Add($"{spellId}: a {effect.AimKind.Id()} needs a 'length' in squares");

            if (effect.AimKind == AimKind.Line && effect.Width <= 0)
                problems.Add($"{spellId}: a line needs a 'width' in squares - SRD lines are " +
                             "usually 1, five feet");

            if (effect.AimKind != AimKind.Line && effect.Width > 0)
                problems.Add($"{spellId}: 'width' only means something on a line - a cone's " +
                             "width is its distance from you, a cube's is its length");

            if (effect.AimKind.IsDirected() && effect.Radius > 0)
                problems.Add($"{spellId}: a {effect.AimKind.Id()} with a 'radius' - its size is " +
                             "'length'");

            if (effect.AimKind == AimKind.Square && effect.Length <= 0)
                problems.Add($"{spellId}: a square needs a 'length' in squares");

            if (effect.AimKind == AimKind.Wall && !handler.MakesAZone)
                problems.Add($"{spellId}: only a zone is put down as a wall");

            if (effect.AddsModifier && !handler.Allows("add_modifier"))
                problems.Add($"{spellId}: 'add_modifier' on a {effect.Kind.Id()} - it adds to " +
                             "damage or healing");

            // a caster's-turn duration counts somebody's turn: it has to be a turn-shaped one
            // (the only two there are). a duration only a spell has: no feature or item has a caster

            if (effect.AimKind == AimKind.Zone && effect.Pulses == Pulses.None &&
                !handler.MovesTheZone(effect) && !handler.LastsWhileInTheZone(effect))
                problems.Add($"{spellId}: an effect that reaches 'zone' has to say when, with " +
                             "'pulses'");

            if (effect.Pulses != Pulses.None && effect.AimKind != AimKind.Zone)
                problems.Add($"{spellId}: 'pulses' on an effect that is not aimed as a 'zone'");

            if (effect.CoreOnly && effect.AimKind != AimKind.Zone)
                problems.Add($"{spellId}: 'core_only' narrows what reaches the zone");

            if (effect.Affects == Affects.Foes && !handler.MakesAZone && !effect.AimKind.IsArea())
                problems.Add($"{spellId}: 'affects': 'foes' is for a zone or an area");

            if (effect.Affects == Affects.Allies && !handler.MakesAZone)
                problems.Add($"{spellId}: 'affects': 'allies' belongs on the zone");

            if (effect.Lands == Lands.OnRepeat || effect.Lands == Lands.NowAndOnRepeat)
            {
                // checked on the spell: it has a 'repeat'
            }
            else if (effect.Switches)
                problems.Add($"{spellId}: 'switches' narrows an effect that lands on the repeat");

            if ((effect.Lands == Lands.EachTurn || effect.Lands == Lands.NextTurnEnd) &&
                !handler.Allows(effect.Lands.Id()))
                problems.Add($"{spellId}: damage lands 'next_turn_end' or 'each_turn'; nothing else does");

            if (effect.Points > 1 && effect.AimKind != AimKind.Burst)
                problems.Add($"{spellId}: 'points' is how many bursts - it needs 'aim': 'burst'");

            if (effect.SameSave && !effect.Save.HasValue)
                problems.Add($"{spellId}: 'same_save' with no save to share");

            if (effect.CantripGrowth != CantripGrowth.None && level != 0)
                problems.Add($"{spellId}: 'cantrip_growth' on a level {level} spell - only " +
                             "cantrips grow with the caster's level");

            if (!effect.Upcast.IsNothing && level == 0)
                problems.Add($"{spellId}: a cantrip cannot be upcast; it grows with your level");

            if (effect.Upcast.Radius > 0 && !handler.MakesAZone)
                problems.Add($"{spellId}: an 'upcast' 'radius' grows a zone");

            if (effect.Upcast.BlocksSpellsUpTo > 0 && effect.BlocksSpellsUpTo <= 0)
                problems.Add($"{spellId}: an 'upcast' 'blocks_spells_up_to' grows a globe that blocks spells");

            if (effect.Upcast.Targets > 0 && effect.AimKind != AimKind.Creatures)
                problems.Add($"{spellId}: an 'upcast' 'targets' is more picks - 'aim': 'creatures'");
        }
    }
}
