using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Content.Dialogue
{
    // WHAT A CONVERSATION CAN ASK THE TABLE FOR - the narrative flow's "branch out and return"
    // (v1_build_checklist.md section 8). a campaign writes one of these as a Yarn command; the
    // conversation stops on it; the table does the thing - rolls the check, runs the fight, hands
    // over the potion - and answers; the answer lands in a Yarn variable and the story goes on
    // down whichever branch the author wrote for it.
    //
    // CLOSED, like the spell primitives, and for a sharper reason than tidiness: Conversation's own
    // comment calls a campaign-defined command a security boundary on a storefront. every verb here
    // is the engine's. a command that is not one of them is still recorded and stepped over.
    public enum RequestKind
    {
        // <<check athletics 15>>, <<check stealth hard>>, <<check str 12>> - a d20 test by the hero
        Check,

        // <<save dex 13>>
        Save,

        // <<roll 1d20>> - the GM rolls behind the screen, and the number is the author's to use
        Roll,

        // <<encounter north_road>> - the GM consults one of the campaign's encounter tables
        Encounter,

        // <<fight goblin_ambush>> - the story stops for a fight on a map and comes back after it
        Fight,

        // <<give potion_of_healing 2>>
        Give,

        // <<gold 50>> - negative takes it away
        Gold,

        // <<level 3>> - milestone levelling: the campaign says when
        Level,

        // <<loot goblin_pockets>> - the GM opens one of the campaign's loot tables
        Loot,

        // <<shop village_store>> - the story stops at a merchant's counter and comes back when the
        // player walks away from it (a scene, like a fight)
        Shop,

        // <<rest short>> or <<rest long>> - the hero rests, and a rest is an autosave
        Rest,
    }

    public sealed class Request
    {
        Request(RequestKind kind, string command)
        {
            Kind = kind;
            Command = command;
        }

        public RequestKind Kind { get; }

        // what the campaign wrote, for the log
        public string Command { get; }

        public Skill Skill { get; private set; } = Skill.None;

        public Ability Ability { get; private set; }

        public int Dc { get; private set; }

        public DiceRoll Dice { get; private set; }

        // a table id, a fight id, an item id
        public string Id { get; private set; } = "";

        public int Amount { get; private set; }

        public override string ToString() => Command;

        // the verbs, with the variable each one answers into. the variables are declared to the
        // Yarn compiler by DialogueBook, so a campaign reads them without declaring them
        public static readonly IReadOnlyDictionary<string, RequestKind> Verbs =
            new Dictionary<string, RequestKind>(StringComparer.Ordinal)
            {
                ["check"] = RequestKind.Check,
                ["save"] = RequestKind.Save,
                ["roll"] = RequestKind.Roll,
                ["encounter"] = RequestKind.Encounter,
                ["fight"] = RequestKind.Fight,
                ["give"] = RequestKind.Give,
                ["gold"] = RequestKind.Gold,
                ["level"] = RequestKind.Level,
                ["loot"] = RequestKind.Loot,
                ["shop"] = RequestKind.Shop,
                ["rest"] = RequestKind.Rest,
            };

        // null when it is not an engine verb at all; a problem when it is one written wrong
        public static Request Parse(string command, out string problem)
        {
            problem = null;

            string[] words = (command ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0 || !Verbs.TryGetValue(words[0], out RequestKind kind)) return null;

            var request = new Request(kind, command);

            switch (kind)
            {
                case RequestKind.Check:
                case RequestKind.Save:
                {
                    if (words.Length != 3)
                    {
                        problem = $"'{command}' - it is <<{words[0]} what dc>>";
                        return null;
                    }

                    if (kind == RequestKind.Check && Skills.TryParse(words[1], out Skill skill))
                    {
                        request.Skill = skill;
                        request.Ability = skill.Governs();
                    }
                    else if (Abilities.TryParse(words[1], out Ability ability))
                    {
                        request.Ability = ability;
                    }
                    else
                    {
                        problem = $"'{command}' - '{words[1]}' is not " +
                                  (kind == RequestKind.Check ? "a skill or an ability" : "an ability");
                        return null;
                    }

                    if (!ReadDc(words[2], out int dc))
                    {
                        problem = $"'{command}' - '{words[2]}' is not a number or one of " +
                                  string.Join(", ", Difficulties.Ladder.Select(d => d.Id()));
                        return null;
                    }

                    request.Dc = dc;
                    return request;
                }

                case RequestKind.Roll:
                {
                    if (words.Length != 2 || !DiceRoll.TryParse(words[1], out DiceRoll dice, out _) ||
                        !dice.RollsAnything)
                    {
                        problem = $"'{command}' - it is <<roll 1d20>>";
                        return null;
                    }

                    request.Dice = dice;
                    return request;
                }

                case RequestKind.Rest:
                {
                    if (words.Length != 2 || words[1] != "short" && words[1] != "long")
                    {
                        problem = $"'{command}' - it is <<rest short>> or <<rest long>>";
                        return null;
                    }

                    request.Id = words[1];
                    return request;
                }

                case RequestKind.Encounter:
                case RequestKind.Fight:
                case RequestKind.Loot:
                case RequestKind.Shop:
                {
                    if (words.Length != 2 || !Schema.Json.IsId(words[1]))
                    {
                        problem = $"'{command}' - it is <<{words[0]} an_id>>";
                        return null;
                    }

                    request.Id = words[1];
                    return request;
                }

                case RequestKind.Give:
                {
                    int count = 1;

                    if (words.Length < 2 || words.Length > 3 || !Schema.Json.IsId(words[1]) ||
                        words.Length == 3 && (!int.TryParse(words[2], out count) || count < 1))
                    {
                        problem = $"'{command}' - it is <<give item_id>> or <<give item_id 2>>";
                        return null;
                    }

                    request.Id = words[1];
                    request.Amount = count;
                    return request;
                }

                case RequestKind.Gold:
                case RequestKind.Level:
                {
                    if (words.Length != 2 || !int.TryParse(words[1], out int amount) ||
                        kind == RequestKind.Level && (amount < 1 || amount > 20))
                    {
                        problem = kind == RequestKind.Gold
                            ? $"'{command}' - it is <<gold 50>>, or <<gold -50>> to take it"
                            : $"'{command}' - it is <<level 3>>, a level from 1 to 20";
                        return null;
                    }

                    request.Amount = amount;
                    return request;
                }
            }

            return null;
        }

        // a number, or a word from SRD's DC ladder: "hard" is 20
        static bool ReadDc(string word, out int dc)
        {
            if (int.TryParse(word, out dc)) return dc > 0;

            foreach (Difficulty difficulty in Difficulties.Ladder)
            {
                if (!string.Equals(difficulty.Id(), word, StringComparison.OrdinalIgnoreCase)) continue;

                dc = difficulty.Dc();
                return true;
            }

            return false;
        }
    }

    // what the table says back. the conversation writes it into the variables below and carries on
    public sealed class Answer
    {
        public bool Passed { get; init; }

        public int Total { get; init; }

        public int Natural { get; init; }

        // a natural 1 or 20 on a check: the consequence pool is owed a draw
        // (decisions_checklist.md section 1). the table draws it; the author can branch on it too
        public bool DrawsConsequence { get; init; }

        // an encounter table's pick, "" for nothing
        public string Entry { get; init; } = "";

        public bool StartsAFight { get; init; }

        public Core.Combat.Outcome Outcome { get; init; }

        public int Gold { get; init; }

        public static Answer Of(Attempt attempt) => attempt == null
            ? new Answer()
            : new Answer
            {
                Passed = attempt.Succeeded,
                Total = attempt.Total,
                Natural = attempt.Natural,
                DrawsConsequence = attempt.DrawsConsequence,
            };
    }

    // the Yarn variables the answers land in. the names are part of the authoring format - the
    // campaign guide has to list them - so they live in one place
    public static class RequestVariables
    {
        public const string Passed = "$passed";
        public const string Total = "$total";
        public const string Natural = "$natural";
        public const string Consequence = "$consequence";
        public const string Roll = "$roll";
        public const string Encounter = "$encounter";
        public const string EncounterFight = "$encounter_fight";
        public const string Fight = "$fight";

        // how much gold a <<loot>> found, so the story can say "a fat purse" or "a few coppers"
        public const string LootGold = "$loot_gold";

        // "won", "lost", "fled" - words rather than numbers, so a .yarn file reads as English
        public static string Word(Core.Combat.Outcome outcome) => outcome switch
        {
            Core.Combat.Outcome.HeroesWon => "won",
            Core.Combat.Outcome.HeroesLost => "lost",
            Core.Combat.Outcome.Fled => "fled",
            _ => "",
        };
    }
}
