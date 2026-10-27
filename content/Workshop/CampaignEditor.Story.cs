using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Campaigns;
using Content.Workshop.Story;

namespace Content.Workshop
{
    // THE STORY EDITOR'S OPERATIONS (cc_task_o): cards - new, renamed (every go-to to it follows), duplicated, deleted,
    // moved and coloured on the board - and the blocks inside them - added from the palette, moved, duplicated (its lines
    // given new ids), deleted. Each one undo step
    public sealed partial class CampaignEditor
    {
        StoryDraft Story => Draft.Story;

        // A NEW CARD, its title made from its name; placed on the board where asked. Its title is returned
        public string NewCard(string name, float x, float y, string file = StoryDraft.StoryFile)
        {
            string title = ContentId.Free(ContentId.From(name, "card"), t => Story.Card(t) != null);

            Edit("card", d =>
            {
                d.Story.Cards.Add(CardDraft.New(title, file));
                CardLook look = d.Story.Look(title);
                look.X = x;
                look.Y = y;
                look.Placed = true;
                if (!string.IsNullOrWhiteSpace(name) && name.Trim() != Content.Workshop.Tables.TablesDraft.Plain(title)) look.Name = name.Trim();
            });

            return title;
        }

        // the card a chapter starts at (the node named for its id: CampaignRun starts a chapter there), made
        public string MakeChapterStart(string chapter, string firstLine, float x, float y)
        {
            if (Story.Card(chapter) != null) return chapter;

            Edit("card", d =>
            {
                CardDraft card = CardDraft.New(chapter);
                card.Blocks.Add(new Say { Text = firstLine, LineId = d.Story.NewLineId(chapter) });
                d.Story.Cards.Add(card);
                CardLook look = d.Story.Look(chapter);
                look.X = x;
                look.Y = y;
                look.Placed = true;
            });

            return chapter;
        }

        // A CARD RENAMED: its name on the board, and its title made from it - every go-to to it in every card (a Text
        // card's too) follows. A chapter's starting card keeps its title, which the chapter's id is
        public bool RenameCard(string title, string name) => Edit("card", d =>
        {
            CardDraft card = d.Story.Card(title);
            if (card == null) return;

            d.Story.Look(title).Name = name?.Trim() ?? "";

            if (d.Chapter(title) != null) return;

            string to = ContentId.Free(ContentId.From(name, title), t => t != title && d.Story.Card(t) != null);
            if (to == title) return;

            Retitle(d.Story, title, to);
        });

        static void Retitle(StoryDraft story, string from, string to)
        {
            story.Card(from).Retitle(to);

            foreach (CardDraft card in story.Cards)
            {
                if (card.IsText)
                {
                    card.Text = Regex.Replace(card.Text, $@"<<jump\s+{Regex.Escape(from)}\s*>>", $"<<jump {to}>>");
                    continue;
                }

                foreach (Jump jump in Blocks.All(card.Blocks).OfType<Jump>().Where(j => j.Target == from)) jump.Target = to;
            }

            if (story.Looks.Remove(from, out CardLook look)) story.Looks[to] = look;
        }

        public bool MoveCard(string title, float x, float y) => Edit("card.move", d =>
        {
            CardLook look = d.Story.Look(title);
            look.X = x;
            look.Y = y;
            look.Placed = true;
        });

        public bool ColourCard(string title, string colour) => Edit("card", d => d.Story.Look(title).Colour = colour ?? "");

        public string DuplicateCard(string title)
        {
            CardDraft from = Story.Card(title);
            if (from == null) return null;

            string copy = ContentId.Free(title + "_copy", t => Story.Card(t) != null);

            Edit("card", d =>
            {
                CardDraft card = d.Story.Card(title).Copy();
                card.Was = "";
                card.Read = "";
                card.Retitle(copy);
                d.Story.Cards.Add(card);
                if (!card.IsText) Renumber(d.Story, card.Blocks, copy);

                CardLook was = d.Story.Look(title), look = d.Story.Look(copy);
                look.X = was.X + 40;
                look.Y = was.Y + 40;
                look.Placed = true;
                look.Colour = was.Colour;
            });

            return copy;
        }

