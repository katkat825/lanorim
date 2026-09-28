using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Core.Magic
{
    // every lasting thing a spell put on a creature, and at what level it was cast. Dispel Magic's
    // rule is about levels - a 3rd-level dispel ends anything of 3rd level or lower and has to beat
    // 10 + the level for anything higher - so the level has to be kept. and everything that happens
    // to it on the creature's turns and when it is hurt: expiry, a burning, a delayed splash, a
    // waking, a worsening.
    //
    // HOW IT ENDS is the effect's LingerSpec, whole; this adds only who, when, and how the saves
    // have gone so far. it used to copy ~20 of the effect's settings one by one
    // (cc_task_dedupe-effects.md, Phase 4)
    internal sealed class Placement
    {
        public Actor Target;
        public Caster Caster;
        public string Spell;
        public int Level;
        public Condition Condition;

        // the way out, the save track, what damage does to it
        public LingerSpec Spec = LingerSpec.Nothing;

        // the caster's DC, for an escape and a save
        public int Dc;

        // the round it landed: Banishment's minute and Flesh to Stone's count from here
        public int Since;

        // how long, and whose turn counts it
        public Duration Duration;
        public Actor Owner;
        public bool Armed;

        // how the repeat saves have gone
        public int Fails;
        public int Successes;

        // damage at the start of each of the target's turns, and damage once at the end of its
        // next one
        public DiceRoll Burns;
        public DiceRoll Later;
        public DamageType DamageType;

        // a creature it killed rises as this statblock at the start of the caster's next turn
        public string RaisesAs = "";

        // the fight it was put on in, when there was one
        public Encounter Fight;

        // a turn decided for it (Command)
        public Command Command;

        public bool Timed => Duration == Characters.Duration.NextTurn ||
                             Duration == Characters.Duration.NextTurnEnd ||
                             Duration == Characters.Duration.TurnEnd;

        // it saves again at the end of each of its turns - unless it burns, when the save comes
        // right after the burn at the start of the turn (Searing Smite)
        public bool SavesAtTurnEnd => Spec.RepeatSave != null && Burns.IsNothing;

        public bool NeedsWatching =>
            Timed || Spec.RepeatSave != null || Command != Command.None || Spec.Gone != null ||
            Spec.EndsOnAct || Spec.WhileInZone || Spec.PermanentAfterRounds > 0 || RaisesAs.Length > 0 ||
            Spec.Flees || Spec.OnDamage != OnDamage.Nothing || !Burns.IsNothing || !Later.IsNothing;
    }
}
