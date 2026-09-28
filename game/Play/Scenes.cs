using Content.Saves;
using Godot;

namespace Game.Play
{
    // WHERE THE GAME GOES NEXT: the table, or back to the campaign book. the book's Continue and
    // load list and the death card's reload were two copies of "resume the save, then the table"
    // (cc_task_dedupe-methods.md #12), and the scene paths were written out beside each
    public static class Scenes
    {
        public const string Table = "res://table.tscn";

        public const string Book = "res://launch.tscn";

        public static void Go(SceneTree tree, string scene) =>
            tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scene);

        // a saved run, picked up where it was saved; the table comes up around it. a save that
        // won't resume leaves everything where it is
        public static void Resume(SceneTree tree, SaveShelf.Saved saved)
        {
            if (saved == null || GameState.Resume(saved.Game) == null) return;

            Go(tree, Table);
        }
    }
}
