using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Content.Campaigns;

namespace Content.Workshop.Story
{
    // A CAMPAIGN'S STORY AS THE WORKSHOP EDITS IT (cc_task_o): its cards - one a Yarn node - in the files they came from,
    // and how each looks on the board (where it sits, its colour, its name; kept in workshop.json, never in the Yarn).
    // Part of the CampaignDraft: an edit is one undo step, and the autosave keeps it
    public sealed class StoryDraft
    {
        // where a new card goes, unless it's made beside a card of another file
        public const string StoryFile = Package.DialogueFolder + "/story.yarn";

        public List<CardDraft> Cards { get; set; } = new();

        // the board: each card's place, colour and name, and where the board was looking
        public Dictionary<string, CardLook> Looks { get; set; } = new(StringComparer.Ordinal);

        public BoardView View { get; set; } = new();

        // WHAT THE STORY REMEMBERS, as the author phrased it: "$tavin_was_brave" -> "Tavin was brave" (Memory)
        public Dictionary<string, string> Phrases { get; set; } = new(StringComparer.Ordinal);

        public CardDraft Card(string title) => Cards.FirstOrDefault(c => c.Title == title);

        public CardLook Look(string title)
        {
            if (!Looks.TryGetValue(title, out CardLook look)) Looks[title] = look = new CardLook();
            return look;
        }

        public string NameOf(string title) =>
            Looks.TryGetValue(title, out CardLook look) && look.Name.Length > 0 ? look.Name : Tables.TablesDraft.Plain(title);

        // EVERY LINE ID IN THE CAMPAIGN, a Text card's too: a new line's id is made not to be one of them (Yarn wants
        // every line's id once, across every file)
        public HashSet<string> LineIds() =>
            Cards.SelectMany(c => c.IsText ? Regex.Matches(c.Text, @"#line:(\w+)").Select(m => m.Groups[1].Value)
                                           : Blocks.Lines(c.Blocks).Select(l => l.Id))
                 .ToHashSet(StringComparer.Ordinal);

        // A NEW LINE'S ID: the card's name and a number, made once and never changed by editing the words (arrive_3)
        public string NewLineId(string card, ISet<string> also = null)
        {
            HashSet<string> taken = LineIds();
            if (also != null) taken.UnionWith(also);
            string stem = ContentId.From(card, "line");

            for (int n = 1; ; n++)
                if (!taken.Contains($"{stem}_{n}")) return $"{stem}_{n}";
        }

        // where each card leads: its go-tos and its options' go-tos (and a Text card's <<jump>>s), with the option's
        // letter where there is one
        public static IEnumerable<(string To, string Letter)> Leads(CardDraft card)
        {
            if (card.IsText)
            {
                foreach (Match jump in Regex.Matches(card.Text, @"<<jump\s+(\w+)\s*>>")) yield return (jump.Groups[1].Value, "");
                yield break;
            }

            foreach ((string to, string letter) in Leads(card.Blocks, "")) yield return (to, letter);
        }

        static IEnumerable<(string To, string Letter)> Leads(IEnumerable<Block> blocks, string letter)
        {
            foreach (Block block in blocks)
            {
                if (block is Jump jump) yield return (jump.Target, letter);

                if (block is Choice choice)
                {
                    for (int i = 0; i < choice.Options.Count; i++)
                        foreach (var lead in Leads(choice.Options[i].Blocks, Letter(i)))
                            yield return lead;

                    continue;
                }

                foreach (List<Block> inside in block.Inside)
                    foreach (var lead in Leads(inside, letter))
                        yield return lead;
            }
        }

        // A, B, C... an option's letter on the note
        public static string Letter(int index) => ((char)('A' + index % 26)).ToString();

        public override string ToString() => $"{Cards.Count} cards";
    }

