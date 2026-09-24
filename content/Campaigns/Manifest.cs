using System;
using System.Collections.Generic;
using Core.Localization;

namespace Content.Campaigns
{
    // WHAT A PACK SAYS ABOUT ITSELF, and the only file in a campaign folder that has to be there.
    //
    // LIFTED from the old build. One thing changed, and it is the World layer leaving: a chapter
    // used to be a sequence of PLACES, because a place owned a map and a fight was one of the
    // things that could happen in it. lanorim has no World layer, so a chapter is a sequence of
    // MAPS and the fight is the thing that happens on one.
    //
    // Nothing here is player-visible text. A campaign's name and description are localized, so they
    // live in its own locale CSV under keys derived from the id - which is why there is no `name`
    // field to get wrong, and why ManifestReader says so by name when an author writes one anyway.
    public sealed class Manifest
    {
        public Manifest(string id, PackKind kind, int format, Version engine, string author,
                        IReadOnlyList<string> tags, string preview,
                        IReadOnlyList<string> dependencies,
                        IReadOnlyList<Chapter> chapters, string start)
        {
            Id = id;
            Kind = kind;
            Format = format;
            Engine = engine;
            Author = author ?? "";
            Tags = tags ?? Array.Empty<string>();
            Preview = preview ?? "";
            Dependencies = dependencies ?? Array.Empty<string>();
            Chapters = chapters ?? Array.Empty<Chapter>();
            Start = start ?? "";
        }

        public string Id { get; }

        public PackKind Kind { get; }

        // campaign and mixed have chapters to play; a mini or class pack does not
        public bool IsPlayable => Kind == PackKind.Campaign || Kind == PackKind.Mixed;

        public int Format { get; }

        // oldest engine that can play it
        public Version Engine { get; }

        public string Author { get; }

        public IReadOnlyList<string> Tags { get; }

        public string Preview { get; }

        public IReadOnlyList<string> Dependencies { get; }

        public IReadOnlyList<Chapter> Chapters { get; }

        public string Start { get; }

        public string NameKey => KeyConventions.Key(KeyConventions.CampaignNs, Id, "name");

        public string DescriptionKey =>
            KeyConventions.Key(KeyConventions.CampaignNs, Id, "description");

        public static string ChapterTitle(string campaign, string chapter) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, chapter, "title");

        // every key this manifest promises the locale will answer. The audit walks this rather than
        // a list somebody keeps in step by hand.
        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            foreach (Chapter chapter in Chapters) yield return ChapterTitle(Id, chapter.Id);
        }

        public override string ToString() =>
            $"{Id} [{Kind.ToString().ToLowerInvariant()}] format {Format}, engine {Engine}" +
            (IsPlayable
                ? $", {Chapters.Count} chapters, starts at " + (Start.Length > 0 ? Start : "nothing")
                : "") +
            (Dependencies.Count > 0 ? $", needs {string.Join(", ", Dependencies)}" : "");
    }

    public sealed class Chapter
    {
        public Chapter(string id, IReadOnlyList<string> maps)
        {
            Id = id;
            Maps = maps ?? Array.Empty<string>();
        }

        public string Id { get; }

        // the maps it is played on, in order
        public IReadOnlyList<string> Maps { get; }

        public override string ToString() => $"{Id}: {string.Join(", ", Maps)}";
    }
}
