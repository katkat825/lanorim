using System;
using Core.Characters;

namespace Core.Combat
{
    public enum Spend
    {
        // costs nothing: dropping a weapon, speaking, ending concentration
        Free,

        Action,

        Bonus,

        Reaction,

        // the whole turn: one of the few SRD things that is neither an action nor free
        Movement,
    }

    // 2 actions + 1 bonus + 1 reaction is the intentional solo delta - character_sheet_decisions.md.
    // the base two are compensation for playing one hero instead of a party, NOT a stand-in for
    // Extra Attack: a class feature that grants an action stacks on top of them
    // (decisions_checklist.md section 1, corrected 2026-09-23). an extra action a class grants may
    // be for the first round only, for every round, or a fixed number per rest, so the grants are
    // kept apart from the baseline and asked separately.
    //
    // the solo delta is the HERO'S. a monster plays its SRD statblock's turn (decisions_checklist.md
    // section 1, "Monsters don't get the solo extra action", 2026-09-25): one action, a bonus action
    // only if the statblock has one, one reaction - see Statblock.
    public sealed class ActionBudget
    {
        public const int BaseActions = 2;
        public const int BaseBonusActions = 1;
        public const int BaseReactions = 1;

        // a statblock's turn: one action, whatever it is spent on
        public const int StatblockActions = 1;

        // a monster's budget. its Attack action is one attack, or its Multiattack's whole list
        public static ActionBudget Statblock(Multiattack multiattack = null, bool bonusAction = false) =>
            new ActionBudget
            {
                _actions = StatblockActions,
                _bonusActions = bonusAction ? 1 : 0,
                Multiattack = multiattack != null && multiattack.Count > 1 ? multiattack : null,
            };

        int _actions = BaseActions;
        int _bonusActions = BaseBonusActions;

        // SRD Multiattack: what the one Attack action makes. null for a hero, and for a monster
        // that makes one attack
        public Multiattack Multiattack { get; private set; }

        // THE GUARDRAIL, and the only one. nothing forbids a feature granting an action - Extra
        // Attack is exactly that - but no turn holds more than this many, however the grants stack.
        // four is a level 20 fighter: the base two, Extra Attack, and an Action Surge on top.
        public const int MostActionsInATurn = 4;

        // granted for every round of every combat
        public int ExtraActionsEachRound { get; set; }
        public int ExtraBonusActionsEachRound { get; set; }
        public int ExtraReactionsEachRound { get; set; }

        // granted on the first round of a combat only
        public int ExtraActionsFirstRound { get; set; }
        public int ExtraReactionsFirstRound { get; set; }

        // a pool that refills on a long rest, spent a round at a time
        public int ExtraActionsPerLongRest { get; set; }

        public int ExtraActionsLeft { get; private set; }

        public void LongRest() => ExtraActionsLeft = ExtraActionsPerLongRest;

        // one of the per-rest extras, moved into this round's allowance
        public bool DrawExtraAction()
        {
            if (ExtraActionsLeft <= 0) return false;

            ExtraActionsLeft--;
            return true;
        }

        public int ActionsFor(int round) =>
            Math.Min(MostActionsInATurn,
                     _actions + ExtraActionsEachRound + (round == 1 ? ExtraActionsFirstRound : 0));

        public int BonusActionsFor(int round) =>
            _bonusActions + ExtraBonusActionsEachRound;

        public int ReactionsFor(int round) =>
            BaseReactions + ExtraReactionsEachRound + (round == 1 ? ExtraReactionsFirstRound : 0);

        public override string ToString() =>
            $"{ActionsFor(2)} actions, {BonusActionsFor(2)} bonus, {ReactionsFor(2)} reactions" +
            (ExtraActionsPerLongRest > 0 ? $", {ExtraActionsLeft} extra left today" : "");
    }

    // one actor's turn, and what it has left of it. a reaction is not spent on your own turn, so
    // it survives EndTurn and is reset by the round, not by the turn - see Encounter.
    public sealed class Turn
    {
        public Turn(Actor actor, ActionBudget budget, int round)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Budget = budget ?? new ActionBudget();
            Round = round;

            Actions = Budget.ActionsFor(round);
            _granted = Actions;
            BonusActions = Budget.BonusActionsFor(round);
            Movement = actor.Moves;

            // Haste: one more action, of a narrow kind
            Limited = actor.Boons.LimitedAction ? 1 : 0;

            // getting up costs half your speed and you keep the rest - SRD. the cost is taken when
            // the actor actually stands, not here.
            Started = true;
        }

        public Actor Actor { get; }

        public ActionBudget Budget { get; }

        public int Round { get; }

        public int Actions { get; private set; }

        public int BonusActions { get; private set; }

        // feet, converted to squares by whoever is drawing; 5 feet is one square
        public int Movement { get; private set; }

        public bool Started { get; }

        public bool Ended { get; private set; }

        // a Disengage was taken this turn: walking away provokes nothing
        public bool Disengaged { get; private set; }

        public const int FeetPerSquare = 5;

        public int SquaresLeft => Movement / FeetPerSquare;

