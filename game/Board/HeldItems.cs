using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Board
{
    // WHAT A MINI HOLDS (task G 1.4): the class's weapon in its hand. Every KayKit character has a bone for it,
    // handslot.r and handslot.l, placed and turned so that a KayKit item (game/models/minis/kaykit/held/, from
    // the Adventurers pack's Assets) sits in the grip at the item's own origin. So an item is a child of a
    // BoneAttachment3D on that bone and nothing else: no offset, no turn, no scale.
    //
    // Put on AFTER the figure is measured: a staff over the head is not the figure's height, and PaintedModel's
    // height counts anything riding a bone as a hood
    public static class HeldItems
    {
        const string Folder = "res://models/minis/kaykit/held/";

        // an item named with its folder and file ("wings/angel_wings.tscn", the Fairy's) is under game/models/minis/;
        // a bare name is a KayKit item's .gltf
        static string PathOf(string item) =>
            item.Contains('/') ? "res://models/minis/" + item : Folder + item + ".gltf";

        // returns the items it put on, for painting
        public static List<Node3D> Put(Node model, IReadOnlyList<(string Bone, string Item)> holds)
        {
            var put = new List<Node3D>();

            if (holds == null || holds.Count == 0) return put;

            Skeleton3D skeleton = Nodes.AndUnder<Skeleton3D>(model).FirstOrDefault();

            if (skeleton == null) return put;

            foreach ((string bone, string item) in holds)
            {
                string path = PathOf(item);

                if (skeleton.FindBone(bone) < 0 || !ResourceLoader.Exists(path))
                {
                    GD.PushWarning($"mini: can't put '{item}' on '{bone}' - no such bone or no '{path}'");
                    continue;
                }

                var hand = new BoneAttachment3D { Name = item.GetFile().GetBaseName() + "_in_" + bone, BoneName = bone };
                skeleton.AddChild(hand);

                var held = GD.Load<PackedScene>(path).Instantiate<Node3D>();
                hand.AddChild(held);

                // the pose was written before the attachment existed (RigidParts does the same)
                hand.OnSkeletonUpdate();

                put.Add(held);
            }

            return put;
        }
    }
}
