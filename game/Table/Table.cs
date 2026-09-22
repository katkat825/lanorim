using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Rules;
using Game.Access;
using Game.Dice;
using Game.Localization;
using Game.Tray;
using Godot;

namespace Game.Table
{
    // THE TABLE. Everything the player looks at stands on it, and this is the node that owns the
    // wiring between the things on it and the rules underneath.
    //
    // It is deliberately thin. The tray throws dice and says what they came up; core decides what
    // that means; this is the seam, and the seam is about ten lines long - Ask() below.
    //
    // TO LAY OUT: the tray's place on the table, the lamp, the felt, the GM screen, the companion
    // and the help button are all yours. What is here is one tray, one camera and one light, so
    // the scene opens and something happens.
    public partial class Table : Node3D
    {
        [Export] public NodePath TrayPath { get; set; } = "DiceTray";

        [Export] public NodePath CameraPath { get; set; } = "TableCamera";

        [Export] public NodePath BoardPath { get; set; } = "Board";

        // the captions card. null until the settings page turns it on
        [Export] public NodePath CaptionsPath { get; set; }

        public DiceTray Tray { get; private set; }

        public TableCamera Camera { get; private set; }

        public Game.Board.Board Board { get; private set; }

        public Captioned Captions { get; private set; }

        readonly ILocalizer _text = new GodotLocalizer();

        // what the table is waiting on: the question asked of the dice, and who to tell
        Ask _asking;

        public override void _Ready()
        {
            Tray = GetNodeOrNull<DiceTray>(TrayPath);
            Camera = GetNodeOrNull<TableCamera>(CameraPath);
            Board = GetNodeOrNull<Game.Board.Board>(BoardPath);

            Captions = CaptionsPath == null || CaptionsPath.IsEmpty
                ? null
                : GetNodeOrNull<Captioned>(CaptionsPath);

            if (Tray == null)
            {
                GD.PushError($"table: no dice tray at '{TrayPath}' - there is nothing to roll");
                return;
            }

            // the captions card tells the things that make noise about itself, rather than hunting
            // them; one walk of the tree at boot
            Captions?.Tells(this);

            // THE LOCALE PROBE ASKS ONE QUESTION AND LEAVES. Nothing else is wired for it,
            // because it is about the words and not about the table.
            if (LocaleProbe.RequestedFrom(OS.GetCmdlineUserArgs()))
            {
                AddChild(new LocaleProbe { Name = "LocaleProbe" });
                return;
            }

            // THE FAIRNESS SWEEP TAKES OVER THE TRAY. Handed the dice before anything below is
            // wired, because the probe drives the tray itself and a second listener would throw
            // on top of it.
            if (DiceProbe.RequestedFrom(OS.GetCmdlineUserArgs(),
                                        out Die shape, out int throws, out int handful))
            {
                AddChild(new DiceProbe
                {
                    Name = "DiceProbe",
                    Tray = Tray,
                    Shape = shape,
                    Throws = throws,
                    Handful = handful,
                });

                return;
            }

            Tray.Rolled += Read;

            GD.Print("table   Q and E turn the table a quarter, - and = zoom, space throws");

            Demonstrate();

            // and a picture of it, if one was asked for. Added last so it photographs the table
            // with everything already on it
            if (Shot.RequestedFrom(OS.GetCmdlineUserArgs(), out string path, out int after))
                AddChild(new Shot { Name = "Shot", Path = path, After = after });
        }

        // ONE REAL CHECK, THROWN ON THE REAL TRAY, so opening the scene shows the whole stack
        // working: a hero built from the SRD data, a d20 that tumbles in a wooden box, and core
        // reading the face off the felt. Delete it the moment there is a campaign to play instead.
        Actor _someone;

