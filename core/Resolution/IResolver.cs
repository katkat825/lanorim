using Core.Dice;

namespace Core.Resolution
{
    // the one place a d20 leaves the hand. everything else asks this; swapping in a scripted or a
    // logging resolver is how a fight is replayed in a test without a mock of the whole engine.
    public interface IResolver
    {
        Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage = Advantage.Flat);

        // the same, knowing whose roll it is: the hero's d20s go in the physical tray, the GM's
        // behind the screen. a resolver that does not care ignores the roller
        Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage,
                        Core.Characters.Actor roller) =>
            Resolve(kind, modifier, against, advantage);

        int Roll(DiceRoll dice, Core.Characters.Actor roller) => Roll(dice);

        int Roll(DiceRoll dice, Core.Characters.Actor roller,
                 out System.Collections.Generic.IReadOnlyList<int> faces) =>
            Roll(dice, out faces);

        // damage, healing and hit dice - anything that is not a d20 against a number
        int Roll(DiceRoll dice);

        // the same, with the faces that came up: Chromatic Orb leaps on two matching dice. a
        // resolver that cannot say what the faces were says none, and nothing leaps
        int Roll(DiceRoll dice, out System.Collections.Generic.IReadOnlyList<int> faces)
        {
            faces = System.Array.Empty<int>();
            return Roll(dice);
        }
    }
}
