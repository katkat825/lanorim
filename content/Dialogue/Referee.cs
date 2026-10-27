using System;
using Content.Campaigns;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Resolution;
using Core.Rules;
using Core.Tables;
using Core.Words;

namespace Content.Dialogue
{
    // what settling one request did, beyond the answer the story reads: the roll the tray shows,
    // the table the screen consulted, anything that would not fit in the pack
    public sealed class Settled
    {
        public Settled(Request request, Answer answer)
        {
            Request = request;
            Answer = answer;
        }

        public Request Request { get; }

        // what goes back into the conversation. null for a fight, which the referee cannot settle
        public Answer Answer { get; }

        // what a natural 1 or 20 on the check bought, already carried out on the hero: the table
        // shows its line
        public Consequences.Visit Visit { get; init; }

        // the consequence took the hero to 0 hit points away from a fight, so it came round and took
        // a long rest (Recovered): the day is over
        public bool Rested { get; init; }

        // the hero's own d20, for a check or a save - the dice tray throws this
        public Attempt Attempt { get; init; }

        // behind the screen: a <<roll>>, or an encounter table's rolls
        public GmRoll Roll { get; init; }

        public TableRoll Table { get; init; }

        // A SECRET THE PLAYER HEARS (cc_task_i Part 1): an encounter table rolled behind the screen, or a
        // <<roll ... heard>>. the screen rattles for these and nothing else the GM rolls
        public bool Heard => Roll?.Visibility == Visibility.Hidden || Table?.Table.Visibility == Visibility.Hidden;

        // a line the companion owes the story (cc_task_i 2.3): a <<bark>>'s pool, or Camp for a rest
        public Bark? Cue { get; init; }

        // what a <<loot>> turned up and where it went - the pack screen shows this, and anything in
        // its Waiting list goes through the discard flow before the story moves on
        public LootRoll Loot { get; init; }

        public Haul Haul { get; init; }

        // items from a <<give>> that did not fit. the story waits on the discard flow for these
        // (inventory_decisions.md), which is the pack screen's, not the referee's
        public Item Leftover { get; init; }

        public int LeftoverCount { get; init; }

        // why it could not be done as written: an item or a table the campaign does not have.
        // developer text, never shown; the story still goes on with the variable's default
        public string Problem { get; init; }

        public bool NeedsTheTable => Answer == null;
    }

    // SETTLES EVERYTHING A STORY CAN ASK FOR THAT DOES NOT NEED A SCENE: a check or a save by the
    // hero, a roll behind the screen, a consulted encounter table, gold, an item, a level. a
    // <<fight>> is the one thing it hands back - that is a whole scene, and the game runs it and
    // answers with the outcome. everything here goes through the rules' own front doors
    // (Checks, GmScreen, Pack, Hero.LevelTo), so a story cannot roll a check the sheet disagrees
    // with.
    public sealed class Referee
    {
        readonly Hero _hero;
        readonly IResolver _resolver;
        readonly GmScreen _screen;
        readonly Library _library;
        readonly Package _pack;

        public Referee(Hero hero, IResolver resolver, GmScreen screen, Library library,
                       Package pack = null)
        {
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _screen = screen ?? throw new ArgumentNullException(nameof(screen));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _pack = pack;
        }

        // A WORKSHOP TEST'S RULE OVER THE HERO'S OWN CHECKS AND SAVES (cc_task_q 3.3, "Always pass" / "Always fail"): the
        // attempt as rolled, judged again. null in play. The GM's rolls and a fight's never come here
        public Func<Attempt, Attempt> Judge { get; set; }

        Attempt Judged(Attempt attempt) => Judge == null ? attempt : Judge(attempt);

        public Settled Settle(Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return request.Kind switch
            {
                RequestKind.Check => Checked(request, Judged(request.Skill != Skill.None
                                                         ? Checks.Check(_resolver, _hero.Actor, request.Skill, request.Dc)
                                                         : Checks.Check(_resolver, _hero.Actor, request.Ability, request.Dc))),
                RequestKind.Save => Tested(request, Judged(Checks.Save(_resolver, _hero.Actor, request.Ability, request.Dc))),
                RequestKind.Roll => Rolled(request),
                RequestKind.Encounter => Consulted(request),
                RequestKind.Gold => Paid(request, Coins.FromGold(request.Amount)),
                RequestKind.Silver => Paid(request, Coins.FromSilver(request.Amount)),
                RequestKind.Copper => Paid(request, request.Amount),
                RequestKind.Give => Given(request),
                RequestKind.Loot => Looted(request),
                RequestKind.Rest => Rested(request),
                RequestKind.Level => Levelled(request),
                RequestKind.Xp => Earned(request),
                RequestKind.Bark => Barked(request),
                _ => new Settled(request, null),
            };
        }

        // the pool was checked as the story was read (Request.ReadBark)
        static Settled Barked(Request request) =>
            new Settled(request, new Answer()) { Cue = EnumWords.TryParse(request.Id, out Bark pool) ? pool : null };

        static Settled Tested(Request request, Attempt attempt) =>
            new Settled(request, Answer.Of(attempt)) { Attempt = attempt };

        // a check, and what its natural 1 or 20 buys - whether it passed or not (updated_decisions.md)
        Settled Checked(Request request, Attempt attempt)
        {
            Consequences.Visit visit = Consequence(attempt);

            return new Settled(request, Answer.Of(attempt)) { Attempt = attempt, Visit = visit, Rested = Recovered() };
        }

