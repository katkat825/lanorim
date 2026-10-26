using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Godot;

namespace Game.Board
{
    public partial class Board
    {
        // --- the map's people (cc_task_m3 Part 10) -----------------------------------------------------------------------

        // where the figures' models are: the Ultimate Animated Character Pack's, pulled into game/models/minis/npcs/
        const string FigureFolder = "npcs/";

        // a person stands as tall as the bandit (MiniModels)
        const float FigureTall = 0.1875f;

        // AN NPC ON THE MAP: a mini like any other (painted, in a frame of its Idle, on its base), on its square, facing the
        // way it's turned. Not an actor: no fight knows it, it's walked round as a blocking prop is and seen past
        Node3D Figure(Prop prop, PropEntry entry)
        {
            if (MiniScene == null) return null;

            var look = new MiniLook(FigureFolder + System.IO.Path.GetFileName(entry.Model),
                                    FigureTall, "Idle", Skin: FigureSkins.For(prop.Id));

            if (MiniModels.Of(look) is not { } figure) return null;

            var mini = MiniScene.Instantiate<Mini>();
            mini.Name = $"{prop.Id}_{prop.Cell.X}_{prop.Cell.Y}";
            mini.Paint = Paint;
            mini.CellSize = Metrics.CellSize;
            mini.FigureModel = figure.Model;
            mini.FigureHeight = figure.Look.Height;
            mini.Look = figure.Look;
            mini.Position = Metrics.Centre(prop.Cell);
            mini.Rotation = new Vector3(0f, Mathf.Pi * 0.5f * prop.Turn, 0f);

            return mini;
        }

        // the figure standing on a square, if any (the builder's hover)
        public Prop FigureAt(Core.Space.Cell cell, IEnumerable<Prop> props) =>
            props?.FirstOrDefault(p => p.Cell == cell && PropCatalogue.Srd().IsFigure(p.Id));
    }
}
