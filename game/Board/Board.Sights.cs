using System.Collections.Generic;
using Core.Space;
using Godot;

namespace Game.Board
{
    public partial class Board
    {
        // --- what a narrated spell shows (cc_task_m3 Part 2) -----------------------------------------------------------

        // most spells the narrator tells are words only; these few are seen too, on the square they were aimed at:
        // Druidcraft's bloom is a clump of flowers from the props (stood as a map's prop is), there a few seconds and gone;
        // Prestidigitation's spark is the square's pulse
        static readonly Dictionary<string, string> Blooms = new()
        {
            ["druidcraft"] = "flowers_bright",
        };

        static readonly HashSet<string> Sparks = new() { "prestidigitation" };

        // how long a bloom stands, and how long it takes to grow and to wither
        const float BloomSeconds = 4f, BloomGrow = 0.4f;

        public bool Sight(string spellId, Cell at)
        {
            if (Sparks.Contains(spellId))
            {
                Pulse(at);
                return true;
            }

            if (!Blooms.TryGetValue(spellId, out string prop)) return false;

            Node3D bloom = Stand(new Content.Maps.Prop(prop, at));
            if (bloom == null) return false;

            bloom.Name = "Sight_" + spellId;
            AddChild(bloom);

            Vector3 full = bloom.Scale;
            bloom.Scale = full * 0.01f;

            Tween tween = bloom.CreateTween();
            tween.TweenProperty(bloom, "scale", full, BloomGrow).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenInterval(BloomSeconds);
            tween.TweenProperty(bloom, "scale", full * 0.01f, BloomGrow).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(bloom.QueueFree));

            return true;
        }

        // a bloom still standing (a test or a probe)
        public IEnumerable<Node3D> Sights()
        {
            foreach (Node child in GetChildren())
                if (child is Node3D sight && sight.Name.ToString().StartsWith("Sight_")) yield return sight;
        }
    }
}