        void Demonstrate()
        {
            Library srd = Library.Srd();

            var making = new Content.Creation.Creation(srd, srd.Backgrounds);

            making.Pick(srd.Class("rogue"));
            making.Pick(srd.Kind("halfling"));
            making.Pick(srd.Background("criminal"));

            foreach (Skill pick in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(pick);

            foreach (Skill pick in making.Skills.Take(making.ExpertisePicksLeft).ToList())
                making.Master(pick);

            making.Call("Pell");

            Hero hero = making.Finish();

            if (hero == null)
            {
                GD.PushError("table: could not build the demonstration hero - " +
                             string.Join("; ", making.Problems));
                return;
            }

            _someone = hero.Actor;

            GD.Print($"table   {hero}");

            LayAMap(hero);

            Throw();
        }

        // A MAP THE MAP BUILDER COULD HAVE MADE, laid on the board with the hero standing on it
        // and a goblin across the room. Built in code here only because there is no campaign to
        // load one from yet - content/Maps/MapDraft.cs is the thing that makes them, and this is
        // the same MapLayout it produces.
        void LayAMap(Hero hero)
        {
            if (Board == null)
            {
                GD.Print("table   no board on the table yet - the dice work without one");
                return;
            }

            var draft = new Content.Maps.MapDraft(10, 8);

            draft.Paint(new Cell(0, 0), new Cell(9, 7), Tile.Floor);
            draft.Enclose();

            // a little difficult ground and a wall to walk round, so the tiles have something to
            // draw other than floor
            draft.Paint(new Cell(4, 2), new Cell(5, 3), Tile.Rough);

            for (int y = 0; y < 5; y++) draft.Wall(new Border(new Cell(6, y), true), Edge.Wall);

            draft.Wall(new Border(new Cell(6, 3), true), Edge.Door);

            draft.PlaceStart(new Cell(1, 4));
            draft.PlaceSpawn(1, new Cell(8, 1));

            if (!draft.Sound)
            {
                GD.PushError("table: the demonstration map does not hold up - " +
                             string.Join("; ", draft.Problems()));
                return;
            }

            MapLayout map = draft.Layout();

            Board.Lay(map);

            _field = new Battlefield(map);

            _field.Place(hero.Actor, map.Start);
            Board.Place(hero.Actor, map.Start);

            Actor goblin = Library.Srd().Bestiary.Find("goblin").Spawn();
            Cell there = map.SpawnAt(1) ?? new Cell(8, 1);

            _field.Place(goblin, there);
            Board.Place(goblin, there);

            GD.Print($"table   {Board}, {map}");

            // every square the hero could walk to this turn, lit the way the UI will light them
            Board.ShowReach(_field, hero.Actor, hero.Actor.Speed / Turn.FeetPerSquare,
                            new Color(0.3f, 0.6f, 0.9f, 0.35f));
        }

        Battlefield _field;

        // the demonstration throw, and what space does
        void Throw()
        {
            if (_someone == null || Busy) return;

            const int dc = 15;

            GD.Print("");
            GD.Print($"table   {_text.Get(Skills.NameKey(Skill.Stealth))} against DC {dc}, " +
                     $"modifier {_someone.CheckModifier(Skill.Stealth):+0;-0}");

            Check(_someone, Skill.Stealth, dc, Advantage.Flat, attempt =>
            {
                GD.Print($"table   {attempt}");

                GD.Print(attempt.Succeeded ? "table   unseen." : "table   somebody looks up.");

                // the keeper delta: a natural 1 or 20 changes something whatever the total said
                Consequence drawn = Library.Srd().Consequences
                                           .DrawFor(new SeededRng((int)Time.GetTicksMsec()), attempt);

                if (drawn == null) return;

                GD.Print($"table   and {_text.Get(drawn.LineKey)}");
            });
        }


        // --- the seam ---------------------------------------------------------------------------

        // A QUESTION PUT TO THE DICE, and what to do with the answer.
        //
        // THE PHYSICS IS THE RANDOM NUMBER GENERATOR. Nothing rolls a number and then animates a
        // die onto it: the die is thrown, the felt is read, and the faces go to core through a
        // ScriptedRng. That is the whole of the coupling between the table and the rules, and it is
        // why check-dice measures the tray and not just the generator - a biased tray would be a
        // biased game no matter how correct the arithmetic downstream.
        sealed class Ask
        {
            public Ask(Die[] dice, Func<IResolver, object> answer, Action<object> tell)
            {
                Dice = dice;
                Answer = answer;
                Tell = tell;
            }

            public Die[] Dice { get; }

            // given a resolver reading the felt, works out what the throw meant
            public Func<IResolver, object> Answer { get; }

            public Action<object> Tell { get; }
        }

        public bool Busy => _asking != null || (Tray?.IsThrowing ?? false);

        // the general form: throw these dice, then let core read them
        bool Put(Die[] dice, Func<IResolver, object> answer, Action<object> tell)
        {
            if (Tray == null || Busy) return false;

            _asking = new Ask(dice, answer, tell);

            if (Tray.Throw(dice)) return true;

            _asking = null;
            return false;
        }

        void Read(TrayRoll roll)
        {
            Ask asking = _asking;
            _asking = null;

            if (asking == null) return;

            // the faces on the felt, in throw order, as the only randomness core sees
            var resolver = new StandardResolver(roll.AsRng());

            object answer = asking.Answer(resolver);

            asking.Tell?.Invoke(answer);
        }


        // --- what the game actually asks ---------------------------------------------------------

        // a skill check. advantage throws two d20 and core keeps the one the rules say to keep,
        // and both stay on the felt where the player can see them
        public bool Check(Actor actor, Skill skill, int dc, Advantage advantage,
                          Action<Attempt> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Checks.Check(r, actor, skill, dc, advantage),
                       a => then?.Invoke((Attempt)a));
        }

