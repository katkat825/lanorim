using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Combat;
using Content.Schema;
using Content.SoloPlay;
using Core.Words;

namespace Content.Screens
{
    // how fast the enemies' turns play out on the table
    public enum CombatSpeed
    {
        Slow,
        Normal,
        Fast,

        // no animation: the moves land at once and the log says what happened
        Instant,
    }

    // THE GAME HALF OF SETTINGS (Tier 2.8/3a): the combat dials and the reaction policies. The
    // other half - text size, contrast, captions, read-aloud, key bindings - is game/Access's
    // Adjustments, which is Godot's and sits on the same screen as a second tab.
    public sealed class GameSettings
    {
        public CombatSpeed EnemySpeed { get; set; } = CombatSpeed.Normal;

        // how hard the fights are (cc_task_h 3.3): the enemies' hit points and damage, never the hero's rules.
        // changeable at any time; Standard is the game as it always was. the game hands it to SoloRules
        public PlayDifficulty Difficulty { get; set; } = PlayDifficulty.Standard;

        // the hero's rolls are thrown on the tray unless this is on; the numbers are the same dice
        public bool SkipPhysicalDice { get; set; }

        // the camera follows each enemy as it moves
        public bool FollowEnemies { get; set; } = true;

        // the tray lifts toward the camera for the hero's own throws and goes back after
        // (updated_decisions.md; cc_task_ui-issues-9-30.md 4)
        public bool BringTrayToMe { get; set; } = true;

        public bool LogOpen { get; set; }

        // the in-game reader speaks what is on the table aloud (Game.Access.Narrator); F3 turns it on and off from
        // anywhere (cc_task_open-questions-answers.md 4.2). Off until asked: a game that starts talking at somebody
        // who didn't want it is a game they turn off
        public bool ReadAloud { get; set; }

        // how much the companion says (cc_task_i 2.5): Off, Some (the frequent pools halved) or All
        public Companions.CompanionChatter Chatter { get; set; } = Companions.CompanionChatter.All;

        public ReactionSettings Reactions { get; } = new ReactionSettings();

        // seconds an enemy's move is given at each speed; Instant is none
        public double SecondsPerEnemyStep => EnemySpeed switch
        {
            CombatSpeed.Slow => 0.6,
            CombatSpeed.Normal => 0.35,
            CombatSpeed.Fast => 0.15,
            _ => 0,
        };

