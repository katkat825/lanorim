using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Core.Words;

namespace Core.Magic
{
    public sealed class Spell
    {
        public Spell(string id, int level, School school, IReadOnlyList<SpellEffect> effects,
                     int range = 0, bool concentration = false, bool ritual = false,
                     bool approximated = false, IReadOnlyList<string> classes = null,
                     Spend castingTime = Spend.Action, Trigger? trigger = null,
                     Spend? repeat = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Math.Clamp(level, 0, 9);
            School = school;
            Effects = effects ?? Array.Empty<SpellEffect>();
            Range = Math.Max(0, range);
            Concentration = concentration;
            Ritual = ritual;
            Approximated = approximated;
            Classes = classes ?? Array.Empty<string>();
            CastingTime = castingTime;
            Trigger = trigger;
            Repeat = repeat;
        }

        public string Id { get; }

        // 0 is a cantrip, and a cantrip is at-will
        public int Level { get; }

        public School School { get; }

        // squares. 0 is touch or self
        public int Range { get; }

        public bool Concentration { get; }

        public bool Ritual { get; }

        // one of the eight that ship as a bounded approximation (v1_spell_list.md)
        public bool Approximated { get; }

        // what casting it costs out of the turn: an action, a bonus action or a reaction. SRD's
        // other casting times - a minute, an hour - are rituals and travel, which the campaign
        // narrates rather than the fight counting
        public Spend CastingTime { get; }

        // for a reaction spell, the moment it answers. null for everything cast on a turn
        public Trigger? Trigger { get; }

        public bool IsReaction => CastingTime == Spend.Reaction;

        // cast only in answer to a moment the fight offers - a reaction, or a smite's bonus action
        public bool Answers => Trigger.HasValue;

        // the modes the caster picks between at the cast, if any: Blindness/Deafness
        public IReadOnlyList<string> Modes =>
            Effects.Select(e => e.Mode).Where(m => m.Length > 0).Concat(Shapes).Distinct().ToList();

        // SRD calls it a curse: Remove Curse ends it, whatever its level. Hex, Bestow Curse
        public bool Curse { get; init; }

        // a cantrip whose range doubles at 5, 11 and 17: Spare the Dying's 15 feet
        public bool RangeScales { get; init; }

        // how long a spell that repeats without concentration stays repeatable: Produce Flame's
        // flame. Instant for everything else. 'duration' in the data, an effect's word one level
        // up, where it was 'lasts' (cc_task_dedupe-leftovers.md #13)
        public Duration Duration { get; init; } = Duration.Instant;

        // a weapon attack the spell makes: True Strike. it needs a weapon chosen at the cast
        public bool Strikes => Effects.Any(e => e.Kind == Primitive.Strike);

        // shapes a wall may be put down in, picked at the cast like a mode: "line" or "ring"
        public IReadOnlyList<string> Shapes { get; init; } = Array.Empty<string>();

        // not an SRD 5.2.1 spell at all - on the v1 list, but ships under an original name like an
        // approximation does (decisions_checklist.md section 1, 2026-09-24): Murmur of Dread,
        // Wyrmbreath Boon
        public bool NotInSrd { get; init; }

        // what must not be shown under an SRD spell's name
        public bool Renamed => Approximated || NotInSrd;

        // --- 2026-09-25, the SRD check -------------------------------------------------------------

        // a reaction spell that also answers being targeted by this spell: Shield's Magic Missile
        public string AnswersSpell { get; init; } = "";

        // a casting time of a minute or more - Identify, Raise Dead, Foresight. v1 casts these out
        // of a fight only, where the minute passes in the telling
        public bool OutOfCombat { get; init; }

        // cast at this level or higher it needs no concentration: Major Image at 4+. 0 is never
        public int ConcentrationBelow { get; init; }

        // casting it again ends the one already cast: Foresight
        public bool EndsPrevious { get; init; }

        // its repeat moves it to a new creature, and only once the one it is on has dropped to 0
        // hit points: Hex, Hunter's Mark
        public bool MovesWhenDown { get; init; }

        // a creation of magical force, which Disintegrate destroys: Wall of Force, Forcecage
        public bool ForceCreation { get; init; }

        // the save DC is 8 + proficiency + this ability's modifier, whoever casts it: a species'
        // own attack (the Dragonborn's Breath Weapon uses Constitution)
        public Ability? DcAbility { get; init; }

        public int RangeAt(int casterLevel) =>
            RangeScales && IsCantrip ? Range << SpellEffect.CantripTiers(casterLevel) : Range;

        // what a later turn spends to do the repeating part again, for as long as the caster
        // holds the spell. null for a spell that happens once
        public Spend? Repeat { get; }

        public IReadOnlyList<SpellEffect> Effects { get; }

        // which class lists it appears on
        public IReadOnlyList<string> Classes { get; }

        public bool IsCantrip => Level == 0;

        // a spell a higher slot does more with. read off the effects' upcast, so a spell that grows
        // only its zone (Fog Cloud) or its globe (Globe of Invulnerability) is offered higher too -
        // the card used to look only at dice and targets and never offered those two (2026-09-25)
        public bool Upcastable => !IsCantrip && Effects.Any(e => !e.Upcast.IsNothing);

        public int MaxRadius => Effects.Count == 0 ? 0 : Effects.Max(e => e.Radius);

        // what the CAST needs aiming at - an effect done only on a later action (a breath, a hurl)
        // is aimed then, not now
        public bool NeedsATargetSquare =>
            Effects.Any(e => e.Lands != Lands.OnRepeat && e.AimKind.NeedsATargetSquare());

        // a line, a cone or a cube: it has to be pointed, at a square or in a direction
        public bool NeedsADirection => Effects.Any(e => e.Lands != Lands.OnRepeat && e.AimKind.IsDirected());

        public bool Attacks => Effects.Any(e => e.AttackRoll);

        public Ability? SavedAgainst => Effects.FirstOrDefault(e => e.Save.HasValue)?.Save;

        public bool Does(Primitive primitive) => Effects.Any(e => e.Kind == primitive);

        public string NameKey => KeyConventions.SpellName(Id);

        public string DescriptionKey => KeyConventions.SpellDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }


        public override string ToString() =>
            $"{Id} (level {Level} {EnumWords.Name(School)}" +
            (Concentration ? ", concentration" : "") + $"): " +
            string.Join(" + ", Effects);
    }
}