        public bool Save(Actor actor, Ability ability, int dc, Advantage advantage,
                         Action<Attempt> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Checks.Save(r, actor, ability, dc, advantage),
                       a => then?.Invoke((Attempt)a));
        }

        // the single d20 against 10, no modifiers, that decides whether a downed hero gets up
        public bool DeathSave(Actor actor, Action<Attempt> then) =>
            Put(new[] { Die.D20 },
                r => Checks.DeathSave(r, actor),
                a => then?.Invoke((Attempt)a));

        // an attack. the damage is a second throw, because that is two handfuls at a table and the
        // player watches the first one land before the second goes up
        public bool Strike(Actor attacker, Actor target, Attack attack, Advantage advantage,
                           IReadOnlyList<Rider> riders, Action<Blow> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Core.Rules.Strike.Make(r, attacker, target, attack, advantage, riders),
                       a => then?.Invoke((Blow)a));
        }

        // damage, healing, hit dice - anything that is not a d20 against a number
        public bool Roll(DiceRoll dice, Action<int> then)
        {
            if (!dice.RollsAnything)
            {
                // nothing to throw, so nothing is thrown and the flat number comes straight back
                then?.Invoke(dice.Modifier);
                return true;
            }

            Die[] handful = Enumerable.Repeat(dice.Die, dice.Count).ToArray();

            return Put(handful, r => r.Roll(dice), a => then?.Invoke((int)a));
        }


        // --- turning the table --------------------------------------------------------------------

        // BY THE ACT, NOT BY THE KEYCODE. Every one of these is in the InputMap under its own
        // name, so rebinding it on the accessibility page rebinds it here too - a control the
        // keyboard cannot reach is not a control, it is a mouse gesture (Act).
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false }) return;

            if (Pressed(@event, Act.TurnLeft)) Camera?.Turn(-1);
            else if (Pressed(@event, Act.TurnRight)) Camera?.Turn(1);
            else if (Pressed(@event, Act.ZoomIn)) Camera?.ZoomBy(0.12f);
            else if (Pressed(@event, Act.ZoomOut)) Camera?.ZoomBy(-0.12f);
            else if (Pressed(@event, Act.ThrowDice)) Throw();
            else return;

            GetViewport().SetInputAsHandled();
        }

        // an act with no entry in the InputMap is a control nobody can reach, so it is shouted
        // about once rather than silently doing nothing
        static bool Pressed(InputEvent @event, Act act)
        {
            string action = act.Word();

            if (InputMap.HasAction(action)) return @event.IsActionPressed(action);

            GD.PushError($"table: '{action}' is not in the InputMap - add it to project.godot " +
                         "or nothing on the keyboard does it");

            return false;
        }
    }
}
