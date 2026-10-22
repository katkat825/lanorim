using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Campaigns;
using Content.Maps;

namespace Content.Workshop
{
    // THE CHAPTERS AND MAPS TAB (cc_task_m 4.2): the chapters in order (add, rename, reorder, delete), each one's maps
    // (add from My maps, a new one, reorder, remove), and which chapter starts the campaign. Each one undo step
    public sealed partial class CampaignEditor
    {
        // where the campaign's maps live: its maps/ folder, the one the builder saves into
        public MapPlace Maps => MapPlace.OfCampaign(Folder, CampaignFiles.NameKey(Id));

        // a chapter, its id made from its title (and the next free one if that's taken); its id is returned
        public string AddChapter(string title)
        {
            string id = ContentId.Free(ContentId.From(title, "chapter"), taken => Draft.Chapter(taken) != null);

            Edit("chapter", d => d.Chapters.Add(new ChapterDraft { Id = id, Title = title ?? "" }));
            return id;
        }

        // a new title: the chapter keeps its id, since the story's node and the start are spelled with it
        public bool RenameChapter(string id, string title) =>
            Edit("chapter", d => { if (d.Chapter(id) is { } chapter) chapter.Title = title ?? ""; });

        // dragged from one place in the list to another
        public bool MoveChapter(int from, int to) => Edit("chapter", d => Move(d.Chapters, from, to));

        public bool DeleteChapter(string id) =>
            Edit("chapter", d =>
            {
                d.Chapters.RemoveAll(c => c.Id == id);
                if (d.Start == id) d.Start = "";
            });

        public bool StartAt(string id) => Edit("start", d => d.Start = d.Chapter(id) != null ? id : d.Start);

        // DOES THE STORY USE THIS CHAPTER? A node titled with its id (where the chapter starts playing) or a jump to it:
        // deleting it then asks first
        public bool StoryUses(string chapter)
        {
            string dialogue = Path.Combine(Folder, Package.DialogueFolder);

            if (!Directory.Exists(dialogue)) return false;

            var uses = new Regex($@"^\s*title:\s*{Regex.Escape(chapter)}\s*$|<<\s*jump\s+{Regex.Escape(chapter)}\s*>>", RegexOptions.Multiline);

            return Directory.EnumerateFiles(dialogue, "*" + Dialogue.DialogueBook.Extension)
                            .Any(file => uses.IsMatch(File.ReadAllText(file)));
        }

        // --- a chapter's maps ---------------------------------------------------------------------------------------

        public bool AddMap(string chapter, string map) =>
            Edit("map", d => d.Chapter(chapter)?.Maps.Add(map));

        public bool MoveMap(string chapter, int from, int to) =>
            Edit("map", d => { if (d.Chapter(chapter) is { } c) Move(c.Maps, from, to); });

        // off the chapter; the map's file stays in maps/
        public bool RemoveMap(string chapter, int at) =>
            Edit("map", d => { if (d.Chapter(chapter) is { } c && at >= 0 && at < c.Maps.Count) c.Maps.RemoveAt(at); });

        // ONE OF MY MAPS, COPIED IN: under its own id, or the next free one if the campaign has a map called that; the
        // id it's in the campaign under, or null and why not
        public string CopyIn(MapPlace library, string map, out string problem)
        {
            problem = null;

            if (!MapFiles.Read(library, map, out MapDraft draft, out problem)) return null;

            string id = MapFiles.FreeId(Maps, map);
            problem = MapFiles.Write(Maps, id, draft, replace: false);

            return problem == null ? id : null;
        }

        // A NEW MAP, made from a template at its own size and written into maps/ straight away, so the chapter that names
        // it plays whether or not the builder ever saves it; the id it's under, or null and why not
        public string MakeMap(string name, string template, out string problem)
        {
            string id = MapName.IdIn(Maps, name);
            MapDraft drawn = MapTemplates.Read(template) ?? MapTemplates.Read(MapTemplates.Standard);
            MapDraft made = MapTemplates.Make(template, drawn.Columns, drawn.Rows);

            // the name as typed, the id made from it (cc_task_m2 Part 7)
            made.Name = name?.Trim() ?? "";

            problem = MapFiles.Write(Maps, id, made, replace: false);

            return problem == null ? id : null;
        }

        static void Move<T>(System.Collections.Generic.List<T> list, int from, int to)
        {
            if (from < 0 || from >= list.Count || to < 0 || to >= list.Count || from == to) return;

            T one = list[from];
            list.RemoveAt(from);
            list.Insert(to, one);
        }
    }
}
