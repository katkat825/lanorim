using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Play;
using Core.Characters;
using Core.Localization;

namespace Content.Screens
{
    // "WHERE ARE WE?" (F2; cc_task_open-questions-answers.md 4.2): what a lost player is told, at any moment, as whole
    // keyed sentences (Game.Access.Spoken's rule: never glued from parts). The campaign; in a fight the round, whose
    // turn it is, the hero's hit points and how many enemies are still standing; out of one, that the story goes on.
    // An argument that is itself a key (the campaign's name, a monster's) is the game's to say
    public sealed record Said(string Key, params object[] Args);

    public static class Whereabouts
    {
        static string K(string what) => ScreenKeys.Key("where", what);

        public static readonly string CampaignKey = K("campaign");
        public static readonly string RoundKey = K("round");
        public static readonly string YourTurnKey = K("your_turn");
        public static readonly string TheirTurnKey = K("their_turn");
        public static readonly string HealthKey = K("health");
        public static readonly string FoesKey = K("foes");
        public static readonly string StoryKey = K("story");
        public static readonly string BookKey = K("book");

        public static IEnumerable<string> Keys() =>
            new[] { CampaignKey, RoundKey, YourTurnKey, TheirTurnKey, HealthKey, FoesKey, StoryKey, BookKey };

        // on the launch screen, with no campaign under way
        public static IReadOnlyList<Said> AtTheBook() => new[] { new Said(BookKey) };

        public static IReadOnlyList<Said> Of(CampaignRun run, CombatSession fight = null)
        {
            var said = new List<Said>();

            if (run?.Pack?.Manifest is { } manifest) said.Add(new Said(CampaignKey, manifest.NameKey));

            if (fight != null && !fight.Fight.Over)
            {
                said.Add(new Said(RoundKey, fight.Fight.Round));

                Actor turn = fight.Turn?.Actor;

                if (turn != null)
                    said.Add(ReferenceEquals(turn, fight.Hero.Actor)
                        ? new Said(YourTurnKey)
                        : new Said(TheirTurnKey, fight.Battle.StatblockOf(turn)?.NameKey ?? turn.Id));

                Actor hero = fight.Hero.Actor;
                said.Add(new Said(HealthKey, run?.Hero?.Name ?? hero.Id, hero.Health.Current, hero.Health.Maximum));

                int standing = fight.Fight.Actors.Count(a => a.Side != hero.Side && !a.IsDown);
                said.Add(new Said(FoesKey, standing));

                return said;
            }

            if (run?.Hero is { } h)
            {
                said.Add(new Said(StoryKey));
                said.Add(new Said(HealthKey, h.Name, h.Actor.Health.Current, h.Actor.Health.Maximum));
            }

            return said;
        }
    }

    // F1 (cc_task_open-questions-answers.md 4.2): the rules in a few lines, the ones a player who is stuck needs; the
    // keys follow them on the card, each as the Controls page says it
    public static class HelpCard
    {
        static string K(string what) => ScreenKeys.Key("help", what);

        public static readonly string TitleKey = K("title");

        public static readonly IReadOnlyList<string> Lines = new[]
        {
            K("story"), K("dice"), K("turn"), K("move"), K("attack"), K("more"), K("keys"), K("close"),
        };

        public static IEnumerable<string> Keys() => new[] { TitleKey }.Concat(Lines);
    }
}
