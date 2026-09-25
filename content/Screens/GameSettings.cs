using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Combat;
using Content.Schema;

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

        // the hero's rolls are thrown on the tray unless this is on; the numbers are the same dice
        public bool SkipPhysicalDice { get; set; }

        // the camera follows each enemy as it moves
        public bool FollowEnemies { get; set; } = true;

        public bool LogOpen { get; set; }

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
                json.WriteString("enemy_speed", EnemySpeed.ToString().ToLowerInvariant());
                json.WriteBoolean("skip_physical_dice", SkipPhysicalDice);
                json.WriteBoolean("follow_enemies", FollowEnemies);
                json.WriteBoolean("log_open", LogOpen);

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
                    if (Enum.TryParse(root.Text("enemy_speed"), true, out CombatSpeed speed))
                        settings.EnemySpeed = speed;
                    else
                        found.Add($"'{root.Text("enemy_speed")}' is not a combat speed");
                }

                settings.SkipPhysicalDice = Flag(root, "skip_physical_dice", false);
                settings.FollowEnemies = Flag(root, "follow_enemies", true);
                settings.LogOpen = Flag(root, "log_open", false);

                if (root.TryGetProperty("reactions", out JsonElement reactions) &&
                    reactions.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty r in reactions.EnumerateObject())
                    {
                        if (ReactionPolicies.TryParse(r.Value.GetString(), out ReactionPolicy policy))
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
            ScreenKeys.Key("settings", "speed_" + speed.ToString().ToLowerInvariant());

        public static readonly string TitleKey = ScreenKeys.Key("settings", "title");
        public static readonly string GameTabKey = ScreenKeys.Key("settings", "game");
        public static readonly string AccessTabKey = ScreenKeys.Key("settings", "access");
        public static readonly string EnemySpeedKey = ScreenKeys.Key("settings", "enemy_speed");
        public static readonly string SkipDiceKey = ScreenKeys.Key("settings", "skip_physical_dice");
        public static readonly string FollowKey = ScreenKeys.Key("settings", "follow_enemies");
        public static readonly string ReactionsKey = ScreenKeys.Key("settings", "reactions");

        public static IEnumerable<string> Keys() =>
            Enum.GetValues<CombatSpeed>().Select(SpeedKey)
                .Concat(new[] { TitleKey, GameTabKey, AccessTabKey, EnemySpeedKey, SkipDiceKey, FollowKey, ReactionsKey });
    }
}