        public bool DeleteCard(string title) => Edit("card", d =>
        {
            d.Story.Cards.RemoveAll(c => c.Title == title);
            d.Story.Looks.Remove(title);
        });

        // --- blocks -------------------------------------------------------------------------------------------------

        // a block from the palette into a card's list (`inside` is the list's path: [] the card's own), at `at` (-1 the end).
        // Its lines get ids now, made from the card's name. The new block's path is returned
        public int[] AddBlock(string title, int[] inside, int at, Func<CampaignDraft, CardDraft, Block> make)
        {
            int[] made = null;

            Edit("block", d =>
            {
                CardDraft card = d.Story.Card(title);
                List<Block> list = card?.Blocks == null ? null : StoryPath.ListAt(card.Blocks, inside);
                if (list == null) return;

                int where = at < 0 || at > list.Count ? list.Count : at;
                list.Insert(where, make(d, card));
                made = StoryPath.Then(inside, where);
            });

            return made;
        }

        // a change to one block: the card's title and the block's path find it in whichever draft is the page's now
        public bool EditBlock<T>(string title, IReadOnlyList<int> path, Action<T> change, bool typing = false) where T : Block =>
            Edit("block." + title + "." + StoryPath.Name(path), d =>
            {
                if (d.Story.Card(title)?.Blocks is { } blocks && StoryPath.At(blocks, path) is T block) change(block);
            }, typing);

        public bool MoveBlock(string title, IReadOnlyList<int> path, int by) => Edit("block", d =>
        {
            if (d.Story.Card(title)?.Blocks is not { } blocks || StoryPath.Find(blocks, path) is not { } found) return;

            int to = found.At + by;
            if (found.At < 0 || found.At >= found.List.Count || to < 0 || to >= found.List.Count) return;

            (found.List[found.At], found.List[to]) = (found.List[to], found.List[found.At]);
        });

        public bool DeleteBlock(string title, IReadOnlyList<int> path) => Edit("block", d =>
        {
            if (d.Story.Card(title)?.Blocks is { } blocks && StoryPath.Find(blocks, path) is { } found && found.At >= 0 && found.At < found.List.Count)
                found.List.RemoveAt(found.At);
        });

        // A COPY BELOW IT, its lines given new ids (an id is said once in a campaign)
        public bool DuplicateBlock(string title, IReadOnlyList<int> path) => Edit("block", d =>
        {
            if (d.Story.Card(title)?.Blocks is not { } blocks || StoryPath.Find(blocks, path) is not { } found) return;
            if (found.At < 0 || found.At >= found.List.Count) return;

            var copy = new List<Block> { Copy(found.List[found.At]) };
            found.List.Insert(found.At + 1, copy[0]);
            Renumber(d.Story, copy, title);
        });

        static Block Copy(Block block) =>
            System.Text.Json.JsonSerializer.Deserialize<Block>(System.Text.Json.JsonSerializer.Serialize(block));

        // every line in these blocks (already in the story) given a new id, made from the card's name: each cleared
        // first, so a fresh one is never one of the copy's own
        static void Renumber(StoryDraft story, List<Block> blocks, string card)
        {
            var says = Blocks.All(blocks).OfType<Say>().Where(s => s.LineId.Length > 0).ToList();
            var options = Blocks.All(blocks).OfType<Choice>().SelectMany(c => c.Options).Where(o => o.LineId.Length > 0).ToList();

            foreach (Say say in says) say.LineId = "";
            foreach (Option option in options) option.LineId = "";

            foreach (Say say in says) say.LineId = story.NewLineId(card);
            foreach (Option option in options) option.LineId = story.NewLineId(card);
        }

        // a Text card's raw node, typed in Advanced
        public bool EditText(string title, string text) => Edit("card.text", d =>
        {
            if (d.Story.Card(title) is { IsText: true } card) card.Text = text ?? "";
        }, typing: true);
    }
}
