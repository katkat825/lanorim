using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Combat
{
    // what drives an actor the player is not driving. one method: take your turn.
    public interface ITactics
    {
        void Take(Encounter fight, Turn turn);
    }

    // what a monster knows how to do, as data on its statblock. the tags are the "basic approach +
    // attack with tags" of v1_build_checklist.md section 6 - not a behaviour tree, just enough for
    // a goblin to be a goblin and a brute to be a brute.
    [Flags]
    public enum Instinct
    {
        None = 0,

        // goes for whoever is easiest to finish rather than whoever is nearest
        Finisher = 1 << 0,

        // prefers a target it can reach without stepping out of anyone's reach
        Cautious = 1 << 1,

        // won't close to melee while it has a ranged attack and room to use it
        Skirmisher = 1 << 2,

        // stands its ground: never moves away from a target it can already reach
        Stubborn = 1 << 3,

        // bloodied, it runs: a Dash away from its enemies and nothing else (a bandit, a goblin)
        Craven = 1 << 4,
    }

    // approach the best target and hit it until the actions run out. deliberately plain: enemy AI
    // is "standard" per decisions_checklist.md section 3, and a predictable enemy is a readable one.
    // it spends whatever budget the turn has: a monster's is one action, and its Multiattack when
    // the statblock has one (ActionBudget.Statblock), so the same loop that gives a goblin one
    // swing gives a brown bear its bite and its claw.
    public sealed class BasicTactics : ITactics
    {
        public BasicTactics(IReadOnlyList<Attack> attacks, Instinct instinct = Instinct.None)
        {
            Attacks = attacks ?? Array.Empty<Attack>();
            Instinct = instinct;
        }

        public IReadOnlyList<Attack> Attacks { get; }

        public Instinct Instinct { get; }

        bool Is(Instinct flag) => (Instinct & flag) == flag;

        public void Take(Encounter fight, Turn turn)
        {
            if (fight == null || turn == null || turn.Ended) return;

            Actor me = turn.Actor;

            if (!me.CanAct)
            {
                fight.EndTurn();
                return;
            }

            // off the board (Banishment, Maze): nothing here to walk to or swing at
            if (!fight.Field.Where(me).HasValue)
            {
                fight.EndTurn();
                return;
            }

            if (me.Has(Condition.Prone)) fight.StandUp(turn);

            // a creature with nothing in hand and nothing natural to fight with goes back for its
            // weapon first
            if (me.Disarmed && !Attacks.Any(a => a.Hand == Hand.None) && me.DroppedAt.HasValue)
            {
                fight.Walk(turn, me.DroppedAt.Value);

                if (me.Disarmed)
                {
                    fight.EndTurn();
                    return;
                }
            }

            // one target for the whole turn: switching mid-turn looks like indecision and makes
            // the fight harder to read
            Actor quarry = Choose(fight, me);

            if (quarry == null)
            {
                fight.EndTurn();
                return;
            }

            // attack first if something already reaches - a monster that walks away from a target
            // it could hit is the most common way basic AI reads as broken
            Swing(fight, turn, quarry);

            if (turn.Ended || !me.CanAct) return;

            OpenTheWay(fight, turn, quarry);

            if (!Reaches(fight, me, quarry)) Approach(fight, turn, quarry);

            // walked up to a door: through it, with what movement is left
            if (OpenTheWay(fight, turn, quarry) && !Reaches(fight, me, quarry)) Approach(fight, turn, quarry);

            Swing(fight, turn, quarry);

            NextInReach(fight, turn, quarry);

            fight.EndTurn();
        }

        Actor Choose(Encounter fight, Actor me)
        {
            // a creature charmed by someone does not go for them (SRD 5.2.1 Charmed)
            List<Actor> candidates = fight.Field.Enemies(me)
                                          .Where(a => !a.IsDown)
                                          .Where(a => !me.HasFrom(Condition.Charmed, a))
                                          .ToList();

            if (candidates.Count == 0) return null;

            IOrderedEnumerable<Actor> ranked = Is(Instinct.Finisher)
                ? candidates.OrderBy(a => a.Health.Current)
                            .ThenBy(a => fight.Field.Distance(me, a))
                : candidates.OrderBy(a => fight.Field.Distance(me, a))
                            .ThenBy(a => a.Health.Current);

            // the turn order last, so the same fight from the same seed picks the same target every time and a
            // name never decides it (it was the id until 2026-10-03)
            return ranked.ThenBy(fight.InitiativeRank).FirstOrDefault();
        }

        bool Reaches(Encounter fight, Actor me, Actor target) =>
            Best(fight, me, target) != null;

        // WHICH ATTACK, AS A RULE (cc_task_open-questions-answers.md 2.7): of the attacks it can use from where it
        // stands, one it can make without Disadvantage first, then the biggest average damage, then the
        // statblock's own order (OrderBy is a stable sort, so the statblock has the last word). Until 2026-10-03 a
        // tie went to the attack's id, alphabetically: a goblin's scimitar and shortbow both average 5, the boss's
        // "goblin_shortbow" sorted before "scimitar", so beside the hero it shot point-blank at Disadvantage - and
        // renaming the id made the rogue lose the goblin camp 33 times in the sample campaign's test
        Attack Best(Encounter fight, Actor me, Actor target, Turn turn = null) =>
            Attacks.Where(a => me.CanUse(a))
                   .Where(a => turn == null || turn.Allows(a))
                   .Where(a => fight.Field.InRange(me, target, a.Reaches))
                   .Where(a => !Is(Instinct.Skirmisher) || !a.IsRanged ||
                               fight.Field.Distance(me, target) > 1)
                   .OrderBy(a => fight.Band(me, target, a) == Core.Resolution.Advantage.Disadvantage)
                   .ThenByDescending(a => a.DamageFor(me).Average)
                   .FirstOrDefault();

        // SWITCHING TARGETS MID-MULTIATTACK (cc_task_open-questions-answers.md 2.6): SRD 5.2.1 lets each attack of a
        // Multiattack pick its own target, so when the quarry drops partway through, the attacks left go at the next
        // creature it would choose that is already in reach. It doesn't walk for them: one target for the walk
        void NextInReach(Encounter fight, Turn turn, Actor quarry)
        {
            Actor me = turn.Actor;

            while (quarry.IsDown && turn.CanAttack && !fight.Over && me.CanAct &&
                   Choose(fight, me) is Actor next && Reaches(fight, me, next))
            {
                quarry = next;
                Swing(fight, turn, quarry);
            }
        }

        void Swing(Encounter fight, Turn turn, Actor target)
        {
            while (turn.CanAttack && !target.IsDown && !fight.Over)
            {
                Attack attack = Best(fight, turn.Actor, target, turn);

                if (attack == null) return;

                if (fight.Hit(turn, target, attack) == null) return;
            }
        }

        // MONSTERS OPEN DOORS TOO (cc_task_open-questions-answers.md 2.1): a shut door beside it, with its quarry
        // nearer the far side, it opens with the turn's free object interaction (Encounter.Doors.cs) and walks on.
        // Only the free one: it never spends an action on a door it could spend on its quarry
        static bool OpenTheWay(Encounter fight, Turn turn, Actor quarry)
        {
            Actor me = turn.Actor;

            if (!turn.CanInteract || !(fight.Field.Where(me) is Cell here) || !(fight.Field.Where(quarry) is Cell theirs))
                return false;

            foreach (Border door in fight.DoorsBeside(me))
            {
                Cell far = door.Vertical
                    ? new Cell(door.Cell.X == here.X ? here.X - 1 : here.X + 1, here.Y)
                    : new Cell(here.X, door.Cell.Y == here.Y ? here.Y - 1 : here.Y + 1);

                if (Battlefield.Distance(far, theirs) >= Battlefield.Distance(here, theirs)) continue;

                return fight.OpenDoor(turn, door);
            }

            return false;
        }

        void Approach(Encounter fight, Turn turn, Actor target)
        {
            Actor me = turn.Actor;

            if (Is(Instinct.Stubborn) && Reaches(fight, me, target)) return;

            Cell? standing = fight.Field.Where(me);
            Cell? theirs = fight.Field.Where(target);

            if (!standing.HasValue || !theirs.HasValue) return;

            // the squares it could stand in this turn, nearest-to-the-target first. a skirmisher
            // wants to be exactly at its range band, not in the target's face
            int wanted = Wanted();

            IReadOnlyDictionary<Cell, int> reachable =
                fight.Field.Reachable(me, turn.SquaresLeft);

            Cell? best = reachable
                .Where(pair => fight.Field.CanSee(pair.Key, theirs.Value))
                .OrderBy(pair => Math.Abs(Battlefield.Distance(pair.Key, theirs.Value) - wanted))
                .ThenBy(pair => pair.Value)
                .ThenBy(pair => pair.Key.Y)
                .ThenBy(pair => pair.Key.X)
                .Select(pair => (Cell?)pair.Key)
                .FirstOrDefault();

            // nowhere better to be, or nothing in sight: shuffle toward it on the raw route so a
            // monster behind a wall still comes round rather than standing still forever
            if (!best.HasValue)
                best = reachable
                    .OrderBy(pair => Battlefield.Distance(pair.Key, theirs.Value))
                    .ThenBy(pair => pair.Value)
                    .Select(pair => (Cell?)pair.Key)
                    .FirstOrDefault();

            if (!best.HasValue) return;

            int now = Battlefield.Distance(standing.Value, theirs.Value);

            if (Math.Abs(Battlefield.Distance(best.Value, theirs.Value) - wanted) >=
                Math.Abs(now - wanted)) return;

            fight.Walk(turn, best.Value);
        }

        int Wanted()
        {
            if (!Is(Instinct.Skirmisher)) return 1;

            Attack ranged = Attacks.Where(a => a.IsRanged)
                                   .OrderByDescending(a => a.Range)
                                   .FirstOrDefault();

            // just inside the normal band, so it isn't shooting at disadvantage
            return ranged == null ? 1 : Math.Max(2, ranged.Range - 1);
        }

        public override string ToString() =>
            $"{Attacks.Count} attacks, {(Instinct == Instinct.None ? "plain" : Instinct.ToString())}";
    }
}
