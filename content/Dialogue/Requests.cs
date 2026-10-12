using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Words;

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

        // <<roll 1d20>> - the GM rolls behind the screen, and the number is the author's to use. silent unless
        // the author writes <<roll 1d20 heard>>, which rattles the screen (cc_task_i Part 1)
        Roll,

        // <<encounter north_road>> - the GM consults one of the campaign's encounter tables
        Encounter,

        // <<fight goblin_ambush>> - the story stops for a fight on a map and comes back after it
        Fight,

        // <<give potion_of_healing 2>>
        Give,

        // <<gold 50>> - negative takes it away
        Gold,

        // <<level 3>> - milestone levelling: the campaign says when. ignored, with a warning, in a campaign that levels
        // by XP (cc_task_i 3.4)
        Level,

        // <<xp 50>> - experience points, in a campaign that levels by XP (cc_task_i 3.4); ignored, with a warning,
        // in a milestone one
        Xp,

        // <<loot goblin_pockets>> - the GM opens one of the campaign's loot tables
        Loot,

        // <<shop village_store>> - the story stops at a merchant's counter and comes back when the
        // player walks away from it (a scene, like a fight)
        Shop,

        // <<rest short>> or <<rest long>> - the hero rests, and a rest is an autosave
        Rest,

        // <<armory crucible_racks>> - a loadout screen, not a shop (cc_task_i 3.3): the hero wears anything in the
        // armory's stock for free, and the story comes back when the player walks away (a scene, like a shop)
        Armory,

        // <<bark nearby>> - the companion says a line from one of its bark pools (cc_task_i 2.3). nothing to
        // answer; the story goes straight on
        Bark,
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

        // <<roll 1d20 heard>>: the player hears the dice behind the screen
        public bool Heard { get; private set; }

        // the word that makes a <<roll>> heard
        public const string HeardWord = "heard";

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
                ["xp"] = RequestKind.Xp,
                ["loot"] = RequestKind.Loot,
                ["shop"] = RequestKind.Shop,
                ["rest"] = RequestKind.Rest,
                ["bark"] = RequestKind.Bark,
                ["armory"] = RequestKind.Armory,
            };

        // null when it is not an engine verb at all; a problem when it is one written wrong
        public static Request Parse(string command, out string problem)
        {
            problem = null;

            string[] words = (command ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0 || !Verbs.TryGetValue(words[0], out RequestKind kind)) return null;

            // each shape of command reads its own words into the request, or says what is wrong
            Func<Request, string[], string, string> read = kind switch
            {
                RequestKind.Check or RequestKind.Save => ReadTest,
                RequestKind.Roll => ReadRoll,
                RequestKind.Rest => ReadRest,
                RequestKind.Bark => ReadBark,
                RequestKind.Encounter or RequestKind.Fight or RequestKind.Loot or RequestKind.Shop or RequestKind.Armory => ReadId,
                RequestKind.Give => ReadGive,
                RequestKind.Gold or RequestKind.Level or RequestKind.Xp => ReadAmount,
                _ => null,
            };

            if (read == null) return null;

            var request = new Request(kind, command);

            problem = read(request, words, command);

            return problem == null ? request : null;
        }

        // <<check skill dc>>, <<save ability dc>>
        static string ReadTest(Request request, string[] words, string command)
        {
            RequestKind kind = request.Kind;

            if (words.Length != 3) return $"'{command}' - it is <<{words[0]} what dc>>";

            if (kind == RequestKind.Check && EnumWords.TryParse(words[1], out Skill skill))
            {
                request.Skill = skill;
                request.Ability = skill.Governs();
            }
            else if (EnumWords.TryParse(words[1], out Ability ability))
            {
                request.Ability = ability;
            }
            else
            {
                return $"'{command}' - '{words[1]}' is not " +
                       (kind == RequestKind.Check ? "a skill or an ability" : "an ability");
            }

            if (!ReadDc(words[2], out int dc))
                return $"'{command}' - '{words[2]}' is not a number or one of " +
                       string.Join(", ", Difficulties.Ladder.Select(d => d.Id()));

            request.Dc = dc;
            return null;
        }

        static string ReadRoll(Request request, string[] words, string command)
        {
            if (words.Length is < 2 or > 3 || !DiceRoll.TryParse(words[1], out DiceRoll dice, out _) || !dice.RollsAnything ||
                words.Length == 3 && words[2] != HeardWord)
                return $"'{command}' - it is <<roll 1d20>>, or <<roll 1d20 {HeardWord}>> for one the player hears";

            request.Dice = dice;
            request.Heard = words.Length == 3;
            return null;
        }

        static string ReadRest(Request request, string[] words, string command)
        {
            if (words.Length != 2 || words[1] != "short" && words[1] != "long")
                return $"'{command}' - it is <<rest short>> or <<rest long>>";

            request.Id = words[1];
            return null;
        }

        // <<bark danger>>: a pool the engine has, by its word
        static string ReadBark(Request request, string[] words, string command)
        {
            if (words.Length != 2 || !EnumWords.TryParse(words[1], out Bark pool) || !pool.Speaks())
                return $"'{command}' - it is <<bark pool>>, the pool one of " +
                       string.Join(", ", BarkPools.Spoken.Select(p => p.Id()));

            request.Id = pool.Id();
            return null;
        }

        static string ReadId(Request request, string[] words, string command)
        {
            if (words.Length != 2 || !Schema.Json.IsId(words[1])) return $"'{command}' - it is <<{words[0]} an_id>>";

            request.Id = words[1];
            return null;
        }

        static string ReadGive(Request request, string[] words, string command)
        {
            int count = 1;

            if (words.Length < 2 || words.Length > 3 || !Schema.Json.IsId(words[1]) ||
                words.Length == 3 && (!int.TryParse(words[2], out count) || count < 1))
                return $"'{command}' - it is <<give item_id>> or <<give item_id 2>>";

            request.Id = words[1];
            request.Amount = count;
            return null;
        }

        static string ReadAmount(Request request, string[] words, string command)
        {
            if (words.Length != 2 || !int.TryParse(words[1], out int amount) ||
                request.Kind == RequestKind.Level && (amount < 1 || amount > 20) ||
                request.Kind == RequestKind.Xp && amount < 1)
                return request.Kind switch
                {
                    RequestKind.Gold => $"'{command}' - it is <<gold 50>>, or <<gold -50>> to take it",
                    RequestKind.Xp => $"'{command}' - it is <<xp 50>>, a number of experience points",
                    _ => $"'{command}' - it is <<level 3>>, a level from 1 to 20",
                };

            request.Amount = amount;
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

        // the fight was won because the enemies broke (cc_task_h 3.4)
        public bool Broke { get; init; }

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
        // won, lost, fled, or yielded (cc_task_i 3.2: a bout that continues after defeat, given up)
        public const string Fight = "$fight";

        // true when the enemies' morale ended the fight - a rout, or a surrender - rather than the last of them falling
        // (cc_task_h 3.4), so the story can tell a rout from a slaughter
        public const string FightBroke = "$fight_broke";

        // how much gold a <<loot>> found, so the story can say "a fat purse" or "a few coppers"
        public const string LootGold = "$loot_gold";
    }
}
