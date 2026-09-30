using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game
{
    // THE TREE WALK: everything under a node, however deep, each child before its own children. the
    // tray's meshes and walls, the GM screen's meshes, every label, every tray to caption, a door's
    // leaf, a model's skeleton and the first thing to focus each walked it for themselves
    // (cc_task_dedupe-methods.md #11)
    public static class Nodes
    {
        public static IEnumerable<Node> Under(Node node)
        {
            if (node == null) yield break;

            foreach (Node child in node.GetChildren())
            {
                yield return child;

                foreach (Node deeper in Under(child)) yield return deeper;
            }
        }

        public static IEnumerable<T> Under<T>(Node node) where T : class => Under(node).OfType<T>();

        // the node itself first, then everything under it
        public static IEnumerable<T> AndUnder<T>(Node node) where T : class =>
            (node == null ? Enumerable.Empty<Node>() : new[] { node }.Concat(Under(node))).OfType<T>();

        // n frames of the tree going by, for a probe that has to let Godot lay out or settle first
        public static async System.Threading.Tasks.Task Frames(this Node node, int n)
        {
            for (int i = 0; i < n; i++) await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
