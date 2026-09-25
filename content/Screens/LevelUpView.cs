using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Sheet;
using Core.Characters;

namespace Content.Screens
{
    // THE LEVEL-UP SCREEN (Tier 2.8): what the new level brought - hit points, features - and the
    // ability score improvement, spent by the player (Tier 1e), with the suggestion one button away.
    // Made after Hero.LevelTo, told what the hero was before it.
    public sealed class LevelUpView
    {
        public LevelUpView(Hero hero, int fromLevel, int maxHitPointsBefore)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            From = fromLevel;
            HitPointsGained = Math.Max(0, hero.Actor.Health.Maximum - maxHitPointsBefore);

            NewFeatures = hero.Class.Features
                              .Where(f => f.Level > fromLevel && f.Level <= hero.Level)
                              .ToList();
        }

        public Hero Hero { get; }

        public int From { get; }

        public int Level => Hero.Level;

        public int HitPointsGained { get; }

        public IReadOnlyList<Feature> NewFeatures { get; }

        public int Pending => Hero.PendingImprovements;

        public AbilityImprovement Suggested => Hero.Suggested();

        // the score an ability would have with this improvement, for the preview
        public int ScoreWith(AbilityImprovement choice, Ability ability) =>
            Hero.Actor.Scores.Base(ability) +
            choice.Points.Where(p => p.ability == ability).Sum(p => p.points);

        public bool Improve(AbilityImprovement choice, out string whyNotKey) =>
            Hero.Improve(choice, out whyNotKey);

        public int TakeSuggested() => Hero.ImproveAsSuggested();

        // the screen closes when nothing is left to spend
        public bool Done => Pending == 0;

        public string DoneWhyNotKey => Done ? null : ImprovementsWaitingKey;

        public static readonly string TitleKey = ScreenKeys.Key("level_up", "title");
        public static readonly string HitPointsKey = ScreenKeys.Key("level_up", "hit_points");
        public static readonly string FeaturesKey = ScreenKeys.Key("level_up", "features");
        public static readonly string ImprovementKey = ScreenKeys.Key("level_up", "improvement");
        public static readonly string TwoToOneKey = ScreenKeys.Key("level_up", "two_to_one");
        public static readonly string OneToTwoKey = ScreenKeys.Key("level_up", "one_to_two");
        public static readonly string SuggestedKey = ScreenKeys.Key("level_up", "suggested");
        public static readonly string ImprovementsWaitingKey = ScreenKeys.Key("level_up", "improvements_waiting");
        public static readonly string DoneKey = ScreenKeys.Key("level_up", "done");

        public static IEnumerable<string> Keys() =>
            new[]
            {
                TitleKey, HitPointsKey, FeaturesKey, ImprovementKey, TwoToOneKey, OneToTwoKey,
                SuggestedKey, ImprovementsWaitingKey, DoneKey,
            };
    }
}
