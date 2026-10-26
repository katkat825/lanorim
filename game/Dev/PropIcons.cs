using System.IO;
using System.Linq;
using Content.Maps;
using Game.Board;
using Game.Builder;
using Godot;

namespace Game.Dev
{
    // THE PALETTE'S THUMBNAILS, RENDERED ONCE (cc_task_l Part 4.1): every prop in the palette, painted as the board paints
    // it, alone in a small transparent viewport from three-quarters above, saved to game/ui/prop_icons/<id>.png; and every
    // wall style's wall and door, to prop_icons/walls/<style>_wall.png and _door.png (cc_task_m3 Part 6, WallPalette). Run
    // it again after props.json changes; the builder only ever loads the pictures (PropPalette), never renders them live.
    //
    //   godot --path game res://launch.tscn -- --prop-icons          (not headless: it needs the renderer)
    //
    // The angle, the light and the size are a first pass, Kathleen's to change (#5): they're the constants below
    public partial class PropIcons : Node
    {
        const int Side = 128;

        // the camera's angle, in degrees: down from level, and round from straight on
        const float Pitch = 35f;
        const float Yaw = 35f;

        SubViewport _view;
        Camera3D _camera;
        Node3D _stand;
        Material _paint;

        int _at = -1;
        int _waited;
        int _made;

        // every picture to make: where it goes, and the model to stand for it
        System.Collections.Generic.List<(string Path, System.Func<Node3D> Make)> _jobs;
        string _path;

        public override void _Ready()
        {
            if (DisplayServer.GetName() == "headless")
            {
                GD.PrintErr("prop icons: headless has no renderer - run without --headless");
                GetTree().Quit(1);
                return;
            }

            _paint = ResourceLoader.Exists("res://shaders/painted.tres") ? GD.Load<Material>("res://shaders/painted.tres") : null;

            _view = new SubViewport
            {
                Size = new Vector2I(Side, Side),
                TransparentBg = true,
                OwnWorld3D = true,
                Msaa3D = Viewport.Msaa.Msaa4X,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            AddChild(_view);

            _view.AddChild(new WorldEnvironment
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.ClearColor,
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color(0.75f, 0.72f, 0.68f),
                    AmbientLightEnergy = 0.6f,
                },
            });

            var sun = new DirectionalLight3D { LightEnergy = 1.2f };
            _view.AddChild(sun);
            sun.RotationDegrees = new Vector3(-55f, 30f, 0f);

            _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true };
            _view.AddChild(_camera);

            Directory.CreateDirectory(ProjectSettings.GlobalizePath(PropPalette.IconFolder));
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(WallPalette.IconFolder));

            _jobs = PropCatalogue.Srd().Props
                                 .Select(p => (PropPalette.IconFolder + p.Id + ".png",
                                               (System.Func<Node3D>)(() => p.Figure ? Person(p) : PropModels.Make(p.Id))))
                                 .ToList();

            foreach (WallStyle style in System.Enum.GetValues<WallStyle>())
            {
                WallStyle one = style;
                _jobs.Add((WallPalette.Picture(one, Core.Space.Edge.Wall), () => Instance(one == WallStyle.Stone ? StoneWall : WallLooks.Wall(one))));

                if (one.HasDoor())
                    _jobs.Add((WallPalette.Picture(one, Core.Space.Edge.Door), () => Instance(one == WallStyle.Stone ? StoneDoor : WallLooks.Door(one))));
            }

            // only the pictures not made yet (cc_task_m3): remaking every one churns every committed picture for nothing.
            // Delete a picture to have it made again
            int had = _jobs.Count;
            _jobs.RemoveAll(j => File.Exists(ProjectSettings.GlobalizePath(j.Path)));
            GD.Print($"prop icons: {had - _jobs.Count} pictures already made, kept");
        }

        // the dungeon's own, which the board holds (board.tscn) rather than WallLooks
        static PackedScene StoneWall => GD.Load<PackedScene>("res://models/dungeon/wall.tscn");

        static PackedScene StoneDoor => GD.Load<PackedScene>("res://models/dungeon/doorway.tscn");

        static Node3D Instance(PackedScene scene) => scene?.Instantiate<Node3D>();

        const string SkinMeta = "skin";

        // a person (cc_task_m3 Part 10): the figure from game/models/minis/npcs/, held in a frame of its Idle as the board
        // stands it
        static Node3D Person(PropEntry entry)
        {
            string path = "res://models/minis/npcs/" + System.IO.Path.GetFileName(entry.Model);
            Node3D figure = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path).Instantiate<Node3D>() : null;

            if (figure?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is { } player &&
                player.GetAnimationList().FirstOrDefault(c => c.EndsWith("Idle")) is { } idle)
            {
                player.Play(idle);
                player.Seek(Game.Board.Mini.RestingFrame, true);
                player.Pause();
            }

            // the skin the board gives it (FigureSkins), put on after the paint (Stand)
            figure?.SetMeta(SkinMeta, FigureSkins.For(entry.Id));
            return figure;
        }

        public override void _Process(double delta)
        {
            if (_view == null) return;

            // a few frames for the model to be drawn before its picture is taken
            if (_stand != null && ++_waited < 4) return;

            if (_stand != null) Take();

            if (++_at >= _jobs.Count)
            {
                GD.Print($"prop icons: {_made} of {_jobs.Count} pictures in {PropPalette.IconFolder}");
                GetTree().Quit(0);
                return;
            }

            Stand(_jobs[_at].Path, _jobs[_at].Make);
        }

        // the model alone, its middle on the origin, a unit across, and the camera framing it
        void Stand(string path, System.Func<Node3D> make)
        {
            _stand?.QueueFree();
            _stand = null;
            _waited = 0;

            Node3D model = make();

            if (model == null)
            {
                GD.PushWarning($"prop icons: nothing to draw for {path}");
                return;
            }

            if (_paint != null) PaintedModel.Paint(model, _paint, "prop");
            if (model.HasMeta(SkinMeta)) PaintedModel.Recolour(model, "Skin", model.GetMeta(SkinMeta).AsColor());

            _path = path;
            _stand = new Node3D { Name = "Standing" };
            _stand.AddChild(model);
            _view.AddChild(_stand);

            Aabb bounds = PaintedModel.Bounds(model);
            float widest = Mathf.Max(bounds.Size.X, Mathf.Max(bounds.Size.Y, bounds.Size.Z));
            float scale = widest > 0f ? 1f / widest : 1f;

            model.Position = -(bounds.Position + bounds.Size * 0.5f);
            _stand.Scale = Vector3.One * scale;

            var look = Basis.FromEuler(new Vector3(Mathf.DegToRad(-Pitch), Mathf.DegToRad(Yaw), 0f));
            _camera.Transform = new Transform3D(look, look * new Vector3(0f, 0f, 4f));
            _camera.Size = 1.35f;
        }

        void Take()
        {
            Image picture = _view.GetTexture().GetImage();

            picture.SavePng(ProjectSettings.GlobalizePath(_path));
            _made++;

            _stand.QueueFree();
            _stand = null;
        }
    }
}