    // ONE CARD: a Yarn node. Its header lines as written (title:, speaker:, any others) and its blocks; or, for a node
    // outside the editor's subset, its raw text (a Text card, kept byte for byte, editable only in Advanced)
    public sealed class CardDraft
    {
        // the file it lives in, under the campaign: "dialogue/story.yarn"
        public string File { get; set; } = StoryDraft.StoryFile;

        // the title it had when the files were read ("" for a card made since): where its old text was in its file
        public string Was { get; set; } = "";

        public List<string> Headers { get; set; } = new();

        // null: a Text card
        public List<Block> Blocks { get; set; }

        // a Text card's whole node, as written
        public string Text { get; set; } = "";

        // the node as it was read, so an untouched card is written back as it was ("" for a card made since)
        public string Read { get; set; } = "";

        [JsonIgnore]
        public bool IsText => Blocks == null;

        [JsonIgnore]
        public string Title => IsText ? YarnNode.Header(Lines(Text), "title") : YarnNode.Header(Headers, "title");

        // the speaker a line has when it doesn't say (the node's "speaker:" header)
        [JsonIgnore]
        public string Speaker => IsText ? YarnNode.Header(Lines(Text), StoryWords.SpeakerHeader) : YarnNode.Header(Headers, StoryWords.SpeakerHeader);

        static IEnumerable<string> Lines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r'));

        public static CardDraft Of(YarnNode node, string file)
        {
            List<Block> blocks = node.Sound && node.Headers.All(h => Regex.IsMatch(h, @"^\w+: ?\S")) ? StoryText.Read(node.Body) : null;

            return new CardDraft
            {
                File = file,
                Was = node.Title,
                Headers = node.Headers.ToList(),
                Blocks = blocks,
                Text = blocks == null ? node.Raw : "",
                Read = node.Raw,
            };
        }

        // a new card: its title, the GM speaking, nothing in it yet
        public static CardDraft New(string title, string file = StoryDraft.StoryFile) => new()
        {
            File = file,
            Headers = new List<string> { "title: " + title, StoryWords.SpeakerHeader + ": " + StoryWords.Gm },
            Blocks = new List<Block>(),
        };

        // THE NODE'S TEXT: a Text card's own, an untouched card's as it was read, any other written from its blocks.
        // Ends with its line break when the read one did (a node at the very end of a file may not)
        public string Write(string newline)
        {
            if (IsText) return Text;

            var lines = new List<string>(Headers) { "---" };
            lines.AddRange(StoryText.Write(Blocks));
            lines.Add("===");

            string written = string.Join(newline, lines) + newline;

            if (Read.Length > 0 && !Read.EndsWith("\n") && written.EndsWith(newline)) written = written.Substring(0, written.Length - newline.Length);

            return written;
        }

        // a copy of it, blocks and all
        public CardDraft Copy() =>
            System.Text.Json.JsonSerializer.Deserialize<CardDraft>(System.Text.Json.JsonSerializer.Serialize(this));

        // retitled: the "title:" header (a Text card's first title line)
        public void Retitle(string title)
        {
            if (IsText)
            {
                Text = Regex.Replace(Text, @"^title:.*$", "title: " + title, RegexOptions.Multiline);
                return;
            }

            int at = Headers.FindIndex(h => h.StartsWith("title:", StringComparison.Ordinal));
            if (at >= 0) Headers[at] = "title: " + title;
            else Headers.Insert(0, "title: " + title);
        }

        public override string ToString() => $"{Title}{(IsText ? " (text)" : $": {Blocks.Count} blocks")}";
    }

    // where a card sits on the board, its colour ("" the theme's) and its name ("" its title made plain)
    public sealed class CardLook
    {
        public float X { get; set; }

        public float Y { get; set; }

        public bool Placed { get; set; }

        public string Colour { get; set; } = "";

        public string Name { get; set; } = "";

        public bool IsPlain => !Placed && Colour.Length == 0 && Name.Length == 0;
    }

    public sealed class BoardView
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Zoom { get; set; } = 1;
    }
}
