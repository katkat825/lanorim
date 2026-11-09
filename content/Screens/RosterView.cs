using System;
using System.Collections.Generic;
using System.Linq;
using Content.Challengers;
using Content.Monsters;
using Content.Workshop.Tables;
using Core.Localization;
using Core.Words;

namespace Content.Screens
{
    // THE ARENA'S ROSTER, AS THE SCREEN SHOWS IT (cc_task_cg Part 6): each challenger on offer by name, with its
    // difficulty (the Workshop's own Easy / Medium / Hard, TableWords.Difficulty) and whether it's a special fight;
    // the one selected shows its description, what it is and whether it's one opponent, a group or a team. Fight picks
    // it (CampaignRun.Pick); Back goes to the story without picking (CampaignRun.LeaveRoster)
    public sealed class RosterView
    {
        readonly Func<string, Monster> _find;

        public RosterView(Arena arena, Func<string, Monster> find)
        {
            Arena = arena ?? new Arena();
            _find = find ?? (_ => null);
            Selected = Arena.Roster.FirstOrDefault();
        }

        public Arena Arena { get; }

        public int Reputation => Arena.Reputation;

        public IReadOnlyList<Challenger> Rows => Arena.Roster;

        public Challenger Selected { get; private set; }

        public bool Select(string id)
        {
            Challenger found = Rows.FirstOrDefault(c => c.Id == id);

            if (found == null) return false;

            Selected = found;
            return true;
        }

        public static string DifficultyKey(Challenger challenger) => TableWords.Difficulty(challenger.Difficulty);

        public static string ShapeKey(Challenger challenger) =>
            challenger.Shape == ChallengerShape.Team ? TeamKey : challenger.Alone ? AloneKey : GroupKey;

        // who steps into the arena, a line each: "Goblin Warrior" for one, "2 Guards" is "2 × Guard" (MemberKey)
        public IReadOnlyList<(int Count, string NameKey)> Members(Challenger challenger) =>
            (challenger?.Group ?? Array.Empty<Core.Tables.Mustered>())
                .Select(m => (m.Count, _find(m.Monster)?.NameKey ?? KeyConventions.MonsterName(m.Monster)))
                .ToList();

        static readonly Func<string, string> K = ScreenKeys.For("roster");

        public static readonly string TitleKey = K("title");
        public static readonly string ReputationKey = K("reputation");
        public static readonly string SpecialKey = K("special");
        public static readonly string AloneKey = K("alone");
        public static readonly string GroupKey = K("group");
        public static readonly string TeamKey = K("team");
        public static readonly string MemberKey = K("member");
        public static readonly string WhoKey = K("who");
        public static readonly string FightKey = K("fight");
        public static readonly string BackKey = K("back");
        public static readonly string EmptyKey = K("empty");
        public static readonly string ChooseKey = K("choose");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, ReputationKey, SpecialKey, AloneKey, GroupKey, TeamKey, MemberKey, WhoKey, FightKey, BackKey,
                EmptyKey, ChooseKey,
            };

        public override string ToString() =>
            $"{Rows.Count} on the roster, reputation {Reputation}" + (Selected != null ? $", {Selected.Id} selected" : "");
    }
}
