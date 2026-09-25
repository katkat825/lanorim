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

        // the hero's own d20, for a check or a save - the dice tray throws this
        public Attempt Attempt { get; init; }

        // behind the screen: a <<roll>>, or an encounter table's rolls
        public GmRoll Roll { get; init; }

        public TableRoll Table { get; init; }

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

        public Settled Settle(Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            switch (request.Kind)
            {
                case RequestKind.Check:
                {
                    Attempt attempt = request.Skill != Skill.None
                        ? Checks.Check(_resolver, _hero.Actor, request.Skill, request.Dc)
                        : Checks.Check(_resolver, _hero.Actor, request.Ability, request.Dc);

                    return new Settled(request, Answer.Of(attempt)) { Attempt = attempt };
                }

                case RequestKind.Save:
                {
                    Attempt attempt = Checks.Save(_resolver, _hero.Actor, request.Ability, request.Dc);

                    return new Settled(request, Answer.Of(attempt)) { Attempt = attempt };
                }

                case RequestKind.Roll:
                {
                    GmRoll roll = _screen.Roll(request.Dice);

                    return new Settled(request, new Answer { Total = roll.Total }) { Roll = roll };
                }

                case RequestKind.Encounter:
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

                case RequestKind.Gold:
                {
                    // taking more than the hero has takes what there is: a toll does not go into
                    // debt
                    if (request.Amount >= 0) _hero.Pack.Earn(request.Amount);
                    else _hero.Pack.Spend(Math.Min(_hero.Pack.Gold, -request.Amount));

                    return new Settled(request, new Answer());
                }

                case RequestKind.Give:
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

                case RequestKind.Loot:
                {
                    LootTable table = _pack?.LootTable(request.Id);

                    if (table == null)
                        return new Settled(request, new Answer())
                        {
                            Problem = $"no loot table '{request.Id}' in this campaign",
                        };

                    // the same per-class rule the merchant keeps: nothing the hero could not use
                    LootRoll roll = _screen.Open(table, _pack.Loot, _library.Items, _hero.Class.Id,
                                                 _hero.Level);

                    Haul haul = Spoils.Hand(_hero.Pack, roll, _library.Items);

                    return new Settled(request, new Answer { Gold = roll.Gold })
                    {
                        Loot = roll,
                        Haul = haul,
                    };
                }

                case RequestKind.Rest:
                {
                    if (request.Id == "long") _hero.LongRest();
                    else
                    {
                        // a story's short rest spends hit dice the way a player would: until the
                        // hero is whole or the dice run out (the rest screen can ask later)
                        Health health = _hero.Actor.Health;

                        while (health.Current < health.Maximum && health.HitDice > 0)
                            health.SpendHitDie(_resolver, _hero.Actor.AbilityModifier(Ability.Constitution));

                        _hero.ShortRest(_resolver);
                    }

                    return new Settled(request, new Answer());
                }

                case RequestKind.Level:
                {
                    // milestone levelling only goes up; a campaign that says <<level 3>> to a level
                    // 5 hero is a campaign played out of order, not a demotion
                    _hero.LevelTo(request.Amount);

                    return new Settled(request, new Answer());
                }

                default:
                    return new Settled(request, null);
            }
        }
    }
}
