using System;

namespace Game.Access
{
    // ONE THING TAB CAN REACH ON THE TABLE (cc_task_open-questions-answers.md 4.2): what is said when the hand lands on
    // it, what shows where it is (the square lights, the button takes the focus), and what Enter does to it (a piece
    // is clicked, as the mouse would; a button needs nothing, Enter presses a focused button already)
    public sealed record Reachable(string Says, Action Focus = null, Action Touch = null);
}
