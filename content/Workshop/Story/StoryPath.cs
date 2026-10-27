using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Workshop.Story
{
    // WHERE A BLOCK IS IN ITS CARD: its index in the card's list, then - for a block inside another - which of that
    // block's lists (Block.Inside: an option's, a branch arm's, the else) and its index there, and so on. [3] is the
    // card's fourth block; [3, 1, 0] the first block under its second option. An edit names the block it changes by its
    // path, so it finds it again in the draft an undo puts back
    public static class StoryPath
    {
        // the list a path's block is in, and where; null when the path doesn't fit the card any more
        public static (List<Block> List, int At)? Find(List<Block> top, IReadOnlyList<int> path)
        {
            if (top == null || path == null || path.Count == 0 || path.Count % 2 == 0) return null;

            List<Block> list = top;

            for (int i = 0; i < path.Count - 1; i += 2)
            {
                if (path[i] < 0 || path[i] >= list.Count) return null;

                List<List<Block>> inside = list[path[i]].Inside.ToList();
                if (path[i + 1] < 0 || path[i + 1] >= inside.Count) return null;

                list = inside[path[i + 1]];
            }

            return (list, path[^1]);
        }

        public static Block At(List<Block> top, IReadOnlyList<int> path) =>
            Find(top, path) is { } found && found.At >= 0 && found.At < found.List.Count ? found.List[found.At] : null;

        // the list a path inside a block opens on: [3, 1] is the list under the fourth block's second option
        public static List<Block> ListAt(List<Block> top, IReadOnlyList<int> inside)
        {
            if (inside == null || inside.Count == 0) return top;

            Block holder = At(top, inside.Take(inside.Count - 1).ToList());
            List<List<Block>> lists = holder?.Inside.ToList();

            return lists != null && inside[^1] >= 0 && inside[^1] < lists.Count ? lists[inside[^1]] : null;
        }

        public static int[] Then(IReadOnlyList<int> path, params int[] more) => path.Concat(more).ToArray();

        public static string Name(IReadOnlyList<int> path) => string.Join("_", path);
    }

    // A CARD READ ALOUD (cc_task_o Part 5.1, Preview): its lines and notes in order, an option's blocks under it when it's
    // taken, and a command said as what the GM would do there. Not a play-test (that's Q): nothing is rolled
    public sealed record PreviewStep(string Speaker, string Text, IReadOnlyList<string> Options, Do Skipped)
    {
        public bool IsNote => Options != null;

        // the steps of a list, an option's blocks walked when `choose` says which (the first, unless told)
        public static IEnumerable<PreviewStep> Of(IEnumerable<Block> blocks, string speaker, Func<Choice, int> choose)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case Say say:
                        yield return new PreviewStep(say.Speaker.Length > 0 ? say.Speaker : speaker, say.Text, null, null);
                        break;

                    case Choice choice:
                        yield return new PreviewStep(speaker, "", choice.Options.Select(o => o.Text).ToList(), null);
                        int picked = Math.Clamp(choose(choice), 0, Math.Max(0, choice.Options.Count - 1));
                        if (choice.Options.Count > 0)
                            foreach (PreviewStep step in Of(choice.Options[picked].Blocks, speaker, choose)) yield return step;
                        break;

                    case Do act:
                        yield return new PreviewStep(speaker, "", null, act);
                        break;

                    case Branch branch when branch.Arms.Count > 0:
                        foreach (PreviewStep step in Of(branch.Arms[0].Blocks, speaker, choose)) yield return step;
                        break;
                }
            }
        }
    }
}
