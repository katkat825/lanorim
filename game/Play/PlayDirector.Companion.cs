using System;
using Content.Companions;
using Content.Dialogue;
using Content.Screens;
using Core.Dice;
using Core.Words;
using Game.Table;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- the companion on the table (cc_task_ui-issues-9-30.md 6.4; its barks, cc_task_i Part 2) -----------
        //
        // CompanionMind watches the fight (it is one of the fight's observers), hears the story's cues (a rest, a
        // <<bark>>, the hero's own checks) and the screen's rattle, and feels; the figure on the table plays the
        // feeling. It speaks from the game's bark bank for the hero's companion with this campaign's lines on top,
        // and its Chatter keeps it to one line at a time, a cooldown and the player's "Companion chatter" setting.
        // The mind hears on the rules thread, so what it says and feels is posted to the main thread.

        CompanionMind _mind;
        BarkBubble _bubble;

        // `--say-bark <pool>`: a line from that pool every few seconds, past the chatter, for a screenshot of the bubble
        string _sayBark;
        double _sayBarkIn;

        void WakeCompanion()
        {
            CompanionFigure figure = Table.Companion;

            figure?.Wear(GameState.Companion, Table.Board?.Paint);

            BarkBank bank = Run.Barks;
            var rng = new SeededRng(System.Environment.TickCount);
            var chatter = new Chatter(rng, () => GameState.Settings.Chatter);

            _mind = new CompanionMind(bank?.Open(rng), Run.Hero.Actor, chatter);
            _mind.Felt += mood => MainQueue.Post(() => figure?.Feel(mood));
            _mind.Spoke += key => MainQueue.Post(() => Bark(key));

            Run.Cued += HearsTheStory;
            Run.Tested += SeesTheHerosRoll;

            _bubble = new BarkBubble { Name = "Bark", Camera = Table.Camera, Over = figure };
            _ui.AddChild(_bubble);

            _sayBark = DevArgs.SayBark;

            GD.Print($"play    {GameState.Companion}: {bank?.ToString() ?? "no barks"}");
        }

        void SleepCompanion()
        {
            if (Run == null) return;

            Run.Cued -= HearsTheStory;
            Run.Tested -= SeesTheHerosRoll;
        }

        // raised on the rules thread
        void HearsTheStory(Bark cue) => _mind?.Hears(cue);

        void SeesTheHerosRoll(Core.Resolution.Attempt roll) => _mind?.Sees(roll);

        // --- the story's lines at a moment in a fight (FightCues.cs) -----------------------------------------
        //
        // said one after another, each held for as long as it takes to read, past the chatter's cooldown and the
        // player's chatter setting: they are the story's lines, not barks. Each is in the fight's log too, so a line
        // the player was too busy to read is still there
        readonly System.Collections.Generic.Queue<string> _cued = new();
        double _cueIn;

        void Cue(System.Collections.Generic.IReadOnlyList<DialogueLine> lines)
        {
            foreach (DialogueLine line in lines) _cued.Enqueue(line.Key);

            GD.Print($"play    cue: {lines.Count} line(s) from {lines[0].Node}");
        }

        void TickCues(double delta)
        {
            if ((_cueIn -= delta) > 0 || _cued.Count == 0) return;

            string key = _cued.Dequeue();

            Bark(key);
            _cueIn = GameState.Access.Time(BarkText.Plain(Game.Screens.Ui.Say(key))) + 0.5;
        }

        // a held reaction runs out on the main thread's clock, which is also the chatter's
        void TickCompanion(double delta)
        {
            TickCues(delta);

            _mind?.Tick(delta);

            if (_sayBark == null || (_sayBarkIn -= delta) > 0) return;

            _sayBarkIn = 4;

            if (EnumWords.TryParse(_sayBark, out Bark pool))
                Bark(Run.Barks?.Key(pool.Pool(), 1 + (int)(_played / 4) % Math.Max(1, Run.Barks.Count(pool.Pool()))));
        }

        // A BARK, SHOWN (cc_task_i 2.4): the bubble near the companion, the same line in the fight's log, and read
        // aloud when Read aloud is on. The bubble keeps the emphasis; the log and the voice get the words
        void Bark(string key)
        {
            if (key == null) return;

            string line = Game.Screens.Ui.Say(key);
            string plain = BarkText.Plain(line);

            _bubble?.Say(line, GameState.Access.Time(plain));
            Table.Companion?.Speak();

            if (_fighting) _combat.Write(plain);

            Game.Access.AccessDesk.Instance?.Narrator.Say(Game.Access.Spoken.Of(plain));

            GD.Print($"play    the companion says {key}");
        }

        // the story's "companion" speaker is whichever companion the hero has (DialogueView.Speaker)
        void CompanionSays(DialogueView view)
        {
            if (view.Speaker == GameState.Companion && GameState.Companion != "") Table.Companion?.Speak();
        }
    }
}