        // DOWN OUTSIDE A FIGHT IS A LONG REST (Kathleen, 2026-09-28): a consequence that takes the
        // hero to 0 hit points away from the board doesn't kill it - it comes round, and the day is
        // done. SRD 5.2.1's rest needs 1 hit point to start, so it is steadied and brought to 1 first
        bool Recovered()
        {
            Actor hero = _hero.Actor;

            if (!hero.IsDown || hero.IsDead) return false;

            hero.Stabilize();
            hero.Mend(1);
            _hero.LongRest();

            return true;
        }

        // WHAT A NATURAL 1 OR 20 ON A CHECK, OR THE HERO'S CRITICAL HIT, BUYS: drawn behind the
        // screen from the pool, carried out on the hero - its hit points, its gold, a shift until it
        // rests, a condition - and its amount thrown on the hero's own dice. the one door for both
        // the story's checks and a fight's crits (CombatSession.Consequence). null when it buys nothing
        public Consequences.Visit Consequence(Attempt attempt)
        {
            Consequence drawn = _screen.Draw(Pool, attempt);

            if (drawn == null) return null;

            Consequences.Visit visit = Consequences.Befall(drawn, _resolver, _hero.Actor, _hero.Pack);

            // a bane that dropped the hero to 0: what keeps them up has its say here too, the keepsake included
            // (cc_task_h 3.2: "out of a fight, the same trigger")
            if (_hero.Actor.IsDown && !_hero.Actor.IsDead) _hero.HoldUp(_resolver);

            return visit;
        }

        Settled Rolled(Request request)
        {
            GmRoll roll = _screen.Roll(request.Dice, request.Heard ? Visibility.Hidden : Visibility.Quiet);

            return new Settled(request, new Answer { Total = roll.Total }) { Roll = roll };
        }

        Settled Consulted(Request request)
        {
            EncounterTable table = _pack?.Encounter(request.Id);

            if (table == null)
                return new Settled(request, new Answer())
                {
                    Problem = $"no encounter table '{request.Id}' in this campaign",
                };

            TableRoll consulted = _screen.Consult(table);

            return new Settled(request, new Answer
            {
                Entry = consulted.Triggered ? consulted.Entry?.Id ?? "" : "",
                StartsAFight = consulted.Fights,
            })
            {
                Table = consulted,
            };
        }

        // <<gold N>>, <<silver N>>, <<copper N>>, each already turned into copper (Coins): the purse is copper
        Settled Paid(Request request, int copper)
        {
            // taking more than the hero has takes what there is: a toll does not go into debt
            if (copper >= 0) _hero.Pack.Earn(copper);
            else _hero.Pack.Spend(Math.Min(_hero.Pack.Copper, -copper));

            return new Settled(request, new Answer());
        }

        Settled Given(Request request)
        {
            Item item = _library.Items.Find(request.Id);

            if (item == null)
                return new Settled(request, new Answer())
                {
                    Problem = $"no item '{request.Id}' in the SRD or this campaign",
                };

            int left = _hero.Pack.Take(item, request.Amount);

            return new Settled(request, new Answer())
            {
                Leftover = left > 0 ? item : null,
                LeftoverCount = left,
            };
        }

        Settled Looted(Request request)
        {
            LootTable table = _pack?.LootTable(request.Id);

            if (table == null)
                return new Settled(request, new Answer())
                {
                    Problem = $"no loot table '{request.Id}' in this campaign",
                };

            // the same per-class rule the merchant keeps: nothing the hero could not use
            LootRoll roll = _screen.Open(table, _pack.Loot, _library.Items, _hero.Class.Id, _hero.Level);

            Haul haul = Spoils.Hand(_hero.Pack, roll, _library.Items);

            return new Settled(request, new Answer { Copper = roll.Copper })
            {
                Loot = roll,
                Haul = haul,
            };
        }

        Settled Rested(Request request)
        {
            if (request.Id == "long") _hero.LongRest();
            else
            {
                // a story's short rest spends hit dice the way a player would: until the hero is
                // whole or the dice run out (the rest screen can ask later)
                Health health = _hero.Actor.Health;

                while (health.Current < health.Maximum && health.HitDice > 0)
                    health.SpendHitDie(_resolver, _hero.Actor.AbilityModifier(Ability.Constitution));

                _hero.ShortRest(_resolver);
            }

            return new Settled(request, new Answer()) { Cue = Bark.Camp };
        }

        // milestone levelling only goes up; a campaign that says <<level 3>> to a level 5 hero is a
        // campaign played out of order, not a demotion
        Settled Levelled(Request request)
        {
            if (Leveling == Leveling.Xp)
                return new Settled(request, new Answer())
                {
                    Problem = $"<<level {request.Amount}>> is ignored: this campaign levels by XP - give <<xp n>> instead",
                };

            _hero.LevelTo(request.Amount);

            return new Settled(request, new Answer());
        }

        // XP LEVELLING (cc_task_i 3.4): the story's experience points, in a campaign that levels by them
        Settled Earned(Request request)
        {
            if (Leveling != Leveling.Xp)
                return new Settled(request, new Answer())
                {
                    Problem = $"<<xp {request.Amount}>> is ignored: this campaign levels by milestone - " +
                              "give <<level n>>, or set \"leveling\": \"xp\" in pack.json",
                };

            _hero.Earn(request.Amount);

            return new Settled(request, new Answer());
        }

        Leveling Leveling => _pack?.Manifest?.Rules.Leveling ?? Leveling.Milestone;

        // the game's consequences with the campaign's on top, or in their place (cc_task_i 4.3)
        ConsequencePool Pool => _pool ??= _pack?.PoolOn(_library.Consequences) ?? _library.Consequences;

        ConsequencePool _pool;
    }
}
