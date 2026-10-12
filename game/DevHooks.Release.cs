namespace Game.Dev
{
    // THE RELEASE BUILD'S DEV REGISTRY: nothing (cc_task_j-split-classes-dev-hooks.md Part 2). game/Dev/ isn't compiled
    // for ExportRelease (Lanorim.csproj) and this file is compiled for nothing else, so in a release the scenes' calls
    // into the registry land here: no probe, no quick start, no demonstration table, no picture. The launch screen
    // opens its first screen itself (`--show book` still works: check-export.ps1 runs the release that way).
    //
    // Keep these in step with Dev/DevHooks.cs: `dotnet build game/Lanorim.csproj -c ExportRelease` fails otherwise
    public static class DevHooks
    {
        public static bool AtLaunch(Game.Screens.Launch launch) => false;

        public static bool AtTable(Game.Table.Table table) => false;

        public static void TableLaid(Game.Table.Table table)
        {
        }

        public static void AtPlay(Game.Play.PlayDirector director)
        {
        }

        public static void AtBuilder(Game.Builder.MapBuilder builder)
        {
        }

        public static void AtAccessDesk(Game.Access.AccessDesk desk)
        {
        }
    }
}
