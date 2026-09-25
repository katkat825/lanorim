using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Godot;

using ContentLibrary = Content.Schema.Library;

namespace Game.Play
{
    // WHAT OUTLIVES A SCENE CHANGE: the campaigns found on disk, the saves, the settings, and the
    // campaign being played. The launch screen fills it and changes to the table; the table reads it.
    // Plain static state, because Godot frees a scene's nodes when it changes scene and this is the
    // one thing that has to survive that.
    public static class GameState
    {
        static Game.Campaigns.Library _campaigns;

        // every campaign found under the campaign roots, with the SRD and all of them laid together
        public static Game.Campaigns.Library Campaigns => _campaigns ??= Game.Campaigns.Library.Load();

        public static ContentLibrary Content => Campaigns.Content;

        public static IEnumerable<Manifest> Manifests =>
            Campaigns.Playable.Select(c => c.Package.Manifest).Where(m => m != null);

        public static Package Package(string id) => Campaigns.Campaign(id)?.Package;

        static SaveLibrary _saves;

        public static SaveLibrary Saves =>
            _saves ??= new SaveLibrary(ProjectSettings.GlobalizePath(SavesFolder));

        // where the saves live. A headless check (--begin) plays in a folder of its own, so it can
        // never touch - or be reloaded from - a player's own saves
        public static string SavesFolder { get; private set; } = "user://saves";

        public static void SaveProbesApart()
        {
            SavesFolder = $"user://saves_probe_{System.Environment.ProcessId}";
            _saves = null;

            // a probe that was stopped rather than finished leaves its folder; an hour on, it is
            // nobody's, and goes
            string root = ProjectSettings.GlobalizePath("user://");

            try
            {
                foreach (string stale in Directory.EnumerateDirectories(root, "saves_probe_*"))
                    if (Directory.GetLastWriteTimeUtc(stale) < DateTime.UtcNow.AddHours(-1))
                        Directory.Delete(stale, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // a probe's own saves, gone when it is done
        public static void ForgetProbeSaves()
        {
            if (!SavesFolder.Contains("saves_probe_")) return;

            string path = ProjectSettings.GlobalizePath(SavesFolder);

            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException bad)
            {
                GD.PushWarning("probe saves: " + bad.Message);
            }
        }

        // --- settings ------------------------------------------------------------------------------

        const string SettingsFile = "user://settings.json";

        static GameSettings _settings;

        public static GameSettings Settings
        {
            get
            {
                if (_settings != null) return _settings;

                string path = ProjectSettings.GlobalizePath(SettingsFile);
                string text = File.Exists(path) ? File.ReadAllText(path) : "";

                _settings = GameSettings.Read(text, out IReadOnlyList<string> problems);

                foreach (string problem in problems) GD.PushWarning("settings: " + problem);

                return _settings;
            }
        }

        public static void SaveSettings()
        {
            try
            {
                File.WriteAllText(ProjectSettings.GlobalizePath(SettingsFile), Settings.Write());
            }
            catch (IOException bad)
            {
                GD.PushError("settings: could not be saved - " + bad.Message);
            }
        }

        // --- the campaign being played -------------------------------------------------------------

        public static CampaignRun Run { get; private set; }

        public static Package Pack { get; private set; }

        // the travelling companion, which the dialogue's 'companion' speaker becomes
        public static string Companion => Run?.Hero.Class.Companion ?? "";

        // the dice the rules read for the hero: the tray, unless the player turned it off. Set by the
        // table when it comes up, because the tray is a node in that scene
        public static IDiceSource HeroDice { get; set; }

        // the generator behind the GM's screen, seeded from the clock so play is not a replay
        public static IRng Gm { get; } = new SeededRng(System.Environment.TickCount);

        public static TableResolver Resolver { get; private set; }

        public static TableResolver MakeResolver(Hero hero) =>
            Resolver = new TableResolver(Gm, new Deferred(() => HeroDice), a => ReferenceEquals(a, hero?.Actor));

        // a new character in a campaign: the run starts at the campaign's first chapter
        public static CampaignRun Begin(Package pack, Hero hero, int slot)
        {
            Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            Run = new CampaignRun(Content, pack, hero, MakeResolver(hero), Gm, Saves, slot);
            Starting = true;
            return Run;
        }

        // back from a save, at the exact spot it was made
        public static CampaignRun Resume(SaveGame save)
        {
            Package pack = Package(save.Campaign);

            if (pack == null)
            {
                GD.PushError($"load: the campaign '{save.Campaign}' is not installed");
                return null;
            }

            TableResolver resolver = null;

            CampaignRun run = CampaignRun.Resume(save, Content, pack, new Late(() => resolver), Gm, Saves,
                                                 out IReadOnlyList<ContentProblem> problems);

            foreach (ContentProblem problem in problems) GD.PushWarning("load: " + problem);

            if (run == null) return null;

            resolver = MakeResolver(run.Hero);
            Pack = pack;
            Run = run;
            Starting = false;
            return run;
        }

        // true for a run that has not been started yet (a new character); false for a loaded one,
        // which continues rather than starts
        public static bool Starting { get; private set; }

        public static void Leave()
        {
            Run = null;
            Pack = null;
            Resolver = null;
        }

        // the dice source, looked up when a throw happens rather than when the resolver is made - the
        // table sets it after the run exists
        sealed class Deferred : IDiceSource
        {
            readonly Func<IDiceSource> _source;

            public Deferred(Func<IDiceSource> source) => _source = source;

            public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice) =>
                (_source() ?? new Digital(Gm)).Throw(dice);
        }

        // a resolver made after the run that needs it
        sealed class Late : IResolver
        {
            readonly Func<IResolver> _resolver;

            public Late(Func<IResolver> resolver) => _resolver = resolver;

            IResolver Real => _resolver() ?? new StandardResolver(Gm);

            public Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage = Advantage.Flat) =>
                Real.Resolve(kind, modifier, against, advantage);

            public Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage, Core.Characters.Actor roller) =>
                Real.Resolve(kind, modifier, against, advantage, roller);

            public int Roll(DiceRoll dice) => Real.Roll(dice);

            public int Roll(DiceRoll dice, Core.Characters.Actor roller) => Real.Roll(dice, roller);

            public int Roll(DiceRoll dice, out IReadOnlyList<int> faces) => Real.Roll(dice, out faces);

            public int Roll(DiceRoll dice, Core.Characters.Actor roller, out IReadOnlyList<int> faces) =>
                Real.Roll(dice, roller, out faces);
        }
    }
}
