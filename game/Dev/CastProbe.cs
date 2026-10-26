using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Magic;
using Core.Space;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // IS A CAST SEEN AND SAID ON THE TABLE? (cc_task_m3 Part 2: "Druidcraft and Command don't seem to do anything")
    //
    //   godot --headless --path game res://launch.tscn -- --begin sample_millbrook druid --learn druidcraft
    //         --start cellar --autodice --autostory --cast druidcraft
    //   ... --begin sample_millbrook cleric --learn command --start cellar --autodice --autostory --cast command:grovel
    //
    // On the hero's first turn it picks the spell the way the Spells menu does (CombatDirector.Pick), the word the way
    // the bar's choice does (PickMode), and aims it as a click would: at the nearest foe, or at the square beside the
    // hero. Then it waits for the table: a narrated spell's line in the log and under the tray, and Druidcraft's bloom
    // on the board; a Command's word said in the log on the foe's turn, and Grovel's foe lying down. Prints
    // "cast    check passed" or what's missing; with --shot it leaves the quitting to the picture. Not localized.
    public partial class CastProbe : Node
    {
        public CombatDirector Combat { get; set; }

        public string Spell { get; set; }

        public string Mode { get; set; }

        enum Stage { Waiting, Cast, Watching, Done }

        Stage _stage;
        double _settle, _watched;
        int _turns;
        Actor _foe;

        bool _seen;

        public override void _Process(double delta)
        {
            // a picture waits for the moment the table shows it
            if (DevArgs.Shot && Nodes.Under<Shot>(GetTree().Root).FirstOrDefault() is { Until: null } shot) shot.Until = () => _seen;

            if (Combat?.Session == null || _stage == Stage.Done) return;

            CombatSession session = Combat.Session;

            _settle += delta;
            if (_settle < 0.5) return;
            _settle = 0;

            switch (_stage)
            {
                case Stage.Waiting:
                    if (!Combat.CanAct) return;

                    ActionOption option = session.Options().FirstOrDefault(o => o.Spell?.Id == Spell);
                    if (option == null) { Fail($"no {Spell} on the bar (--learn it, and a class that casts it)"); return; }

                    Combat.Pick(option);
                    if (Mode != null) Combat.PickMode(Mode);

                    if (Mode != null && session.Mode != Mode) { Fail($"the bar's choice didn't take '{Mode}' (it is '{session.Mode}')"); return; }

                    _foe = session.Fight.Actors.Where(a => a.Side != session.Hero.Actor.Side && !a.IsDown)
                                  .OrderBy(a => session.Fight.Field.Distance(session.Hero.Actor, a)).FirstOrDefault();

                    Aim(session);
                    _stage = Stage.Cast;
                    break;

                case Stage.Cast:
                    // cast, and the board done showing it (the hero has two actions: the turn is still the hero's)
                    if (session.Selected != null || !Combat.CanAct) return;
                    _stage = Stage.Watching;
                    _turns++;

                    // a Command is obeyed on the foe's turn: the hero's ends
                    if (Mode != null) Combat.EndTurn();
                    break;

                case Stage.Watching:
                    _watched += 0.5;
                    if (Seen(session)) Pass();

                    // the foe made its save and played its own turn: again, while there are slots
                    else if (Mode != null && Combat.CanAct && _watched > 2 && _turns < 4)
                    {
                        _watched = 0;
                        _stage = Stage.Waiting;
                        GD.Print("cast    no word obeyed this round; casting again. the log: " + string.Join(" | ", Combat.LogWritten.TakeLast(8)));
                    }
                    else if (_watched > 30) Fail("the table never showed it");
                    break;
            }
        }

        // as a click on the board would (CombatDirector.Click)
        void Aim(CombatSession session)
        {
            Actor me = session.Hero.Actor;

            switch (session.Selected?.Targeting)
            {
                case Targeting.Creature:
                case Targeting.Creatures:
                    Actor target = session.LegalTargets().Contains(_foe) ? _foe : session.LegalTargets().FirstOrDefault();
                    if (target == null) { Fail("nothing to aim at"); return; }
                    Combat.Rules.Post(() => session.Confirm(target), Combat.Refresh);
                    break;

                // the nearest square nobody stands on, so what grows there is seen
                case Targeting.Square:
                    var stood = session.Fight.Actors.Select(a => session.Fight.Field.Where(a)).ToHashSet();
                    Cell square = session.LegalSquares().Where(c => !stood.Contains(c))
                                         .OrderBy(c => Core.Combat.Battlefield.Distance(c, session.Fight.Field.Where(me).Value)).First();
                    Combat.Rules.Post(() => session.Confirm(square), Combat.Refresh);
                    break;

                default:
                    Combat.Rules.Post(() => session.Confirm(), Combat.Refresh);
                    break;
            }
        }

        bool Seen(CombatSession session)
        {
            Spell spell = GameState.Content.Spells.Find(Spell);

            if (spell.Narrates)
            {
                string line = Ui.Say(spell.NarrationKey, Combat.HeroName);
                bool written = Combat.LogWritten.Any(l => l == line);
                bool bloom = Spell != "druidcraft" || Combat.Board.Sights().Any();

                GD.Print($"cast    {Spell}: the line in the log {(written ? "yes" : "not yet")}, on the board {(bloom ? "yes" : "not yet")}");
                return written && bloom;
            }

            if (Mode == null || _foe == null) return true;

            string obeys = Ui.Say(Core.Localization.KeyConventions.Key("ui", "log", "obeys_" + Mode, "name"), "");
            bool said = Combat.LogWritten.Any(l => l.EndsWith(obeys.Trim()));
            bool lying = Mode != "grovel" || Combat.Board.Of(_foe)?.IsProne == true;

            GD.Print($"cast    {Spell} {Mode}: obeyed in the log {(said ? "yes" : "not yet")}, on the board {(lying ? "yes" : "not yet")}");
            return said && lying;
        }

        void Pass()
        {
            GD.Print("cast    check passed");
            _seen = true;
            _stage = Stage.Done;
            if (!DevArgs.Shot) GetTree().Quit(0);
        }

        void Fail(string why)
        {
            GD.PrintErr("cast    FAILED - " + why);
            _stage = Stage.Done;
            if (!DevArgs.Shot) GetTree().Quit(1);
        }
    }
}
