namespace Game.Table
{
    // ONE COMPANION MODEL AND HOW IT IS SHOWN (task G 2), one line in CompanionModels:
    //
    //   File      the model, in game/models/companions/
    //   Clips     which clip plays which mood (CompanionClips.Flying, .Blob)
    //   Hover     metres between the table and its lowest point: a flyer hangs a little off the table. 0 stands on it
    //   Bob       how far it rises and falls while it hovers, in metres; 0 is still
    //   Recolour  its own copy of the atlas, some cells repainted (AtlasRecolour): "col,row #rrggbb; ...". The
    //             cells are the atlas's 32 x 32 grid, one row a monster; null wears the pack's colours
    public sealed record CompanionLook(string File, CompanionClips Clips, float Hover = 0f, float Bob = 0f,
                                       string Recolour = null);
}
