using Game.Audio;
using Godot;

namespace Game.Tray
{
    public partial class DiceTray
    {
        // --- the skin ------------------------------------------------------------------------

        public string SkinName =>
            Skin?.ResourcePath is { Length: > 0 } path ? path.GetFile().GetBaseName() : "none";

        // idempotent; physics and look come off one TraySurface, so a felt look with plank bounce
        // is impossible by construction
        void ApplySkin()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            if (_floorBody == null || _wallsBody == null)
            {
                GD.PushError($"dice tray: the tray needs two StaticBody3D at '{TrayFloorPath}' and " +
                             $"'{TrayWallsPath}' - physics materials belong to bodies, not shapes");
                return;
            }

            if (Skin == null)
            {
                GD.PushError("dice tray: no skin - the tray will be untextured and will bounce " +
                             "like Godot's default");
                return;
            }

            Dress(_floorBody, Skin.Floor, "floor");
            Dress(_wallsBody, Skin.Walls, "walls");

            // one voice for the whole tray: it holds nothing per-die
            var voice = new SurfaceVoice(Skin);

            foreach (DieAudio audio in _voices) if (audio != null) audio.Voice = voice;
        }

        static void Dress(StaticBody3D body, TraySurface surface, string which)
        {
            if (surface == null)
            {
                GD.PushError($"dice tray: the skin has no {which} surface");
                return;
            }

            body.PhysicsMaterialOverride = surface.Physics;

            foreach (MeshInstance3D mesh in Nodes.Under<MeshInstance3D>(body)) mesh.MaterialOverride = surface.Material;
        }

        // a felt chosen off the table. materials are cosmetic and always will be
        // (ART_DIRECTION section 6): no tray and no die grants a bonus
        public bool Wear(string name)
        {
            TraySkin wearing = TraySkin.Load(name);

            if (wearing == null)
            {
                GD.PushWarning($"dice tray: there is no skin called '{name}' in {TraySkin.Folder} " +
                               $"- the tray keeps {SkinName}");
                return false;
            }

            Skin = wearing;
            ApplySkin();

            GD.Print($"tray    {SkinName}");

            // every skin needs its own fairness sweep: bounce decides how a die settles
            return true;
        }
    }
}
