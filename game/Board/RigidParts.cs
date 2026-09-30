using System;
using System.Linq;
using Godot;

namespace Game.Board
{
    // PIECES OF A RIGGED FIGURE THAT AREN'T SKINNED, PUT ON THE BONE THEY BELONG TO.
    //
    // rogue_v2's hood and cloak are meshes of their own hanging off the model's root, not off a bone
    // (board.tscn says so). Mini.Pose stands the body in a frame of its idle, which tilts the head and
    // settles the shoulders, and the hood and cloak stayed where the T-pose had left them - the hood
    // floating over a head that had moved out from under it, which read as hunched and top-heavy
    // (cc_task_table-ui-minis-zoom-damage.md 4). Each piece now rides a BoneAttachment3D on its bone,
    // keeping its offset from that bone's REST pose, so it follows the pose the way a skinned piece would.
    //
    // Which bone: by the piece's name first (a hood or a hat on the head, a cloak or cape on the torso),
    // and otherwise the bone whose rest position is nearest the piece. The better fix is in Blender -
    // weight the hood to Head and the cloak to Torso - and then there is nothing here to do.
    public static class RigidParts
    {
        static readonly (string Piece, string[] Bones)[] ByName =
        {
            ("hood", new[] { "Head" }),
            ("hat", new[] { "Head" }),
            ("helm", new[] { "Head" }),
            ("cloak", new[] { "Torso", "Chest", "Spine", "Neck" }),
            ("cape", new[] { "Torso", "Chest", "Spine", "Neck" }),
            ("cowl", new[] { "Neck", "Head" }),
        };

        // returns how many pieces were put on a bone
        public static int Attach(Node model)
        {
            Skeleton3D skeleton = Nodes.AndUnder<Skeleton3D>(model).FirstOrDefault();

            if (skeleton == null || skeleton.GetBoneCount() == 0 || model is not Node3D root) return 0;

            var loose = Nodes.AndUnder<MeshInstance3D>(model)
                             .Where(m => m.Skin == null && !skeleton.IsAncestorOf(m) && m.Mesh != null)
                             .ToList();

            foreach (MeshInstance3D piece in loose)
            {
                int bone = BoneFor(skeleton, piece, root);

                if (bone < 0) continue;

                // the piece's place, and the bone's rest, both in the model's space
                Transform3D pieceAt = PaintedModel.Relative(root, piece);
                Transform3D restAt = PaintedModel.Relative(root, skeleton) * skeleton.GetBoneGlobalRest(bone);

                var attachment = new BoneAttachment3D { Name = piece.Name + "_on_" + skeleton.GetBoneName(bone), BoneName = skeleton.GetBoneName(bone) };
                skeleton.AddChild(attachment);

                piece.GetParent().RemoveChild(piece);
                attachment.AddChild(piece);
                piece.Transform = restAt.AffineInverse() * pieceAt;

                // the pose was written before the attachment existed; take it now, not a frame late
                attachment.OnSkeletonUpdate();

                GD.Print($"mini    {root.Name} '{piece.Name}' rides on '{attachment.BoneName}'");
            }

            return loose.Count;
        }

        static int BoneFor(Skeleton3D skeleton, MeshInstance3D piece, Node3D root)
        {
            string name = piece.Name.ToString();

            foreach ((string part, string[] bones) in ByName)
            {
                if (!name.Contains(part, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (string bone in bones)
                {
                    int found = skeleton.FindBone(bone);
                    if (found >= 0) return found;
                }
            }

            // nearest by rest position to the middle of the piece
            Vector3 middle = (PaintedModel.Relative(root, piece) * piece.GetAabb()).GetCenter();
            Transform3D skeletonAt = PaintedModel.Relative(root, skeleton);

            int nearest = -1;
            float best = float.MaxValue;

            for (int bone = 0; bone < skeleton.GetBoneCount(); bone++)
            {
                float far = (skeletonAt * skeleton.GetBoneGlobalRest(bone)).Origin.DistanceSquaredTo(middle);

                if (far < best) { best = far; nearest = bone; }
            }

            return nearest;
        }
    }
}
