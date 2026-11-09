using System;
using Core.Tables;

namespace Content.Challengers
{
    // THE ARENA'S NUMBERS (cc_task_cg Parts 2 and 4), pack.json's "challengers" block: how far a bout moves reputation,
    // how many challengers reputation puts on the roster, and a won bout's XP. Each is optional, and the defaults are
    // Kathleen's (+5 a win, -2 a loss, from the Crucible's Prose.md) and the assessment's (a quarter of a level a bout)
    public sealed class ArenaRules
    {
        public static readonly ArenaRules Default = new ArenaRules();

        // reputation for a won bout, and for a lost one (a yielded or fled bout is a lost one)
        public int Win { get; init; } = 5;

        public int Loss { get; init; } = -2;

        // THE ROSTER'S SIZE: Least, and one more for every Every reputation, never more than Most
        public int Least { get; init; } = 2;

        public int Every { get; init; } = 10;

        public int Most { get; init; } = 6;

        public int RosterSize(int reputation) =>
            Math.Clamp(Least + Math.Max(0, reputation) / Math.Max(1, Every), 1, Math.Max(1, Most));

        // A WON BOUT'S XP: Share of what the hero needs from this level to the next, times the challenger's difficulty
        public double Share { get; init; } = 0.25;

        public double Low { get; init; } = 0.75;

        public double Moderate { get; init; } = 1.0;

        public double High { get; init; } = 1.5;

        public double Times(EncounterDifficulty difficulty) => difficulty switch
        {
            EncounterDifficulty.Low => Low,
            EncounterDifficulty.High => High,
            _ => Moderate,
        };

        public int Moved(int reputation, bool won) => Math.Max(0, reputation + (won ? Win : Loss));

        public bool IsDefault =>
            Win == Default.Win && Loss == Default.Loss && Least == Default.Least && Every == Default.Every &&
            Most == Default.Most && Share == Default.Share && Low == Default.Low && Moderate == Default.Moderate &&
            High == Default.High;

        public override string ToString() =>
            $"win {Win:+0;-0}, loss {Loss:+0;-0}, roster {Least} +1 per {Every} to {Most}, " +
            $"bout XP {Share} x {Low}/{Moderate}/{High}";
    }
}
