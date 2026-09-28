using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Audio
{
    // the folder is the list: a hard-coded list would drift and fail as silence, which looks like a die that never hit anything
    // cached by path: a pool is immutable, so asking twice doesn't re-read the disk
    public sealed class ImpactPool
    {
        public const string Default = "res://audio/samples/impacts/wood/";   // what a surface gets if it names none

        static readonly Dictionary<string, ImpactPool> Loaded = new();

        // lazy, not static-initialised: GD.Load in a static constructor can run before the resource system is up
        public static ImpactPool For(string folder)
        {
            folder = Normalize(folder);

            if (Loaded.TryGetValue(folder, out ImpactPool pool)) return pool;

            pool = new ImpactPool(folder);
            Loaded[folder] = pool;
            return pool;
        }

        static string Normalize(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) folder = Default;

            return folder.EndsWith("/") ? folder : folder + "/";
        }

        // does this folder have anything to play, asked without building a pool or pushing an error
        // For() treats an empty folder as an error, but one not recorded yet is ordinary, so a caller with an honest fallback asks here instead
        public static bool Has(string folder)
        {
            folder = Normalize(folder);

            return DirAccess.DirExistsAbsolute(folder) && Files(folder).Count > 0;
        }

        // an AudioStreamRandomizer over every sample in the folder
        public AudioStream Stream { get; }

        // zero means the tray is going to be silent
        public int Count { get; }

        ImpactPool(string folder)
        {
            // the randomizer gives sample variation for free; RandomNoRepeats, since replacement would audibly double a sample
            var randomizer = new AudioStreamRandomizer
            {
                PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,

                // 1.08 is about a semitone either way: enough that no two throws match, not enough to sound like a different die
                RandomPitch = 1.08f,
                RandomVolumeOffsetDb = 2f,
            };

            foreach (string file in Files(folder))
            {
                var stream = GD.Load<AudioStream>(folder + file);

                if (stream == null)
                {
                    // developer diagnostic; a load failure is silence, which looks like it works, so it has to say so
                    GD.PushError($"impact pool: {folder}{file} did not load - one impact short");
                    continue;
                }

                randomizer.AddStream(-1, stream);
            }

            Stream = randomizer;
            Count = randomizer.StreamsCount;

            if (Count == 0) GD.PushError($"impact pool: no samples in {folder} - hits there will be silent");
        }

        // the samples, however the folder spells them (ResFolder), or it breaks at export time
        static SortedSet<string> Files(string folder) =>
            new SortedSet<string>(ResFolder.Files(folder, "impact pool").Where(n => n.EndsWith(".wav")));
    }
}
