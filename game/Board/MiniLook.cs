using System.Collections.Generic;

namespace Game.Board
{
    // WHAT A MINI LOOKS LIKE, as one line of data (MiniModels for a statblock's mini, HeroMinis for a class's).
    //
    //   File    the model, under game/models/minis/
    //   Height  how tall it stands on the board, in metres
    //   Pose    the clip it is held in, one frame of it (Mini.Pose). null: the model's own idle, as every mini
    //           before task G. A clip a rig library brings is found by its short name: "Melee_2H_Idle"
    //   At      seconds into that clip. A pose read mid-motion can look odd, so it is data too
    //   Rig     the clip libraries to give a model that carries none of its own (MiniRigs): the KayKit
    //           characters are bare, and their clips come in the pack's separate Rig_Medium files
    //   Holds   what is in its hands, bone and item (HeldItems): "handslot.r", "sword_1handed"
    //   Crown   measured crown to sole rather than to its top bone (PaintedModel.PosedHeight). A KayKit head
    //           stands as far again above its head bone as the neck is long, where a Quaternius head barely does
    //   Head    another model whose head this body wears instead of its own (FigureParts, task H): "kaykit/Mage.glb".
    //           null keeps its own. Only the head comes across, never its hat
    //   Leave   the parts left off, by the end of their name: "Hat", "Helmet", "HelmetVisor", "Cape"
    //   Skin    the colour its surface called "Skin" is painted instead of its own (the map's people, FigureSkins), and
    //           on a KayKit body its atlas's skin swatch (PaintedModel.RecolourSwatch). null keeps the pack's
    //   Hair    the same for its hair: the surface called "Hair", or the KayKit atlas's hair swatch (the hero's pick)
    public sealed record MiniLook(string File, float Height, string Pose = null, float At = Mini.RestingFrame,
                                  string Rig = null, IReadOnlyList<(string Bone, string Item)> Holds = null,
                                  bool Crown = false, string Head = null, IReadOnlyList<string> Leave = null,
                                  Godot.Color? Skin = null, Godot.Color? Hair = null);
}
