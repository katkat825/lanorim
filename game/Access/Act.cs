using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Godot;

namespace Game.Access
{
    // EVERYTHING THE KEYBOARD CAN DO, AND THEREFORE EVERYTHING THERE IS TO REBIND.
    //
    // Closed, and short on purpose. The table is made of objects rather than controls, so a
    // keyboard player needs a way to move between them, a way to press the one they are on, and
    // the two things a lost player reaches for - the rules, and "where are we". There is no
    // movement key and no hotbar, because there is nothing for them to do.
    //
    // WHAT LANORIM ADDED: the camera. The old build's camera was fixed in a room; this one turns
    // in quarters and zooms (ART_DIRECTION section 4), and a control the mouse can reach has to be
    // one the keyboard can reach too, or it is not a control - it is a mouse gesture.
    //
    // These ARE the Input actions: Word() is the name in the InputMap, so throw_dice keeps the name
    // it has had since M1 and nothing has to be kept in step with project.godot by hand.
    public enum Act
    {
        // the mouse's own button keeps its action (place_piece); this is the same press, by keyboard
        Touch,

        ReachNext,

        ReachBack,

        ThrowDice,

        // the "?" on the table, from anywhere - the one control a lost player should not have to find
        Help,

        // "what was I doing / where are we", askable at any point
        WhereAreWe,

        // the screen reader, on and off, without going through the book to get there
        ReadAloud,

        // a quarter turn of the table, each way, and the zoom
        TurnLeft,

        TurnRight,

        ZoomIn,

        ZoomOut,
    }

    public static class Acts
    {
        public static string Word(this Act act) => act switch
        {
            Act.Touch => "touch",
            Act.ReachNext => "reach_next",
            Act.ReachBack => "reach_back",
            Act.ThrowDice => "throw_dice",
            Act.WhereAreWe => "where_are_we",
            Act.ReadAloud => "read_aloud",
            Act.TurnLeft => "turn_left",
            Act.TurnRight => "turn_right",
            Act.ZoomIn => "zoom_in",
            Act.ZoomOut => "zoom_out",
            _ => act.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Act>().Select(Word).ToArray();

        public const string Subject = "act";

        // ui.act.reach_next.name - "Reach for the next thing - {0}", the key counted in, so the page
        // that lists the bindings reads as sentences rather than as a table of two columns
        public static string NameKey(this Act act) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(act), "name");

        // what stands in the {0} while the page is waiting for you to press something
        public static string Waiting => KeyConventions.Key(KeyConventions.UiNs, Subject, "waiting");

        public static IEnumerable<string> Keys()
        {
            foreach (Act act in Enum.GetValues<Act>()) yield return NameKey(act);

            yield return Waiting;
        }

        // every act's line counts its key in; nothing else here does
        public static bool TakesAnArgument(string key) =>
            key != Waiting && Enum.GetValues<Act>().Any(a => NameKey(a) == key);

        // THE DEFAULTS, and each one is the key a player already expects. Tab walks a list in every
        // piece of software there is, Enter presses the thing, and Space has thrown the dice since
        // the tray existed.
        public static Key Standard(this Act act) => act switch
        {
            Act.Touch => Key.Enter,
            Act.ReachNext => Key.Tab,
            Act.ReachBack => Key.Tab,
            Act.ThrowDice => Key.Space,
            Act.Help => Key.F1,
            Act.WhereAreWe => Key.F2,
            Act.ReadAloud => Key.F3,

            // Q and E turn, the way they do in every game with a turnable view; the zoom keys are
            // the ones every map in the world uses
            Act.TurnLeft => Key.Q,
            Act.TurnRight => Key.E,
            Act.ZoomIn => Key.Equal,
            Act.ZoomOut => Key.Minus,

            _ => Key.None,
        };

        // reaching backward is the one that shares a key, the way it does everywhere
        public static bool Shifted(this Act act) => act == Act.ReachBack;

        // the acts a hand needs to walk the room. Named, because a rebinding that left one of these
        // unbound would leave a keyboard player with no way back out of wherever they are
        public static bool Essential(this Act act) =>
            act is Act.Touch or Act.ReachNext or Act.ReachBack;

        public static bool TryWord(string word, out Act act)
        {
            act = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Act one in Enum.GetValues<Act>())
            {
                if (Word(one) != trimmed) continue;

                act = one;
                return true;
            }

            return false;
        }
    }
}
