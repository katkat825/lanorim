using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Play;
using Content.Saves;
using Core.Localization;

namespace Content.Screens
{
    public sealed class ChoiceRow
    {
        public int Option { get; init; }

        public string Key { get; init; } = "";

        public IReadOnlyList<string> Substitutions { get; init; } = Array.Empty<string>();

        // a choice the story shows but will not take (its condition failed): drawn, not pressable
        public bool Offered { get; init; }
    }

    // THE DIALOGUE POPUP (Tier 2.8): who speaks, the line, and Continue - or the choices. The
    // narrator ('dm') has no name plate; 'companion' is said by whichever companion is travelling
    public sealed class DialogueView
    {
        public DialogueView(CampaignRun run, string companion = null)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            Companion = companion;
        }

        public CampaignRun Run { get; }

        public string Companion { get; }

        Conversation Talk => Run.Talk;

        public bool Showing => Run.Now == Scene.Line || Run.Now == Scene.Choice;

        public string Speaker =>
            Talk.Saying == null ? null
            : Companion != null ? Talk.Saying.SpeakerFor(Companion)
            : Talk.Saying.Speaker;

        public const string Narrator = "dm";

        // null for the narrator: no name plate
        public string SpeakerNameKey =>
            Speaker == null || Speaker == Narrator ? null : KeyConventions.ActorName(Speaker);

        public string LineKey => Talk.Saying?.Key;

        public IReadOnlyList<string> Substitutions => Talk.Saying?.Substitutions ?? Array.Empty<string>();

        public IReadOnlyList<ChoiceRow> Choices =>
            Talk.Choosing.Select((c, i) => new ChoiceRow
                {
                    Option = i,
                    Key = c.Key,
                    Substitutions = c.Substitutions,
                    Offered = c.Offered,
                })
                .ToList();

        public bool CanContinue => Run.Now == Scene.Line;

        public Scene Continue() => Run.Next();

        public Scene Choose(int option) => Run.Choose(option);

        public static readonly string ContinueKey = ScreenKeys.Key("dialogue", "continue");

        public static IEnumerable<string> Keys() => new[] { ContinueKey };
    }

    // THE DEATH SCREEN (Tier 2.8): the hero is gone; reload the save from just before, load
    // another, or go back to the book
    public sealed class DeathView
    {
        public DeathView(SaveLibrary saves, string campaign, int slot)
        {
            Reload = saves?.ForReload(campaign, slot);
        }

        public SaveShelf.Saved Reload { get; }

        public bool CanReload => Reload != null;

        public static readonly string TitleKey = ScreenKeys.Key("death", "title");
        public static readonly string ReloadKey = ScreenKeys.Key("death", "reload");
        public static readonly string LoadKey = ScreenKeys.Key("death", "load");
        public static readonly string BookKey = ScreenKeys.Key("death", "to_the_book");
        public static readonly string NoSaveKey = ScreenKeys.Key("death", "no_save");

        public static IEnumerable<string> Keys() => new[] { TitleKey, ReloadKey, LoadKey, BookKey, NoSaveKey };
    }
}