        public string Write()
        {
            using var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteString("enemy_speed", EnumWords.Name(EnemySpeed));
                json.WriteString("difficulty", EnumWords.Name(Difficulty));
                json.WriteBoolean("skip_physical_dice", SkipPhysicalDice);
                json.WriteBoolean("follow_enemies", FollowEnemies);
                json.WriteBoolean("bring_tray", BringTrayToMe);
                json.WriteBoolean("log_open", LogOpen);
                json.WriteBoolean("read_aloud", ReadAloud);
                json.WriteString("companion_chatter", EnumWords.Name(Chatter));

                json.WritePropertyName("reactions");
                json.WriteStartObject();
                foreach (KeyValuePair<string, ReactionPolicy> r in Reactions.Chosen.OrderBy(r => r.Key, StringComparer.Ordinal))
                    json.WriteString(r.Key, r.Value.Id());
                json.WriteEndObject();

                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        // a settings file that will not read is the defaults, and the problem is named
        public static GameSettings Read(string text, out IReadOnlyList<string> problems)
        {
            var found = new List<string>();
            var settings = new GameSettings();
            problems = found;

            if (string.IsNullOrWhiteSpace(text)) return settings;

            if (!Json.TryParse(text, out JsonDocument doc, out string bad))
            {
                found.Add(bad);
                return settings;
            }

            using (doc)
            {
                JsonElement root = doc.RootElement;

                if (root.Has("enemy_speed"))
                {
                    if (EnumWords.TryName(root.Text("enemy_speed"), out CombatSpeed speed))
                        settings.EnemySpeed = speed;
                    else
                        found.Add($"'{root.Text("enemy_speed")}' is not a combat speed");
                }

                if (root.Has("difficulty"))
                {
                    if (EnumWords.TryName(root.Text("difficulty"), out PlayDifficulty difficulty))
                        settings.Difficulty = difficulty;
                    else
                        found.Add($"'{root.Text("difficulty")}' is not a difficulty");
                }

                if (root.Has("companion_chatter"))
                {
                    if (EnumWords.TryName(root.Text("companion_chatter"), out Companions.CompanionChatter chatter))
                        settings.Chatter = chatter;
                    else
                        found.Add($"'{root.Text("companion_chatter")}' is not a companion chatter setting");
                }

                settings.SkipPhysicalDice = Flag(root, "skip_physical_dice", false);
                settings.FollowEnemies = Flag(root, "follow_enemies", true);
                settings.BringTrayToMe = Flag(root, "bring_tray", true);
                settings.LogOpen = Flag(root, "log_open", false);
                settings.ReadAloud = Flag(root, "read_aloud", false);

                if (root.TryGetProperty("reactions", out JsonElement reactions) &&
                    reactions.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty r in reactions.EnumerateObject())
                    {
                        if (EnumWords.TryParse(r.Value.GetString(), out ReactionPolicy policy))
                            settings.Reactions.Set(r.Name, policy);
                        else
                            found.Add($"'{r.Value}' is not a reaction policy (for '{r.Name}')");
                    }
            }

            return settings;
        }

        static bool Flag(JsonElement root, string name, bool otherwise) =>
            root.TryGetProperty(name, out JsonElement v) && (v.ValueKind == JsonValueKind.True ||
                                                            v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean()
                : otherwise;

        public static string SpeedKey(CombatSpeed speed) =>
            ScreenKeys.Key("settings", "speed_" + EnumWords.Name(speed));

        // each difficulty's name, and what it's for, in words that never judge the player
        public static string DifficultyKey(PlayDifficulty difficulty) =>
            ScreenKeys.Key("settings", "difficulty_" + EnumWords.Name(difficulty));

        public static string DifficultyAboutKey(PlayDifficulty difficulty) =>
            ScreenKeys.Key("settings", "difficulty_" + EnumWords.Name(difficulty) + "_about");

        public static readonly string DifficultyTitleKey = ScreenKeys.Key("settings", "difficulty");

        public static string ChatterKey(Companions.CompanionChatter chatter) =>
            ScreenKeys.Key("settings", "chatter_" + EnumWords.Name(chatter));

        public static readonly string ChatterTitleKey = ScreenKeys.Key("settings", "chatter");

        public static readonly string TitleKey = ScreenKeys.Key("settings", "title");
        public static readonly string GameTabKey = ScreenKeys.Key("settings", "game");
        public static readonly string AccessTabKey = ScreenKeys.Key("settings", "access");
        public static readonly string EnemySpeedKey = ScreenKeys.Key("settings", "enemy_speed");
        public static readonly string SkipDiceKey = ScreenKeys.Key("settings", "skip_physical_dice");
        public static readonly string FollowKey = ScreenKeys.Key("settings", "follow_enemies");
        public static readonly string BringTrayKey = ScreenKeys.Key("settings", "bring_tray");
        public static readonly string ReactionsKey = ScreenKeys.Key("settings", "reactions");
        public static readonly string ReadAloudKey = ScreenKeys.Key("settings", "read_aloud");

        public static IEnumerable<string> Keys() =>
            Enum.GetValues<CombatSpeed>().Select(SpeedKey)
                .Concat(Enum.GetValues<PlayDifficulty>().Select(DifficultyKey))
                .Concat(Enum.GetValues<PlayDifficulty>().Select(DifficultyAboutKey))
                .Concat(Enum.GetValues<Companions.CompanionChatter>().Select(ChatterKey))
                .Concat(new[] { ChatterTitleKey })
                .Concat(new[] { DifficultyTitleKey, TitleKey, GameTabKey, AccessTabKey, EnemySpeedKey, SkipDiceKey, FollowKey, BringTrayKey, ReactionsKey,
                                ReadAloudKey });
    }
}
