using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Maps;
using Content.Schema;
using Core.Space;

namespace Content.Campaigns
{
    public sealed partial class Package
    {
        static IReadOnlyDictionary<string, MapLayout> ReadMaps(string folder,
                                                               List<ContentProblem> problems,
                                                               Dictionary<string, IReadOnlyList<Prop>> props = null)
        {
            var maps = new Dictionary<string, MapLayout>(StringComparer.Ordinal);

            foreach ((string file, string each, string text) in
                     PackFolder.Read(Path.Combine(folder, MapsFolder), MapExtension, MapsFolder, problems))
            {
                string id = Path.GetFileNameWithoutExtension(each);

                if (!ContentId.IsLocal(id))
                {
                    problems.Add(new ContentProblem(file, "",
                        $"'{id}' is not a map id - a map is named by its file, so the file name is " +
                        "lowercase a-z, 0-9 and underscore"));
                    continue;
                }

                if (!MapDraft.TryRead(text, out MapDraft draft, out string problem))
                {
                    problems.Add(new ContentProblem(file, "", problem));
                    continue;
                }

                if (!draft.Sound)
                {
                    foreach (string one in draft.Problems())
                        problems.Add(new ContentProblem(file, "", one));

                    continue;
                }

                if (maps.ContainsKey(id))
                {
                    problems.Add(new ContentProblem(file, "", $"there are two maps called '{id}'"));
                    continue;
                }

                // a prop the palette doesn't have is drawn as a placeholder - worth a word, not a refusal
                foreach (string unknown in new MapEditor(draft).UnknownProps)
                    problems.Add(ContentProblem.Caution(file, "props",
                        $"'{unknown}' is not in the map builder's palette, so the table draws a placeholder"));

                maps[id] = draft.Layout();

                if (props != null) props[id] = draft.Props.ToList();
            }

            return maps;
        }

        // a chapter that names a map nobody shipped is a campaign that stops mid-play, so it is
        // caught at load rather than at the moment the player walks into it
        static void ChaptersNameRealMaps(Manifest manifest,
                                         IReadOnlyDictionary<string, MapLayout> maps,
                                         List<ContentProblem> problems)
        {
            foreach (Chapter chapter in manifest.Chapters)
                foreach (string map in chapter.Maps)
                    if (!maps.ContainsKey(map))
                        problems.Add(new ContentProblem(
                            ManifestReader.PackFileName, $"chapters.{chapter.Id}",
                            $"chapter '{chapter.Id}' is played on '{map}' and there is no " +
                            $"{MapsFolder}/{map}{MapExtension} in this campaign"));
        }
    }
}
