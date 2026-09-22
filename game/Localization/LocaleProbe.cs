using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Godot;

namespace Game.Localization
{
    // DOES GODOT ACTUALLY HAVE THE WORDS?
    //
    // check-locale.ps1 proves the CSV has English for every key. That is not the same question.
    // Godot reads the compiled `.translation` binary beside the CSV, so a key added to the CSV and
    // not re-imported is missing at runtime while every test in the repo still passes - a raw
    // `spell.fireball.name` on a card and a green suite saying all is well.
    //
    // This is the check that catches it, and it can only run inside the engine:
    //
    //   godot --headless --path game res://table.tscn -- --locale
    //
    // After editing game/locale/game.csv, re-import before trusting anything:
    //
    //   godot --headless --path game --import
    public partial class LocaleProbe : Node
    {
        public const string Flag = "--locale";

        public static bool RequestedFrom(string[] args) =>
            args != null && Array.IndexOf(args, Flag) >= 0;

        public override void _Ready()
        {
            var text = new GodotLocalizer();

            string[] keys = GameKeys.All().Distinct()
                                    .OrderBy(k => k, StringComparer.Ordinal)
                                    .ToArray();

            var malformed = new List<string>();
            var missing = new List<string>();

            foreach (string key in keys)
            {
                if (!KeyConventions.IsWellFormed(key))
                {
                    malformed.Add(KeyConventions.Explain(key));
                    continue;
                }

                // Godot hands the key straight back when it does not have it
                if (!text.Has(key)) missing.Add(key);
            }

            GD.Print($"locale  {TranslationServer.GetLocale()}, {keys.Length} keys asked for");

            foreach (string problem in malformed.Take(20)) GD.PrintErr("  " + problem);

            foreach (string key in missing.Take(30)) GD.PrintErr($"  '{key}' has no translation");

            if (missing.Count > 30) GD.PrintErr($"  ...and {missing.Count - 30} more");

            if (malformed.Count + missing.Count == 0)
            {
                GD.Print("locale  every key the game emits has words in it");
                GetTree().Quit(0);
                return;
            }

            GD.PrintErr($"locale  {malformed.Count} malformed, {missing.Count} missing.");
            GD.PrintErr("locale  if the CSV has them, the .translation beside it is stale: " +
                        "godot --headless --path game --import");

            GetTree().Quit(1);
        }
    }
}
