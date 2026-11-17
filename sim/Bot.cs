using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Combat;
using Content.Schema;
using Content.Sheet;
using Core.Combat;
using Core.Magic;
using Core.Resolution;

namespace Sim
{
    // HOW THE SIM'S HERO IS PLAYED (cc_task_cb Part 1), taken out of the arguments like the solo switches:
    //   --bot person   the default: a caster played the way a sensible player would
    //   --bot plain    the AutoPlayer as it was before 2026-11-16, for task H's numbers
    // "person" is four things, and nothing cleverer: the mage learns Mage Armor and Shield before the rest of its
    // spells (the quick hero takes the first on the list, which a person wouldn't); whatever the before-the-fight card
    // offers is cast or used first (BeforeTheFight, as the table offers it); a ranged caster steps out of melee when
    // that pays (AutoPlayer.KeepsAway); and the hero's reactions follow the game's own Auto settings (a Shield only
    // when it turns the hit) instead of the engine's bare "take the first reaction offered"
    public static class Bot
    {
        public static bool Person { get; set; } = true;

        public static string[] Take(string[] args)
        {
            var rest = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--bot" && i + 1 < args.Length)
                {
                    Person = args[++i] != "plain";
                    continue;
                }

                rest.Add(args[i]);
            }

            Console.WriteLine($"bot: {(Person ? "person" : "plain")}");
            return rest.ToArray();
        }

        // the spells a person's hero of this class learns first
        public static string[] LearnFirst(string cls) =>
            Person && cls == "mage" ? new[] { "mage_armor", "shield" } : null;

        // the hero's reactions: the game's settings at their defaults (every reaction on Auto), or none set
        public static IReactionChooser Chooser() =>
            Person ? new PolicyChooser(new ReactionSettings(), null) : null;

        public static AutoPlayer Player() => new AutoPlayer { KeepsAway = Person };

        // before the fight: everything the card offers, spells cast and features used, until it offers nothing
        public static void Ready(Hero hero, IResolver resolver)
        {
            if (!Person) return;

            for (int guard = 0; guard < 8; guard++)
            {
                Spell spell = BeforeTheFight.Offered(hero).FirstOrDefault();
                Feature feature = BeforeTheFight.Features(hero).FirstOrDefault();

                if (spell != null) BeforeTheFight.Cast(hero, spell, resolver, out _);
                else if (feature != null) BeforeTheFight.Use(hero, feature, resolver, out _);
                else return;
            }
        }
    }
}
