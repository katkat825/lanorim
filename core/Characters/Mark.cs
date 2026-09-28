using Core.Dice;

namespace Core.Characters
{
    // extra damage whenever the boon's owner hits the bearer with an attack roll: Hex's 1d6
    // necrotic, Hunter's Mark's 1d6 force. it rides on the target rather than on the caster
    // because it is about *this* creature
    public sealed record Mark(DiceRoll Dice, DamageType Type);
}
