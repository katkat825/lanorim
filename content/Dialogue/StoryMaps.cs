using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    // A <<map>> NAMING A MAP THE PACK DOESN'T HAVE (cc_task_tf Part 1.3): a campaign check problem with its file, node
    // and line, read from the story the way VerbCheck reads it. The story's own check (DialogueBook) doesn't know the
    // pack's maps, so the campaign check (WorkshopProblems) asks this beside it
    public static class StoryMaps
    {
        public static IEnumerable<ContentProblem> Problems(Package pack)
        {
            string dialogue = Path.Combine(pack?.Folder ?? "", Package.DialogueFolder);

            if (pack == null || !Directory.Exists(dialogue)) yield break;

            foreach (string path in Directory.EnumerateFiles(dialogue, "*" + DialogueBook.Extension).OrderBy(p => p, StringComparer.Ordinal))
            {
                string file = Package.DialogueFolder + "/" + Path.GetFileName(path);

                foreach (VerbCheck.Found found in VerbCheck.Requests(file, File.ReadAllText(path))
                                                           .Where(f => f.Request.Kind == RequestKind.Map && !pack.Maps.ContainsKey(f.Request.Id)))
                    yield return new ContentProblem(file, found.Node,
                        $"<<map {found.Request.Id}>> - there is no {Package.MapsFolder}/{found.Request.Id}{Package.MapExtension} in this campaign",
                        found.Line);
            }
        }
    }
}
