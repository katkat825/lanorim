namespace Game.Table
{
    // WHICH CLIP PLAYS WHICH MOOD, for one kind of model (task G 2.2). They were CompanionFigure's exports, written
    // for the Quaternius animals (gone since task H); each companion model now names its set (CompanionLook), so a
    // new model is one entry and never a code branch. Every one is a list and the first name the model has wins.
    // Idles are dealt in turn (Idling), none twice.
    //
    // A clip for a moment (alert, pleased, speaking, the huff) plays once and the idle follows it. The lie-down
    // loops for a model with a lying or head-low clip (LieLoops); for one with nothing to lie in, it plays once
    // (No) and holds the idle. There's no lying-down clip in the Ultimate Monsters pack.
    public sealed record CompanionClips(
        string[] Idles, string[] Walk, string[] Settle, string[] Watching, string[] Alert, string[] Pleased,
        string[] Lie, string[] Speak, string[] Huff, bool LieLoops)
    {
        // Ultimate Monsters' Flying set (the dragon, the pigeon, the Squidle, the ghost): eight clips
        // between them. Roaming drifts in Flying_Idle - Fast_Flying at a walking pace beats its wings like a panic
        public static readonly CompanionClips Flying = new(
            Idles: new[] { "Flying_Idle" },
            Walk: new[] { "Flying_Idle" },
            Settle: new[] { "Flying_Idle" },
            Watching: new[] { "Flying_Idle" },
            Alert: new[] { "HitReact" },
            Pleased: new[] { "Yes" },
            Lie: new[] { "No" },
            Speak: new[] { "Yes" },
            Huff: new[] { "Headbutt", "Punch" },
            LieLoops: false);

        // Ultimate Monsters' Blob set (Mushnub_Evolved). "HitRecieve" is the pack's spelling, and the name it has
        public static readonly CompanionClips Blob = new(
            Idles: new[] { "Idle" },
            Walk: new[] { "Walk" },
            Settle: new[] { "Idle" },
            Watching: new[] { "Idle" },
            Alert: new[] { "HitRecieve" },
            Pleased: new[] { "Yes", "Dance", "Jump" },
            Lie: new[] { "No" },
            Speak: new[] { "Yes" },
            Huff: new[] { "Bite_Front" },
            LieLoops: false);
    }
}
