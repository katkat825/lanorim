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
            AccessFolder = SavesFolder;
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

        // --- the access dials and the key bindings (game/Access) ------------------------------------

        static Game.Access.Adjustments _access;

        // where settings.txt lives; a probe keeps its own beside its saves, so it never rebinds the
        // player's keys
        static string AccessFolder { get; set; } = "user://";

        // read from user://settings.txt the first time anything asks, and the player's rebindings put
        // into the InputMap then, so every screen after it hears the keys the player chose
        public static Game.Access.Adjustments Access
        {
            get
            {
                if (_access != null) return _access;

                _access = Game.Access.Adjustments.From(ProjectSettings.GlobalizePath(AccessFolder));

                if (_access.Keys.Changed > 0) Game.Access.Keyboard.Install(_access.Keys);

                return _access;
            }
        }

        public static void SaveAccess()
        {
            if (!Access.To(ProjectSettings.GlobalizePath(AccessFolder)))
                GD.PushError("settings: the key bindings could not be saved");
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

            CampaignRun run = CampaignRun.Resume(save, Content, pack, hero => MakeResolver(hero), Gm, Saves,
                                                 out IReadOnlyList<ContentProblem> problems);

            foreach (ContentProblem problem in problems) GD.PushWarning("load: " + problem);

            if (run == null) return null;

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

            public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice)
            {
                IDiceSource source = _source();

                if (source != null) return source.Throw(dice);

                // no table up to throw them on (a level-up's hit points rolled from a menu, say)
                GD.PushWarning($"dice: the hero's {string.Join(" ", dice ?? Array.Empty<Die>())} had no tray to land on - rolled digitally instead");
                return new Digital(Gm).Throw(dice);
            }
        }
    }
}