        public bool Can(Spend spend, int amount = 1) => spend switch
        {
            Spend.Free => true,
            Spend.Action => Actor.CanAct && !Actor.Boons.NoActions && Actions >= amount &&
                            !(Actor.Boons.ActionOrBonus && (_tookAction || _tookBonus)),
            Spend.Bonus => Actor.CanAct && !Actor.Boons.NoActions && BonusActions >= amount &&
                           !(Actor.Boons.ActionOrBonus && (_tookAction || _tookBonus)),
            Spend.Reaction => Actor.CanAct,
            Spend.Movement => !Actor.IsRooted && Movement >= amount,
            _ => false,
        };

        bool _tookAction;
        bool _tookBonus;

        public bool Take(Spend spend, int amount = 1)
        {
            if (!Can(spend, amount)) return false;

            switch (spend)
            {
                // an action spent on anything else ends a Multiattack that was under way
                case Spend.Action: Actions -= amount; _tookAction = true; _volley = null; break;
                case Spend.Bonus: BonusActions -= amount; _tookBonus = true; break;
                case Spend.Movement: Movement -= amount; break;
            }

            return true;
        }

        // Haste's extra action: one weapon attack, a Dash, a Disengage or a Hide, and nothing else
        public int Limited { get; private set; }

        public bool TakeLimited()
        {
            if (Limited <= 0 || !Actor.CanAct || Actor.Boons.NoActions) return false;

            Limited--;
            return true;
        }

        // an attack paid for with an action. a Slowed creature attacks once whatever it has left;
        // a Hasted one may spend its narrow action on it when the ordinary ones are gone. a monster
        // with Multiattack pays one action for the first attack and makes the rest of the list free
        public bool TakeAttack(Spend spend, Attack attack = null)
        {
            if (spend == Spend.Action && Actor.Boons.ActionOrBonus && _attacked) return false;

            // the rest of a Multiattack already paid for
            if (spend == Spend.Action && _volley != null && Actor.CanAct && !Actor.Boons.NoActions &&
                _volley.Take(attack?.Id))
                return true;

            bool ordinary = Take(spend);
            bool paid = ordinary || spend == Spend.Action && TakeLimited();

            if (!paid) return false;

            _attacked = true;

            // a Slowed creature makes one attack, and Haste's narrow action buys one weapon attack,
            // Multiattack or not. an attack the Multiattack isn't made of is an Attack action of one
            if (ordinary && spend == Spend.Action && Budget.Multiattack != null &&
                !Actor.Boons.ActionOrBonus)
            {
                Volley volley = Budget.Multiattack.Begin();

                if (volley.Take(attack?.Id)) _volley = volley;
            }

            return true;
        }

        bool _attacked;

        Volley _volley;

        // the attacks left of a Multiattack under way
        public int AttacksLeft => _volley?.Left ?? 0;

        // whether it can attack now: the rest of a Multiattack, or an action to start one
        public bool CanAttack =>
            AttacksLeft > 0 && Actor.CanAct && !Actor.Boons.NoActions ||
            Can(Spend.Action);

        // whether this attack can be the next: any attack starts an action, but the rest of a
        // Multiattack is the attacks it lists
        public bool Allows(Attack attack) =>
            AttacksLeft == 0 || attack == null || _volley.Allows(attack.Id) || Can(Spend.Action);

        // SRD: standing up costs half your speed, rounded the way the table always rounds it -
        // down to a whole square
        public bool StandUp()
        {
            if (!Actor.Has(Condition.Prone) || Actor.IsPinned(Condition.Prone)) return false;

            // SRD 5.2.1 Prone: "You can't right yourself ... if your Speed is 0"
            if (Actor.IsRooted || Actor.Moves == 0) return false;

            int cost = Actor.Speed / 2 / FeetPerSquare * FeetPerSquare;

            if (Movement < cost) return false;

            Movement -= cost;
            Actor.Remove(Condition.Prone);
            return true;
        }

        // SRD Dash: movement equal to your speed again, on top of what is left
        public void Hasten() => Movement += Actor.Moves;

        // SRD: when your speed changes during your move, subtract the distance you have already
        // moved from the new speed - which is the change, added to what is left
        public void SpeedChanged(int before, int after)
        {
            if (before == after) return;

            Movement = Math.Max(0, Movement + after - before);
        }

        public void Disengage() => Disengaged = true;

        // Action Surge: one of the per-rest extras, spent now, as one more action this turn. it
        // still answers to the guardrail - a surge on a turn that is already at the cap is not
        // spent, rather than spent for nothing
        public bool Surge()
        {
            if (Ended || !Actor.CanAct) return false;

            // the whole turn's actions, spent or not, so the guardrail counts the turn and not
            // just what is left of it
            if (_granted >= ActionBudget.MostActionsInATurn) return false;

            if (!Budget.DrawExtraAction()) return false;

            Actions++;
            _granted++;
            return true;
        }

        int _granted;

        public void End() => Ended = true;

        public override string ToString() =>
            $"{Actor.Id} round {Round}: {Actions} actions, {BonusActions} bonus, " +
            $"{SquaresLeft} squares";
    }
}
