using System.Linq;
using Content.Sheet;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE CHARACTER SHEET, OPENED FOR A PICTURE (cc_task_cs: the run log's screenshots)
    //
    //   godot --path game res://launch.tscn -- --begin sample_millbrook fighter --start cellar --autodice --autostory
    //         --sheet front --shot <png> --after 600
    //   ... --begin sample_millbrook mage --learn hex --level 5 --start cellar --autodice --autostory --sheet back:hex
    //
    // On the hero's first turn it opens the sheet (with `:spell`, once that spell is cast at the nearest foe and held,
    // straight through the fight's Incantation: a sheet of a hero concentrating) the way C does
    // (PlayDirector.OpenSheet) at the side asked for, or the back with a spell's card or a feature's over it (the one
    // with the most to say). With --shot the picture waits for it. Prints "sheet   opened ..." and what the page reads
    // aloud. Not localized
    public partial class SheetProbe : Node
    {
        public PlayDirector Director { get; set; }

        // front, back, spell or feature, and after a colon a spell to be holding
        public string Side { get; set; } = "front";

        string Holding => Side.Contains(':') ? Side.Split(':')[1] : null;

        string Which => Side.Split(':')[0];

        bool _opened;
        int _frames;

        public override void _Process(double delta)
        {
            if (DevArgs.Shot && Nodes.Under<Shot>(GetTree().Root).FirstOrDefault() is { } shot) shot.Until = () => _opened && _frames > 10;

            if (_opened)
            {
                _frames++;
                return;
            }

            // a caster's "before the fight" card: on to the fight, nothing cast
            if (Director?.Showing is BeforeFightCard before)
            {
                before.Close();
                return;
            }

            CombatDirector combat = Director?.Combat;

            if (combat == null || !combat.CanAct || Director.Showing != null) return;

            if (Holding is { } id && !GameState.Run.Hero.Actor.IsConcentrating) Hold(combat, id);

            Director.OpenSheet(back: Which != "front");
            _opened = true;

            SheetView sheet = SheetView.Of(GameState.Run.Hero, combat.Session.Fight, combat.Session.Turn);

            if (Which == "spell" && sheet.Spells.Count > 0)
                CardOverlay.Show(Director, sheet.Spells.OrderByDescending(c => Ui.Say(c.DescriptionKey).Length).First());

            if (Which == "feature" && sheet.Special.Count > 0)
                CardOverlay.Show(Director, sheet.Special.OrderByDescending(r => Ui.Say(r.DescriptionKey).Length).First());

            if (Director.Showing is SheetPage page)
                foreach (string line in page.Aloud()) GD.Print("sheet   " + line);

            GD.Print($"sheet   opened at the {Side} for {sheet}");
        }

        // the spell cast at the nearest foe and held, as if the hero had cast it a turn ago
        static void Hold(CombatDirector combat, string id)
        {
            Content.Sheet.Hero hero = GameState.Run.Hero;
            Core.Magic.Spell spell = hero.Caster?.Known.FirstOrDefault(s => s.Id == id);
            Core.Characters.Actor foe = combat.Session.Fight.Actors.FirstOrDefault(a => a.Side != hero.Actor.Side && !a.IsDown);

            if (spell == null || foe == null)
            {
                GD.PrintErr($"sheet   can't hold {id}: {(spell == null ? "not known (--learn it)" : "no foe")}");
                return;
            }

            // at the foe, or (a spell about the caster, or a foe out of range) at the hero
            bool cast = combat.Session.Battle.Magic.Cast(hero.Caster, spell, Core.Magic.Aim.At(foe), spell.Level).Cast ||
                        combat.Session.Battle.Magic.Cast(hero.Caster, spell, Core.Magic.Aim.At(hero.Actor), spell.Level).Cast;
            GD.Print($"sheet   {id} cast: {cast}, holding {hero.Actor.Concentrating ?? "nothing"}");
        }
    }
}
