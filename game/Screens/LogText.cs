using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Localization;

namespace Game.Screens
{
    // A LINE OF THE FIGHT LOG, IN WORDS. The log keeps a key and its arguments; an argument that is a
    // creature becomes its name (the hero's own, a monster's by statblock), one that is itself a key
    // (a damage type) becomes its words, and a number stays a number.
    public static class LogText
    {
        public static string Say(LogLine line, Battle battle, string heroName)
        {
            if (line == null) return "";

            object[] words = line.Args.Select(arg => Word(arg, battle, heroName)).ToArray();

            return Ui.Say(line.Key, words);
        }

        public static string NameOf(Actor actor, Battle battle, string heroName)
        {
            if (actor == null) return "";

            if (battle != null && ReferenceEquals(actor, battle.Hero.Actor)) return heroName;

            if (battle?.StatblockOf(actor) is { } monster) return Ui.Say(KeyConventions.MonsterName(monster.Id));

            return actor.Id;
        }

        static object Word(object arg, Battle battle, string heroName) => arg switch
        {
            Actor actor => NameOf(actor, battle, heroName),
            string key when key.Contains('.') && Ui.Text.Has(key) => Ui.Say(key),
            _ => arg,
        };
    }
}
