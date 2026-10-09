namespace Core.Rules
{
    // A SHARE OF A NUMBER, ROUNDED UP AND NEVER UNDER 1: the difficulty's enemy hit points and enemy damage
    // (cc_task_h 3.3). A positive number stays positive, so a 75% goblin still has a hit point and a 75% scratch
    // still scratches; 0 and below are left as they are
    public static class Percent
    {
        public static int Of(int value, int percent) =>
            value <= 0 || percent == 100 ? value : System.Math.Max(1, (value * percent + 99) / 100);
    }
}
