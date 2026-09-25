using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Core.Magic;
using Core.Resolution;

namespace Content.Combat
{
    // WHAT THE PLAYER WANTS DONE WITH A REACTION, one setting per reaction (combat_ux.md). Auto is
    // the engine's own sense - opportunity attacks always, a Shield only when it turns the hit, a
    // smite never unasked - and it is what every reaction starts on.
    public enum ReactionPolicy
    {
        Auto,
        Always,
        Ask,
        Never,
    }

    public static class ReactionPolicies
    {
        public static readonly IReadOnlyList<ReactionPolicy> All = new[]
        {
            ReactionPolicy.Auto, ReactionPolicy.Always, ReactionPolicy.Ask, ReactionPolicy.Never,
        };

        public static string Id(this ReactionPolicy policy) => policy.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out ReactionPolicy policy)
        {
            foreach (ReactionPolicy p in All)
            {
                if (!string.Equals(p.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                policy = p;
                return true;
            }

            policy = ReactionPolicy.Auto;
            return false;
        }

        public static string NameKey(this ReactionPolicy policy) =>
            KeyConventions.Key(KeyConventions.UiNs, "reaction_policy", policy.Id(), "name");

        public static IEnumerable<string> Keys() =>
            All.Select(NameKey).Concat(ReactionQuestion.Keys());
    }

    // the player's settings, by reaction id ("opportunity_attack", "shield", "divine_smite")
    public sealed class ReactionSettings
    {
        readonly Dictionary<string, ReactionPolicy> _policies =
            new Dictionary<string, ReactionPolicy>(StringComparer.Ordinal);

        public ReactionPolicy For(string reaction) =>
            reaction != null && _policies.TryGetValue(reaction, out ReactionPolicy p)
                ? p
                : ReactionPolicy.Auto;

        public void Set(string reaction, ReactionPolicy policy)
        {
            if (string.IsNullOrEmpty(reaction)) return;

            if (policy == ReactionPolicy.Auto) _policies.Remove(reaction);
            else _policies[reaction] = policy;
        }

        // everything set to something other than Auto - what the settings file saves
        public IReadOnlyDictionary<string, ReactionPolicy> Chosen => _policies;
    }

    // A PENDING QUESTION: the fight has stopped and wants a yes or a no. "Cast Shield? AC 14 → 19
    // turns the hit." The presentation shows it (no timer) and answers through the asker.
    public sealed class ReactionQuestion
    {
        public ReactionQuestion(Actor reactor, IReaction reaction, Moment moment)
        {
            Reactor = reactor;
            Reaction = reaction;
            Moment = moment;
        }

        public Actor Reactor { get; }

        public IReaction Reaction { get; }

        public Moment Moment { get; }

        public string ReactionId => Reaction.Id;

        // the reaction's own name: a spell's, or the opportunity attack's
        public string NameKey =>
            Reaction is SpellReaction spell
                ? spell.Spell.NameKey
                : KeyConventions.Key(KeyConventions.UiNs, "reaction", Reaction.Id, "name");

        // what kind of prompt: "shield" (with the armor class before and after), "attack", "cast",
        // "smite", "damaged"
        public string PromptKey => Prompt(Moment.Trigger, Reaction.Deflects > 0);

        static string Prompt(Trigger trigger, bool deflects) =>
            KeyConventions.Key(KeyConventions.UiNs, "reaction_prompt",
                               deflects ? "deflect" : trigger.Id(), "name");

        // for a deflection: the armor class now and with it
        public int ArmorClassNow => Reactor.ArmorClass;

        public int ArmorClassWith => Reactor.ArmorClass + Reaction.Deflects;

        // would the deflection turn this hit into a miss
        public bool TurnsTheHit =>
            Moment.Attempt != null && Reaction.Deflects > 0 && !Moment.Attempt.IsCritical &&
            !Moment.Attempt.Rejudged(ArmorClassWith).Succeeded;

        // who caused it: the attacker, the caster, the one running past
        public Actor Other => Moment.Source;

        public static IEnumerable<string> Keys()
        {
            yield return Prompt(Trigger.Hit, true);

            foreach (Trigger trigger in Triggers.All) yield return Prompt(trigger, false);

            yield return KeyConventions.Key(KeyConventions.UiNs, "reaction", "opportunity_attack",
                                            "name");
        }
    }

    // who answers an Ask: the presentation. a test answers at once; the table blocks the rules
    // thread until the player clicks
    public interface IReactionAsker
    {
        bool Ask(ReactionQuestion question);
    }

    public sealed class AnswerAlways : IReactionAsker
    {
        readonly bool _yes;

        public AnswerAlways(bool yes) => _yes = yes;

        public bool Ask(ReactionQuestion question) => _yes;
    }

    // THE PLAYER'S CHOOSER, from the settings: for each reaction offered, what its policy says.
    // Auto defers to the engine's own sensible default
    public sealed class PolicyChooser : IReactionChooser
    {
        readonly ReactionSettings _settings;
        readonly IReactionAsker _asker;

        public PolicyChooser(ReactionSettings settings, IReactionAsker asker)
        {
            _settings = settings ?? new ReactionSettings();
            _asker = asker ?? new AnswerAlways(false);
        }

        // every question asked, for the log and tests
        public IReadOnlyList<ReactionQuestion> Asked => _asked;

        readonly List<ReactionQuestion> _asked = new List<ReactionQuestion>();

        public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                IReadOnlyList<IReaction> options)
        {
            IReaction auto = ReactionChoosers.WhenItHelps.Choose(fight, reactor, moment, options);

            foreach (IReaction option in options)
            {
                switch (_settings.For(option.Id))
                {
                    case ReactionPolicy.Never:
                        continue;

                    case ReactionPolicy.Always:
                        return option;

                    case ReactionPolicy.Ask:
                    {
                        var question = new ReactionQuestion(reactor, option, moment);
                        _asked.Add(question);

                        if (_asker.Ask(question)) return option;
                        continue;
                    }

                    default:
                        if (ReferenceEquals(option, auto)) return option;
                        continue;
                }
            }

            return null;
        }
    }
}
