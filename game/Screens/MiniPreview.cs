using Game.Board;
using Godot;

namespace Game.Screens
{
    // A MINI ON A TURNTABLE, IN A WINDOW OF ITS OWN (cc_task_m2 Part 1): the creation screen's own small viewport, with
    // the hero's mini standing on its base and turning slowly - the same Mini the board stands, painted the same way,
    // in a little world with one lamp and a camera of its own, so nothing on the table behind it is touched. Show()
    // stands another; null shows nothing. The look of it (size, lamp, turn) is Kathleen's to tune: the numbers are
    // the exports below.
    public partial class MiniPreview : SubViewportContainer
    {
        [Export] public float DegreesASecond { get; set; } = 30f;

        // how high the camera stands above the base, and how far back, in metres (a hero is 230 mm)
        [Export] public Vector3 CameraAt { get; set; } = new Vector3(0f, 0.2f, 0.42f);

        [Export] public Vector3 LookAt { get; set; } = new Vector3(0f, 0.11f, 0f);

        [Export] public float Fov { get; set; } = 40f;

        [Export] public float KindScale { get; set; } = 0.9f;

        [Export] public float KindDrop { get; set; } = 0.02f;

        const string MiniScene = "res://Board/mini.tscn";
        const string PaintPath = "res://shaders/painted.tres";

        readonly Vector2I _size;
        SubViewport _viewport;
        Node3D _turntable;
        MiniLook _showing;

        public MiniPreview(Vector2I size)
        {
            _size = size;
            Stretch = true;
            CustomMinimumSize = size;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public bool Turning { get; set; } = true;

        public override void _Ready()
        {
            _viewport = new SubViewport
            {
                Size = _size,
                OwnWorld3D = true,
                TransparentBg = true,
                Msaa3D = Viewport.Msaa.Msaa4X,
                RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
            };
            AddChild(_viewport);

            var stage = new Node3D { Name = "Stage" };
            _viewport.AddChild(stage);

            stage.AddChild(new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-45f, 30f, 0f),
                LightEnergy = 1.4f,
            });

            stage.AddChild(new WorldEnvironment
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.ClearColor,
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color(0.55f, 0.52f, 0.48f),
                    AmbientLightEnergy = 0.6f,
                },
            });

            var camera = new Camera3D { Fov = Fov, Current = true };
            stage.AddChild(camera);
            camera.Position = CameraAt;
            camera.LookAt(LookAt, Vector3.Up);

            _turntable = new Node3D { Name = "Turntable" };
            stage.AddChild(_turntable);

            Stand(_showing);
        }

        // the mini for this look; the same look again changes nothing, so a pick elsewhere doesn't restart the turn
        public void Show(MiniLook look)
        {
            if (look == _showing && _turntable?.GetChildCount() > 0) return;

            _showing = look;

            if (_turntable != null) Stand(look);
        }

        void Stand(MiniLook look)
        {
            foreach (Node old in _turntable.GetChildren()) old.QueueFree();

            if (look == null || MiniModels.Of(look) is not { } model) return;

            var mini = GD.Load<PackedScene>(MiniScene).Instantiate<Mini>();
            mini.FigureModel = model.Model;

            // a Beastfolk's own mini a tenth smaller and a little lower here, so its ears and hood stay in the window
            // (Kathleen's UI notes, 2026-10-27)
            bool kind = HeroMinis.OwnKind(look);
            mini.FigureHeight = look.Height * (kind ? KindScale : 1f);
            mini.Position = new Vector3(0f, kind ? -KindDrop : 0f, 0f);
            mini.Look = look;
            mini.Paint = ResourceLoader.Exists(PaintPath) ? GD.Load<Material>(PaintPath) : null;

            _turntable.AddChild(mini);
        }

        public override void _Process(double delta)
        {
            if (Turning && _turntable != null)
                _turntable.RotateY(Mathf.DegToRad(DegreesASecond) * (float)delta);
        }
    }
}
