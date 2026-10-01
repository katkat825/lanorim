using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // part of Encounter (Encounter.cs): doors in a fight (cc_task_open-questions-answers.md 2.1). Kathleen: "go with
    // SRD; if the SRD doesn't say, yes, it costs an action". SRD 5.2.1, Interacting with Things: "You can interact
    // with one object or feature of the environment for free, during either your move or action. For example, you
    // could open a door during your move as you stride toward a foe." And: "If you want to interact with a second
    // object, you need to take the Utilize action." So a shut door beside you opens for free once a turn, and a
    // second costs an action (Utilize is an action). The door is the only object v1's fight has. Monsters open them
    // the same way (Tactics), so a closed door is no wall to them either. An open door is a gap
    // (Battlefield.OpenDoor); it doesn't shut again in a fight.
    public sealed partial class Encounter
    {
        // the shut doors on the four sides of a creature's square
        public IReadOnlyList<Border> DoorsBeside(Actor actor)
        {
            // Gaseous Form's mist can't manipulate objects, and so can't open one
            if (!(Field.Where(actor) is Cell at) || actor.Boons.Forbids(Forbid.Objects)) return new List<Border>();

            return new[] { Border.North(at), Border.East(at), Border.South(at), Border.West(at) }
                   .Where(b => Field.Map.At(b) == Edge.Door)
                   .ToList();
        }

        // what opening one costs this turn: nothing, or an action once the free interaction is spent
        public static Spend DoorCost(Turn turn) => turn != null && turn.CanInteract ? Spend.Free : Spend.Action;

        public bool OpenDoor(Turn turn, Border door)
        {
            if (turn == null || !turn.Actor.CanAct || !DoorsBeside(turn.Actor).Contains(door)) return false;

            if (!turn.Interact() && !turn.Take(Spend.Action)) return false;

            Field.OpenDoor(door);
            Observer.DoorOpened(turn.Actor, door);

            return true;
        }
    }
}
