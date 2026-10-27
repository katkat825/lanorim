using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Schema;

namespace Content.Workshop.Story
{
    // A CAMPAIGN'S STORY, READ INTO CARDS AND WRITTEN BACK (cc_task_o Parts 3 and 4): every .yarn under dialogue/, node by
    // node. Saving puts each card back into the file it came from, in its place: an untouched node as it was read, a
    // changed one written fresh from its blocks, a new one at the end of its file, a deleted one gone. Every line the
    // editor's cards speak gets its row in the campaign's locale, under the key the game reads; a line taken out takes
    // its row with it. Rows the editor's cards never had are left alone
    public static class StoryFiles
    {
        public static StoryDraft Open(string folder)
        {
            var story = new StoryDraft();

            // the Workshop's own memory node is written from the cards, never shown as one (Memory.Declarations)
            foreach ((string file, string _, string text) in Files(folder).Where(f => f.File != Memory.File))
                foreach (YarnNode node in YarnText.Of(text).Nodes)
                    story.Cards.Add(CardDraft.Of(node, file));

            return story;
        }

        static IEnumerable<(string File, string Path, string Text)> Files(string folder) =>
            PackFolder.Read(Path.Combine(folder, Package.DialogueFolder), DialogueBook.Extension, Package.DialogueFolder, new List<ContentProblem>());

        // --- save ---------------------------------------------------------------------------------------------------

        // every story file written where it changed, and the lines' words into the sheet. Throws the disk's IOException
        public static void Save(StoryDraft story, string folder, string campaign, LocaleSheet sheet)
        {
            StoryDraft was = Open(folder);
            var files = story.Cards.Select(c => c.File).Concat(was.Cards.Select(c => c.File)).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal);

            foreach (string file in files.ToList())
            {
                string path = Path.Combine(folder, file);
                string original = File.Exists(path) ? File.ReadAllText(path) : "";
                string written = Text(original, story.Cards.Where(c => c.File == file).ToList());

                if (written == original) continue;

                if (written.Trim().Length == 0 && original.Length > 0)
                {
                    File.Delete(path);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, written);
            }

            Words(story, was, campaign, sheet);

            // what the story remembers, declared (or the file taken away when it remembers nothing)
            string memory = Path.Combine(folder, Memory.File);
            string declared = Memory.Declarations(story);

            if (declared.Length == 0 && File.Exists(memory)) File.Delete(memory);
            else if (declared.Length > 0 && (!File.Exists(memory) || File.ReadAllText(memory) != declared))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(memory));
                File.WriteAllText(memory, declared);
            }

            // each card is now what its file holds
            foreach (CardDraft card in story.Cards)
            {
                card.Was = card.Title;
                card.Read = card.Write(YarnText.Of(File.ReadAllText(Path.Combine(folder, card.File))).Newline);
            }
        }

        // ONE FILE'S TEXT with these cards in it: each node in its old place (its new text, or gone), the new ones after
        public static string Text(string original, IReadOnlyList<CardDraft> cards)
        {
            YarnText file = YarnText.Of(original);
            string newline = file.Newline;
            var written = new System.Text.StringBuilder();
            var placed = new HashSet<CardDraft>();

            foreach (YarnText.Piece piece in file.Pieces)
            {
                if (!piece.IsNode)
                {
                    written.Append(piece.Text);
                    continue;
                }

                // the card that was this node; else one of this title not otherwise placed (a card saved before, whose
                // draft from before that save is back by an undo)
                CardDraft card = cards.FirstOrDefault(c => c.Was == piece.Node.Title && c.Was.Length > 0 && !placed.Contains(c))
                              ?? cards.FirstOrDefault(c => c.Title == piece.Node.Title && !placed.Contains(c) &&
                                                           !cards.Any(o => o != c && o.Was == piece.Node.Title));

                if (card == null) continue;

                placed.Add(card);
                written.Append(Unchanged(card, newline) ? piece.Text : card.Write(newline));
            }

            foreach (CardDraft card in cards.Where(c => !placed.Contains(c)))
            {
                if (written.Length > 0 && !written.ToString().EndsWith(newline)) written.Append(newline);
                written.Append(card.Write(newline));
            }

            return written.ToString();
        }

        // a card whose text would come out as it was read
        static bool Unchanged(CardDraft card, string newline) => card.Read.Length > 0 && card.Write(newline) == card.Read;

        // --- the words ----------------------------------------------------------------------------------------------

        // EVERY LINE A CARD SPEAKS, its row set (dialogue.<speaker>.line.<campaign>.<id>), and the rows of lines the cards
        // had when read and don't now taken away. A Text card's lines are its own business, so its rows are never touched
        public static void Words(StoryDraft now, StoryDraft was, string campaign, LocaleSheet sheet)
        {
            Dictionary<string, string> keep = Keys(now, campaign);

            foreach (string gone in Keys(was, campaign).Keys.Where(k => !keep.ContainsKey(k)).ToList()) sheet.Remove(gone);

            foreach (KeyValuePair<string, string> line in keep) sheet.Set(line.Key, line.Value);
        }

        // A LINE'S WORDS AS THE LOCALE HOLDS THEM: Yarn hands each {expression} in a line to the table as a substitution,
        // numbered in order, so "Locked, {$hero_name}." is "Locked, {0}." in the row
        public static string Placeholders(string text)
        {
            int n = 0;
            return System.Text.RegularExpressions.Regex.Replace(text ?? "", @"(?<!\\)\{[^}]*\}", _ => "{" + n++ + "}");
        }

        // every line in the cards that aren't Text cards: its key, and its words
        public static Dictionary<string, string> Keys(StoryDraft story, string campaign)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (CardDraft card in story.Cards.Where(c => !c.IsText))
                foreach ((string id, string text, string speaker) in Blocks.Lines(card.Blocks))
                {
                    string who = speaker.Length > 0 ? speaker : card.Speaker;
                    if (who.Length == 0 || !ContentId.IsLocal(id)) continue;
                    keys[DialogueKeys.Line(who, campaign, id)] = Placeholders(text);
                }

            return keys;
        }
    }
}
