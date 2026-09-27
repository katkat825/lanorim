using Core.Characters;
using Core.Combat;

namespace Core.Tests
{
    // a Shield with no spell behind it: when hit, +5 armor class until the start of your next
    // turn. core has to be able to test its own window without the content layer's spell files
    sealed class Brace : IReaction
    {
        public int Answered { get; private set; }

        public string Id => "brace";

        public Trigger Trigger => Trigger.Hit;

        public int Deflects => 5;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment) =>
            ReferenceEquals(moment.Target, reactor);

        public void Answer(Encounter fight, Actor reactor, Moment moment)
        {
            Answered++;
            reactor.Boons.Add(new Boon("brace", "brace", Duration.NextTurn, armorClass: 5));
        }
    }
}
