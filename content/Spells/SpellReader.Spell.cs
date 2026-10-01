using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    public static partial class SpellReader
    {
        static Spell ReadOne(JsonElement entry, string id, List<string> problems)
        {
            int level = entry.Number("level", -1);

            if (level < 0 || level > 9)
            {
                problems.Add($"{id}: level {level} - a spell is level 0 (a cantrip) to 9");
                return null;
            }

            if (!EnumWords.TryParse(entry.Text("school", "evocation"), out School school))
                problems.Add($"{id}: '{entry.Text("school")}' is not a school of magic");

            var effects = new List<SpellEffect>();

            foreach (JsonElement raw in entry.Items("effects"))
            {
                SpellEffect effect = ReadEffect(raw, id, level, problems);

                if (effect != null) effects.Add(effect);
            }

            if (effects.Count == 0)
                problems.Add($"{id}: no effects - a spell that does nothing is a reference card, " +
                             "and reference cards are not loaded here");

            bool concentration = entry.Flag("concentration");

            (Spend time, Trigger? trigger, Spend? repeat, Duration lasts) = ReadTiming(entry, id, problems);

            CheckWhole(entry, id, effects, concentration, trigger, repeat, lasts, problems);

            if (!EnumWords.TryParse(entry.Text("solo", "usable"), out Solo solo))
                problems.Add($"{id}: '{entry.Text("solo")}' is not a solo word (usable, unavailable)");

            return new Spell(id, level, school, effects,
                             entry.Number("range"),
                             concentration,
                             entry.Flag("ritual"),
                             entry.Flag("approximated"),
                             entry.Strings("classes"),
                             time,
                             trigger,
                             repeat)
            {
                Curse = entry.Flag("curse"),
                RangeScales = entry.Flag("range_scales"),
                Duration = lasts,
                Shapes = entry.Strings("shapes"),
                NotInSrd = entry.Flag("not_in_srd"),
                AnswersSpell = entry.Text("answers_spell") ?? "",
                OutOfCombat = entry.Flag("out_of_combat"),
                ConcentrationBelow = entry.Number("concentration_below"),
                EndsPrevious = entry.Flag("ends_previous"),
                MovesWhenDown = entry.Flag("moves_when_down"),
                ForceCreation = entry.Flag("force_creation"),
                DcAbility = entry.Ability("dc_ability", problems, id),
                Solo = solo,
            };
        }

        // when it is cast and how long it lasts: its casting time, the moment a reaction answers, the
        // part of a turn a held spell repeats with, and its duration
        static (Spend Time, Trigger? Trigger, Spend? Repeat, Duration Lasts) ReadTiming(JsonElement entry, string id,
                                                                                   List<string> problems)
        {
            if (!EnumWords.TryParse(entry.Text("casting_time", "action"), out Spend time) ||
                time != Spend.Action && time != Spend.Bonus && time != Spend.Reaction)
                problems.Add($"{id}: '{entry.Text("casting_time")}' is not a casting time " +
                             "(action, bonus_action, reaction)");

            Trigger? trigger = null;
            string triggerId = entry.Text("trigger");

            if (!string.IsNullOrEmpty(triggerId))
            {
                if (EnumWords.TryParse(triggerId, out Trigger parsed)) trigger = parsed;
                else
                    problems.Add($"{id}: '{triggerId}' is not a trigger - it is " +
                                 string.Join(", ", Triggers.All.Select(t => t.Id())));
            }

            // a reaction spell is cast in answer to exactly one kind of moment, and a spell cast on
            // a turn answers nothing - either half without the other is a spell the fight can
            // never offer, or offers for no reason
            if (time == Spend.Reaction && !trigger.HasValue)
                problems.Add($"{id}: a reaction spell has to say what it answers with 'trigger'");

            // SRD 5.2.1's smites are the one thing answered with a bonus action: right after
            // your own hit. every other trigger is a reaction's
            if (trigger == Trigger.Struck && time != Spend.Bonus)
                problems.Add($"{id}: 'struck' is answered with a bonus action - 'casting_time': " +
                             "'bonus_action'");

            if (trigger.HasValue && trigger != Trigger.Struck && time != Spend.Reaction)
                problems.Add($"{id}: a 'trigger' on a spell that is not cast as a reaction");

            Spend? repeat = null;
            string repeatId = entry.Text("repeat");

            if (!string.IsNullOrEmpty(repeatId))
            {
                if (EnumWords.TryParse(repeatId, out Spend again) &&
                    (again == Spend.Action || again == Spend.Bonus))
                    repeat = again;
                else
                    problems.Add($"{id}: 'repeat' is 'action' or 'bonus_action', not '{repeatId}'");
            }

            if (!EnumWords.TryParse(entry.Text("duration", "instant"), out Duration lasts))
                problems.Add($"{id}: '{entry.Text("duration")}' is not a duration");

            return (time, trigger, repeat, lasts);
        }

        // what the spell's effects have to agree on between them, and with the spell
        static void CheckWhole(JsonElement entry, string id, List<SpellEffect> effects, bool concentration,
                               Trigger? trigger, Spend? repeat, Duration lasts, List<string> problems)
        {
            // a repeat is done while the spell is held, or while it lasts (Produce Flame), so it
            // needs something to hold and something to repeat - either half alone is a spell
            // that says it does more than it can
            if (repeat.HasValue && !concentration && lasts == Duration.Instant)
                problems.Add($"{id}: a spell that repeats has to be held - mark it concentration, " +
                             "or say how long it 'lasts'");

            if (repeat.HasValue != effects.Any(e => e.Lands.Repeats()))
                problems.Add($"{id}: 'repeat' on the spell and an effect that 'lands' on the repeat " +
                             "come together");

            if (effects.Count > 0 && effects[0].Follows)
                problems.Add($"{id}: the first effect has nothing before it to follow");

            // an effect that acts through the zone needs a zone to act through, and a zone with
            // nothing acting through it is only a marker - which is allowed, it is what a zone was
            bool hasZone = effects.Any(e => e.Handler.MakesAZone);

            if (effects.Any(e => e.AimKind == AimKind.Zone && !e.Handler.MovesTheZone(e)) && !hasZone)
                problems.Add($"{id}: an effect reaches 'zone' but the spell makes no zone");

            // one zone to a spell - or to each of its modes: Forcecage is a cage or a box
            if (effects.Where(e => e.Handler.MakesAZone).GroupBy(e => e.Mode).Any(g => g.Count() > 1))
                problems.Add($"{id}: one zone to a spell, or to each of its modes");

            // stopping a spell needs a spell to stop, and only the cast window has one in hand
            if (effects.Any(e => e.Kind == Primitive.Counter) && trigger != Trigger.Cast)
                problems.Add($"{id}: a counter effect only works on a reaction to a cast " +
                             "('casting_time': 'reaction', 'trigger': 'cast')");

            // the two have to agree or the sheet's "concentrating" light lies: a spell marked for
            // concentration must hold something, and something held must be marked
            foreach (SpellEffect effect in effects)
                if (effect.Duration == Duration.Concentration && !concentration)
                    problems.Add($"{id}: an effect lasts for concentration but the spell is not " +
                                 "marked 'concentration: true'");

            // modes: every effect that names one belongs to it, and a spell with modes has at
            // least two - a single mode is not a choice
            List<string> modes = effects.Select(e => e.Mode).Where(m => m.Length > 0)
                                        .Distinct().ToList();

            if (modes.Count == 1)
                problems.Add($"{id}: one 'mode' is not a choice - give it two or none");

            if (entry.Strings("shapes").Any(s => s != "line" && s != "ring"))
                problems.Add($"{id}: a wall's 'shapes' are 'line' and 'ring'");

            if (effects.Any(e => e.Lands == Lands.OnEnd) && !concentration &&
                effects.All(e => e.Duration == Duration.Instant))
                problems.Add($"{id}: an effect that lands 'on_end' on a spell that never lasts");
        }
    }
}
