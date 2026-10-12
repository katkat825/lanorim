using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    // THE HERO'S MINI, BY CLASS AND FIGURE (cc_task_g Part 1, cc_task_h Parts 1-2). Every species stands as its
    // class's body for now. The bodies are KayKit Adventurers 2.0 (CC0) in game/models/minis/kaykit/;
    // barbarian_male.glb, fighter.glb and cleric.glb are Kathleen's own (the Barbarian without its bear hat, the
    // Knight without its helmet, visor and cape, the Mage without its hat and cape). None carries a clip, so each is
    // given the pack's Rig_Medium libraries (MiniRigs) and held in one frame of its class's pose (Kathleen, 2026-10-08:
    // "yes, accept suggestions").
    //
    // TWO FIGURES A CLASS, the player's pick at creation (Hero.Figure): the class's own head, and another
    // character's head on the same body (FigureParts). They're told apart by the figure, never by a word for it.
    // The second heads are first picks from _design_docs/shots_2026-10-08h/h_heads_sheet.png, for Kathleen to change.
    //
    // EVERY PICK IS ONE LINE. HudLayout.HeroByClass off puts rogue_v3 (the board's HeroFigure) back for every class.
    public static class HeroMinis
    {
        // the KayKit bodies crown to sole: rogue_v3 stands 232 mm to its crown, the goblin 230 (RUN_LOG_2026-10-08)
        const float Tall = 0.23f;

        const string Rig = "Rig_Medium";

        // a borrowed head goes on bare: the body's own headgear comes off with the body's own head
        static readonly string[] Headgear = { "Hat", "Helmet", "HelmetVisor" };

        static readonly (string, string)[] Axe = { ("handslot.r", "axe_2handed") };
        static readonly (string, string)[] SwordAndBoard = { ("handslot.r", "sword_1handed"), ("handslot.l", "shield_round") };
        static readonly (string, string)[] Dagger = { ("handslot.r", "dagger") };
        static readonly (string, string)[] Staff = { ("handslot.r", "staff") };
        static readonly (string, string)[] Bow = { ("handslot.l", "bow") };
        static readonly (string, string)[] Book = { ("handslot.l", "spellbook_open") };

        static MiniLook Body(string file, string pose, (string, string)[] holds) =>
            new(file, Tall, pose, Mini.RestingFrame, Rig, holds, Crown: true);

        static MiniLook Wearing(this MiniLook body, string head) => body with { Head = head, Leave = Headgear };

        static readonly MiniLook Barbarian = Body("kaykit/barbarian_male.glb", "Melee_2H_Idle", Axe);
        static readonly MiniLook Fighter = Body("kaykit/fighter.glb", "Melee_Blocking", SwordAndBoard);
        static readonly MiniLook Paladin = Body("kaykit/Knight.glb", "Melee_Blocking", SwordAndBoard);
        static readonly MiniLook Rogue = Body("kaykit/Rogue.glb", "Sneaking", Dagger);
        static readonly MiniLook Mage = Body("kaykit/Mage.glb", "Ranged_Magic_Raise", Staff);
        static readonly MiniLook Druid = Body("kaykit/Ranger.glb", "Ranged_Bow_Idle", Bow);
        static readonly MiniLook Cleric = Body("kaykit/cleric.glb", "Ranged_Magic_Raise", Book);

        // the second heads, from the heads sheet: Kathleen's two examples (the Mage's head on the Barbarian, the Ranger's
        // on the Paladin - though the Ranger's head has a goatee), and elsewhere the head that contrasts most with the
        // class's own: long dark hair or a ginger braid against a bald head, a bald head against hair
        static readonly Dictionary<string, MiniLook[]> ByClass = new Dictionary<string, MiniLook[]>
        {
            ["barbarian"] = new[] { Barbarian, Barbarian.Wearing("kaykit/Mage.glb") },
            ["fighter"] = new[] { Fighter, Fighter.Wearing("kaykit/Rogue.glb") },
            ["paladin"] = new[] { Paladin, Paladin.Wearing("kaykit/Ranger.glb") },
            ["rogue"] = new[] { Rogue, Rogue.Wearing("kaykit/Knight.glb") },
            ["mage"] = new[] { Mage, Mage.Wearing("kaykit/Ranger.glb") },
            ["druid"] = new[] { Druid, Druid.Wearing("kaykit/Rogue.glb") },
            ["cleric"] = new[] { Cleric, Cleric.Wearing("kaykit/Knight.glb") },
        };

        // how many figures a class offers
        public const int Figures = 2;

        // the pose every body is offered against
        public const string PlainPose = "Idle_A";

        public static IEnumerable<string> Classes => ByClass.Keys;

        // a class's figure (0 or 1; anything else is the first), or null for a class with no row
        public static MiniLook LookOf(string classId, int figure = 0) =>
            classId != null && ByClass.TryGetValue(classId, out MiniLook[] looks)
                ? looks[figure >= 0 && figure < looks.Length ? figure : 0]
                : null;

        // the class's figure and pose, or null for the board's own HeroFigure: a class with no row, or the switch off.
        // For pictures and comparisons: `--old-hero` is the switch off for one run, `--plain-pose` every body in
        // Idle_A, `--empty-handed` nothing held, `--figure 1` the second figure, `--head kaykit/Mage.glb` any head
        // on the body with its headgear off (the heads sheet)
        public static (PackedScene Model, MiniLook Look)? For(string classId, int figure = 0)
        {
            if (!Game.Screens.HudLayout.Current.HeroByClass || DevArgs.OldHero) return null;

            if (DevArgs.Figure is int asked) figure = asked;

            MiniLook look = LookOf(classId, figure);

            if (look == null) return null;

            if (DevArgs.PlainPose) look = look with { Pose = PlainPose };
            if (DevArgs.EmptyHanded) look = look with { Holds = null };
            if (DevArgs.Head is { } head) look = look.Wearing(head);

            return MiniModels.Of(look);
        }
    }
}
