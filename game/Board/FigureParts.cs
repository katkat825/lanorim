using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Board
{
    // A FIGURE BUILT FROM PARTS (cc_task_h Part 2; Kathleen: "can we swap around head and bodies so that we can have
    // male and female of each mini?"). Every KayKit character is separate skinned parts - <Name>_Head, _Body, the arms
    // and legs, and its hat, helmet, cape or quiver - on the same 23-joint Rig_Medium, and Godot binds each part's
    // skin to its skeleton BY BONE NAME (every bind is named, none by index). So a head lifted out of one character
    // and put on another's Skeleton3D rides that skeleton, joint order and all, and every pose works unchanged. The
    // head keeps its own material and texture, so a skin tone may not match the neck's.
    //
    // No model file is made for a combination: the figure is put together each time the mini is built, from the
    // look's data (MiniLook.Head, MiniLook.Leave).
    public static class FigureParts
    {
        const string Folder = "res://models/minis/";

        // the part a head is, by the end of its name
        const string HeadPart = "Head";

        // leaves off the parts the look names and puts the borrowed head on; returns the parts it changed
        public static int Assemble(Node body, string head, IReadOnlyList<string> leave)
        {
            if (string.IsNullOrEmpty(head) && (leave == null || leave.Count == 0)) return 0;

            Skeleton3D skeleton = Nodes.AndUnder<Skeleton3D>(body).FirstOrDefault();

            if (skeleton == null) return 0;

            int changed = 0;

            var off = new List<string>(leave ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(head)) off.Add(HeadPart);

            foreach (MeshInstance3D part in Nodes.AndUnder<MeshInstance3D>(body).ToList())
            {
                if (!off.Any(p => Is(part, p))) continue;

                part.GetParent().RemoveChild(part);
                part.QueueFree();
                changed++;
            }

            if (string.IsNullOrEmpty(head)) return changed;

            MeshInstance3D borrowed = Borrow(head);

            if (borrowed == null)
            {
                GD.PushWarning($"mini: no head in '{head}' - the figure has none");
                return changed;
            }

            skeleton.AddChild(borrowed);
            borrowed.Skeleton = "..";

            return changed + 1;
        }

        // the head mesh out of another model, free of it
        static MeshInstance3D Borrow(string file)
        {
            string path = Folder + file;

            if (!ResourceLoader.Exists(path)) return null;

            Node donor = GD.Load<PackedScene>(path).Instantiate();
            MeshInstance3D head = Nodes.AndUnder<MeshInstance3D>(donor).FirstOrDefault(m => Is(m, HeadPart));

            head?.GetParent().RemoveChild(head);
            donor.Free();

            if (head != null) head.Transform = Transform3D.Identity;

            return head;
        }

        // by the end of its name: Knight_Head is a head, and "Hat" takes the Mage's hat and the Barbarian's bear hat alike
        static bool Is(Node part, string what) => part.Name.ToString().EndsWith(what, StringComparison.OrdinalIgnoreCase);
    }
}
