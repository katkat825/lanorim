using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Sheet;
using Core.Combat;
using Core.Localization;

namespace Content.Combat
{
    // THE COMBAT COMMAND API - the layer the combat UX calls (docs/combat_ux.md). The screen asks
    // what the hero can do, the player picks one, the screen asks who or where it may go and what
    // it would do there, and the player confirms or cancels. Nothing here draws, and nothing here
    // waits on a click: the questions it cannot answer itself (a reaction set to "ask", a die on
    // the felt) go out through the asker and the dice source, which the table implements.
    //
    // ASSUMPTIONS, written down (they are also in the run log):
    // - one hero, driven by the player; every other actor has a brain. a Friendly actor with no
    //   brain simply ends its turn
    // - the hero's rolls are the physical dice when the table says so (TableResolver); the GM's
    //   are behind the screen
    // - a preview is the rules' own arithmetic on the numbers as they stand now; it cannot see a
    //   reaction the target has not spent yet (a Shield), and says so by being a chance, not a
    //   promise
    // - Q and E rotate a line or cone a quarter turn; the square the mouse is over also points it
    //   (Template.Toward), and the confirmed facing is whichever was last set
    public sealed partial class CombatSession
    {
        readonly ItemShelf _shelf;
        readonly FormShelf _forms;

        public CombatSession(Battle battle, ItemShelf shelf = null, FormShelf forms = null)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _shelf = shelf;
            _forms = forms;
        }

        public Battle Battle { get; }

        public Encounter Fight => Battle.Fight;

        public Hero Hero => Battle.Hero;

        public Turn Turn { get; private set; }

        public SessionPhase Phase { get; private set; } = SessionPhase.Waiting;

        public Outcome Outcome => Fight.Outcome;

        public static string Why(string reason) =>
            KeyConventions.Key(KeyConventions.UiNs, "combat_why", reason, "name");

        public static IEnumerable<string> Keys() =>
            new[] { "no_action", "no_bonus", "no_reaction", "cannot_act", "out_of_range", "not_seen",
                    "no_target", "spent", "not_on_your_turn", "rooted", "too_far", "pinned",
                    "nothing_to_break", "nobody_to_shake", "no_uses", "charmed", "not_an_edge",
                    "shifted", "no_surge", "choose_mode", "choose_type", "cannot_cast", "too_long",
                    "seen" }
                .Select(Why);

        // --- the flow -----------------------------------------------------------------------------

        // plays everyone before the hero, and stops on the hero's turn (or the end)
        public void Start() => PlayUntilTheHero();

        public ActionResult EndTurn()
        {
            if (Phase == SessionPhase.Over || Turn == null) return ActionResult.No(Why("not_on_your_turn"));

            Selected = null;
            Fight.EndTurn();
            PlayUntilTheHero();

            return new ActionResult { Done = true };
        }

        void PlayUntilTheHero()
        {
            Turn = null;
            Phase = SessionPhase.Waiting;

            while (true)
            {
                Turn next = Fight.Next();

                if (next == null || Fight.Over)
                {
                    Phase = SessionPhase.Over;
                    return;
                }

                if (ReferenceEquals(next.Actor, Hero.Actor))
                {
                    Turn = next;
                    Phase = SessionPhase.Choosing;
                    return;
                }

                ITactics brain = Battle.BrainOf(next.Actor);

                if (brain != null) brain.Take(Fight, next);

                if (!next.Ended && !Fight.Over) Fight.EndTurn();

                // concentration the monster's turn broke (a hero knocked out, a caster downed)
                Battle.Magic.Check(Fight.Actors);
            }
        }

        bool MyTurn => Phase != SessionPhase.Over && Turn != null && !Turn.Ended &&
                       ReferenceEquals(Turn.Actor, Hero.Actor);
    }
}
