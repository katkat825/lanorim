using System;
using System.Collections.Generic;
using Content.SoloPlay;
using Core.Words;

namespace Sim
{
    // THE SOLO SWITCHES FROM THE COMMAND LINE (cc_task_h Part 5), taken out of the arguments before a command reads
    // them, so every sim can be run each way:
    //   --solo none|slot|keepsake|both   which of 3.1 (the caster's extra slot) and 3.2 (the keepsake) are on
    //   --difficulty story|standard|hard
    // With neither, the game's own settings: both on, Standard.
    public static class SoloFlags
    {
        public static string[] Take(string[] args)
        {
            var rest = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : "";

                if (args[i] == "--solo")
                {
                    SoloRules.CasterSlot = next is "slot" or "both";
                    SoloRules.Keepsake = next is "keepsake" or "both";
                    i++;
                }
                else if (args[i] == "--difficulty" && EnumWords.TryName(next, out PlayDifficulty difficulty))
                {
                    SoloRules.Difficulty = difficulty;
                    i++;
                }
                else rest.Add(args[i]);
            }

            Console.WriteLine($"solo: caster slot {(SoloRules.CasterSlot ? "on" : "off")}, keepsake " +
                              $"{(SoloRules.Keepsake ? "on" : "off")}, {EnumWords.Name(SoloRules.Difficulty)}");

            return rest.ToArray();
        }
    }
}
