using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Core.Magic;
using Game.Play;
using Game.Screens;

namespace Game.Dev
{
    // HEROES FOR THE CHARACTER SHEET'S WORST CASES (cc_task_cs, check-layout): a caster who knows every spell on the class
    // list up to the level they can cast, and the level 20 hero with the most features on the Special side. Built the way
    // the tests build them, never saved
    public static class SheetSamples
    {
        static Library Content => GameState.Content;

        public static Hero Make(string cls, int level, bool everySpell = false)
        {
            CharacterClass made = Content.Class(cls);

            var hero = new Hero("Probe", made, Content.Kind("human"), Content.Background("soldier"),
                                global::Content.Creation.Creation.Standard(made), level);

            int highest = made.Casts ? made.ResourceAt(level, SpellResourceMode.Slots) is SpellSlots slots
                                           ? Enumerable.Range(SpellLevels.Lowest, SpellLevels.Highest).Where(l => slots.Maximum(l) > 0).DefaultIfEmpty(0).Max()
                                           : 0
                                       : -1;

            IEnumerable<Spell> spells = everySpell && made.Casts
                ? Content.Spells.For(cls).Where(s => s.Level <= highest)
                : Enumerable.Empty<Spell>();

            hero.Build(null, made.SkillChoices.Take(made.SkillPicks).ToList(), null, Content.Items, spells.ToList());

            return hero;
        }

        // of every class, the level 20 hero whose Special side is longest
        public static Hero MostFeatures()
        {
            Hero most = Content.Classes.All.Select(c => Make(c.Id, 20)).OrderByDescending(h => SheetView.Of(h).Special.Count).First();
            Godot.GD.Print($"sheet   the most features at level 20: {most.Class.Id}, {SheetView.Of(most).Special.Count}");
            return most;
        }

        // a sheet out of a fight, at either side
        public static SheetPage Page(Hero hero, bool back) =>
            new SheetPage(() => SheetView.Of(hero), () => new SheetActs(hero), back: back);
    }
}
