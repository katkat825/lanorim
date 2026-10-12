using System.IO;
using System.Linq;
using Content.Maps;
using Game.Board;
using Game.Builder;
using Godot;

namespace Game.Dev
{
    // THE PALETTE'S THUMBNAILS, RENDERED ONCE (cc_task_l Part 4.1): every prop in the palette, painted as the board paints
    // it, alone in a small transparent viewport from three-quarters above, saved to game/ui/prop_icons/<id>.png. Run it
    // again after props.json changes; the builder only ever loads the pictures (PropPalette), never renders them live.
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
        }

        public override void _Process(double delta)
        {
            if (_view == null) return;

            // a few frames for the model to be drawn before its picture is taken
            if (_stand != null && ++_waited < 4) return;

            if (_stand != null) Take();

            var props = PropCatalogue.Srd().Props;

            if (++_at >= props.Count)
            {
                GD.Print($"prop icons: {_made} of {props.Count} pictures in {PropPalette.IconFolder}");
                GetTree().Quit(0);
                return;
            }

            Stand(props[_at]);
        }

        // the prop alone, its middle on the origin, a unit across, and the camera framing it
        void Stand(PropEntry prop)
        {
            _stand?.QueueFree();
            _stand = null;
            _waited = 0;

            Node3D model = PropModels.Make(prop.Id);

            if (model == null)
            {
                GD.PushWarning($"prop icons: {prop.Id} has no model pulled");
                return;
            }

            if (_paint != null) PaintedModel.Paint(model, _paint, "prop");

            _stand = new Node3D { Name = prop.Id };
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
            string id = _stand.Name;
            Image picture = _view.GetTexture().GetImage();

            picture.SavePng(ProjectSettings.GlobalizePath(PropPalette.IconFolder + id + ".png"));
            _made++;

            _stand.QueueFree();
            _stand = null;
        }
    }
}
