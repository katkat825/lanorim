using Core.Dice;

namespace Core.Resolution
{
    // the one place a d20 leaves the hand. everything else asks this; swapping in a scripted or a
    // logging resolver is how a fight is replayed in a test without a mock of the whole engine.
    public interface IResolver
    {
        Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage = Advantage.Flat);

        // damage, healing and hit dice - anything that is not a d20 against a number
        int Roll(DiceRoll dice);
    }
}
