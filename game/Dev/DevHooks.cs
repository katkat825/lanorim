using System.Collections.Generic;
using System.Linq;
using Game.Access;
using Game.Builder;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // where a flag does its work
    public enum Where
    {
        Launch,   // res://launch.tscn, the main scene
        Table,    // res://table.tscn opened on its own: the demonstration table
        Play,     // at the table in a campaign (after --begin, or a loaded save)
        Builder,  // at the table in the map builder (after --build, or the book's Map builder)
        Any,      // wherever the thing it changes is
    }

    // one flag: what follows it, where it works, whether a release build still honours it, and what it does
    public sealed record DevFlag(string Flag, string Takes, Where Where, bool Release, string Does);

    // THE DEVELOPER'S AND THE CHECKS' HOOKS, IN ONE PLACE (cc_task_j-split-classes-dev-hooks.md Part 2). Every flag the
    // game takes after `--` (Flags, which the README's list is made from), and the one place each scene asks what the
    // command line adds to it: the launch screen (AtLaunch), the table (AtTable, TableLaid), play (AtPlay), the map
    // builder (AtBuilder) and the keyboard's desk (AtAccessDesk). DevArgs reads the line; this decides what it means.
    //
    // NOT IN A RELEASE BUILD. game/Dev/ isn't compiled for ExportRelease (Lanorim.csproj); the scenes' calls land on
    // game/DevHooks.Release.cs, which does nothing, so a release opens its first screen and plays. Keep the two in step:
    // `dotnet build game/Lanorim.csproj -c ExportRelease` fails otherwise.
    //
    // A NEW FLAG: a line in Flags, a member on DevArgs, and the hook here (or the class it changes reads DevArgs).
    // Then `python tools/dev_flags.py` rewrites the README's list; checks/check-flags.ps1 fails until it has
    public static class DevHooks
    {
        public static readonly IReadOnlyList<DevFlag> Flags = new DevFlag[]
        {
            // the launch screen: what comes up first
            new("--show", "<screen>", Where.Launch, true, "opens a launch screen: book, tutorials, builder, settings, credits (title otherwise); controls and create not in a release"),
            new("--open", "<campaign>", Where.Launch, true, "with --show book, that campaign's two pages; with --show builder, that campaign's maps"),
            new("--book-overlay", "", Where.Launch, false, "the menu on panels over the book, as when the pages are too small"),
            new("--ask-delete", "", Where.Launch, false, "with --show book, the first character's Delete pressed, for a picture of the question"),
            new("--page", "<step>", Where.Launch, false, "with --show create, that creation page, part filled in"),
            new("--class", "<class>", Where.Any, false, "with --show create, that class (mage otherwise); on the demonstration table, the hero's class (rogue otherwise)"),
            new("--look", "<skin,hair>", Where.Launch, false, "with --show create, that skin tone and hair colour picked (FigureSkins, from 0)"),
            new("--longest", "", Where.Launch, false, "with --show create --page, the picks that make that page longest (equipment, traits, cantrips, spells; check-layout's worst case)"),
            new("--species", "<species|lineage>", Where.Any, false, "with --show create, that species (and lineage) picked; on the demonstration table, the hero's (halfling otherwise)"),
            new("--begin", "<campaign> [class]", Where.Launch, false, "a character with every default (fighter otherwise), straight to the table; probe saves are kept apart"),
            new("--learn", "<spell,spell>", Where.Launch, false, "with --begin, those spells learned first"),
            new("--gold", "", Where.Launch, false, "with --begin, the gold instead of the gear, so the run opens in the starting shop"),
            new("--build", "<campaign|mine> [map]", Where.Launch, false, "that map, or a new one called probe_map, in the map builder; mine is My maps"),
            new("--template", "<id>", Where.Launch, false, "with --build, a new map starts as that template (dungeon_room otherwise)"),
            new("--tool", "<tool>", Where.Builder, false, "the builder opens with that tool in hand (select, paint, fill, wall, prop...)"),
            new("--picker", "<picker>", Where.Builder, false, "the builder opens with that picker or pop-up open: buildings, npcs, props, walls, problems"),
            new("--trial-panel", "", Where.Builder, false, "the builder opens its Play this map panel"),
            new("--prop-icons", "", Where.Launch, false, "renders every prop's palette thumbnail into game/ui/prop_icons/ and quits; not headless"),
            new("--mine", "<folder>", Where.Launch, false, "My campaigns and My maps kept in <folder>/campaigns and <folder>/maps, not the player's"),
            new("--wizard", "<page>", Where.Launch, false, "with --show workshop, the new-campaign wizard at that page (about, rules, first_chapter), filled in"),
            new("--tab", "<tab>", Where.Launch, false, "with --show workshop --open <campaign>, the campaign's page at that tab (about, chapters, characters, story, fights, encounters, loot, shops, lucky)"),
            new("--card", "<kind:id>", Where.Launch, false, "with --tab, that thing's card open and scrolled to (fight:goblin_camp, encounters:mill_road, loot:rat_nest, shop:millbrook_store, luck:id)"),
            new("--scroll", "<px>", Where.Launch, false, "with --card, scrolled that much further down"),
            new("--press", "<A,B>", Where.Any, false, "buttons pressed by name, in order, as each comes up (at launch and on at the table): pictures of what they open"),

            // play
            new("--auto", "", Where.Play, true, "plays itself to the end: first choices, the AutoPlayer in fights, dice thrown when asked (check-play)"),
            new("--autodice", "", Where.Play, true, "the dice thrown as soon as they're asked for, the fights left to you"),
            new("--autostory", "", Where.Play, true, "the story's lines and first choices taken as they come, past the starting shop"),
            new("--start", "<node>", Where.Play, true, "the story starts at that node (cellar, road, camp in the sample)"),
            new("--pause", "", Where.Play, true, "the pause menu opened on the hero's first turn in a fight"),
            new("--say-bark", "<pool>", Where.Play, true, "a line from that bark pool every few seconds, past the chatter and the starting shop"),
            new("--gm-screen", "<skin>", Where.Play, true, "another of the GM screens at the table"),

            // how things look, wherever they are
            new("--gm-tallness", "<0..1>", Where.Any, true, "the GM screen at that height (1 is as authored)"),
            new("--tray-footprint", "<0.4..3>", Where.Any, true, "the dice tray's footprint, scaled (DiceTray.Sizing)"),
            new("--tray-walls", "<0.4..3>", Where.Any, true, "the dice tray's walls, scaled"),
            new("--old-hero", "", Where.Any, true, "the board's own hero figure, not the class's mini"),
            new("--plain-pose", "", Where.Any, true, "every hero mini in Idle_A"),
            new("--pose", "<clip>", Where.Any, true, "every hero mini held in that clip (Attack, Sword), for a picture mid-swing"),
            new("--pose-at", "<seconds>", Where.Any, true, "with --pose, so far into the clip"),
            new("--empty-handed", "", Where.Any, true, "hero minis holding nothing"),
            new("--figure", "<0|1>", Where.Any, true, "the class's second figure"),
            new("--head", "<model>", Where.Any, true, "any head on the hero's body, its headgear off (kaykit/Mage.glb)"),

            // the picture
            new("--shot", "[file]", Where.Any, false, "saves a picture of the screen (table.png otherwise) and quits; not headless"),
            new("--after", "<frames>", Where.Any, false, "with --shot, wait so many frames first (150 otherwise)"),
            new("--size", "<WxH>", Where.Any, false, "with --shot, the window made that size first"),
            new("--zoom", "<0..1>", Where.Any, false, "with --shot, the camera at that zoom (1 all the way in)"),
            new("--quarter", "<n>", Where.Any, false, "with --shot, the table turned so many quarters"),
            new("--focus", "<name>", Where.Any, false, "with --shot, the camera following the first mini (or node) of that name"),
            new("--tray-up", "", Where.Any, false, "with --shot, wait for the tray to come up and its dice to be read"),
            new("--frames", "<n>", Where.Any, false, "with --shot, so many pictures, one a frame (tools/flicker.ps1)"),

            // the demonstration table
            new("--lineup", "", Where.Table, false, "a goblin, a bandit and a giant rat in a row beside the hero, as their minis"),
            new("--companion", "[look]", Where.Table, false, "the class's companion by the GM screen, or any look by name"),
            new("--mood", "<mood>", Where.Table, false, "with --companion, it feels that (pleased, alert, quiet, watching)"),
            new("--mood-after", "<seconds>", Where.Table, false, "with --mood, so many seconds in (2 otherwise)"),
            new("--corners", "", Where.Table, false, "a cross of walls on the map, so every kind of wall post is on the table"),
            new("--wall-styles", "", Where.Table, false, "every wall style in a row of its own, two walls, its door (where it has one), two more and a corner, on grass (cc_task_m3)"),
            new("--open-door", "", Where.Table, false, "the map's door swung open"),

            // the probes and the checks
            new("--controls", "", Where.Launch, false, "every act on the Controls page, and Change rebinds (check-controls)"),
            new("--creation", "", Where.Launch, false, "character creation, driven (check-creation)"),
            new("--map-probe", "", Where.Launch, false, "the map builder driven: a map from each template in My maps, ground, walls, a building, a scatter of trees, undo, save, reopen, copy to a campaign, and play it (check-mapbuilder; with --auto)"),
            new("--workshop-probe", "", Where.Launch, false, "the Workshop driven: a campaign made through the wizard, a chapter and a map added, saved, found in the book, and played to its one line (check-workshop; with --auto)"),
            new("--layout", "", Where.Any, false, "every launch screen, or the table's HUD, pause menu, sheet and pack, at six window sizes (check-layout)"),
            new("--cast", "<spell[:mode]>", Where.Play, false, "in a fight, the hero casts that spell (with that mode) at the nearest foe or square, and the table is watched for its line, its sight, a Command's word obeyed (check-play)"),
            new("--prone-probe", "", Where.Play, false, "in a fight, Prone lies down in its square and Stand Up stands it up (check-play)"),
            new("--access-probe", "", Where.Play, false, "presses F2, F1, Tab, Shift+Tab and F3 at the table (check-controls)"),
            new("--locale", "", Where.Table, false, "does Godot have English for every key the engine emits (check-table)"),
            new("--figures-probe", "", Where.Table, false, "stands every class's two minis up and looks at them (check-table)"),
            new("--dice", "<die>", Where.Table, false, "the fairness sweep: throws that die and tallies the faces (check-dice)"),
            new("--throws", "<n>", Where.Table, false, "with --dice, so many throws (200 otherwise)"),
            new("--handful", "<n>", Where.Table, false, "with --dice, so many at once, up to the tray's seats (8 otherwise)"),
            new("--tray-skin", "<skin>", Where.Table, false, "with --dice, sweep another tray skin"),
            new("--alternate", "<die>", Where.Table, false, "with --dice, a handful of that die between the counted throws"),
        };

        // --- the launch screen ------------------------------------------------------------------------------------------

        // the start as the command line asks. false only when it asks for nothing this registry does, and then the
        // launch screen opens its first screen itself
        public static bool AtLaunch(Launch launch)
        {
            // a probe or a start that takes the screen over
            if (DevArgs.ControlsProbe)
            {
                launch.AddChild(new ControlsProbe { Name = "ControlsProbe" });
                return true;
            }

            if (DevArgs.CreationProbe)
            {
                launch.AddChild(new CreationProbe { Name = "CreationProbe" });
                return true;
            }

            if (DevArgs.PropIcons)
            {
                launch.AddChild(new PropIcons { Name = "PropIcons" });
                return true;
            }

            // a My campaigns and a My maps of their own, never the player's (screenshots, the Workshop's probe)
            if (DevArgs.Mine is { } mine)
            {
                Game.Campaigns.CampaignFolders.MineFolder = System.IO.Path.Combine(mine, "campaigns");
                MapBuilderPage.LibraryFolder = System.IO.Path.Combine(mine, "maps");
            }

            if (DevArgs.WorkshopProbe)
            {
                WorkshopProbe.AtLaunch(launch);
                return true;
            }

            if (QuickStart.Building(launch)) return true;

            if (DevArgs.Begin != null)
            {
                GameState.SaveProbesApart();
                QuickStart.Begin(launch, DevArgs.Begin, DevArgs.BeginClass, DevArgs.Learn, DevArgs.Gold);
                return true;
            }

            // the first screen, and what comes after it
            launch.OverlayBook = DevArgs.BookOverlay;

            string screen = DevArgs.Show;

            // creation needs a free character slot, which a player's own saves may have taken: the layout probe and a
            // picture of creation keep saves of their own (cc_task_m3 Part 1: the creation pages were never checked here)
            if (DevArgs.LayoutProbe || screen == "create") GameState.SaveProbesApart();

            if (screen == "controls") QuickStart.Controls(launch);
            else if (screen == "create") QuickStart.Creating(launch, DevArgs.Page, DevArgs.Class);
            else launch.ShowFirst(screen, DevArgs.Open);

            // the wizard at a page, or a campaign's page at a tab (pictures of them)
            if (screen == Game.Workshop.WorkshopPage.PageId) QuickStart.Workshop(launch, DevArgs.Wizard, DevArgs.Tab, DevArgs.Card);

            // the question asked (it asks; nothing is deleted)
            if (screen == "book" && DevArgs.AskDelete)
                Nodes.Under<Button>(launch).FirstOrDefault(b => b.Text == Ui.Say(Content.Screens.CampaignBook.DeleteKey))
                     ?.EmitSignal(BaseButton.SignalName.Pressed);

            if (DevArgs.Shot) launch.AddChild(Shot.Asked());

            if (DevArgs.Press != null) launch.AddChild(new Presser { Name = "Presser" });

            if (DevArgs.LayoutProbe) launch.AddChild(Layouts.OfLaunch(launch));

            return true;
        }

        // --- the table ---------------------------------------------------------------------------------------------------

        // a probe that takes the table over, before anything is wired to the tray. true when one did
        public static bool AtTable(Game.Table.Table table)
        {
            if (DevArgs.LocaleProbe)
            {
                table.AddChild(new LocaleProbe { Name = "LocaleProbe" });
                return true;
            }

            if (DevArgs.FiguresProbe)
            {
                table.AddChild(new FiguresProbe { Name = "FiguresProbe", Board = table.Board });
                return true;
            }

            if (DevArgs.Dice)
            {
                table.AddChild(DiceProbe.Asked(table.Tray));
                return true;
            }

            return false;
        }

        // the table is laid: with nothing to play or build, the demonstration; and a picture, last, so it has everything
        public static void TableLaid(Game.Table.Table table)
        {
            if (GameState.Run == null && GameState.Building == null)
                table.AddChild(new DemoTable { Name = "Demo", Table = table });

            if (DevArgs.Shot) table.AddChild(Shot.Asked());
        }

        // --- play, the builder, the keyboard ----------------------------------------------------------------------------

        public static void AtPlay(PlayDirector director)
        {
            if (DevArgs.Press != null) director.AddChild(new Presser { Name = "Presser" });

            if (DevArgs.WorkshopProbe) WorkshopProbe.AtPlay(director);

            if (DevArgs.ProneProbe) director.AddChild(new ProneProbe { Name = "ProneProbe", Combat = director.Combat });

            if (DevArgs.Cast is { } cast)
                director.AddChild(new CastProbe
                {
                    Name = "CastProbe", Combat = director.Combat,
                    Spell = cast.Split(':')[0], Mode = cast.Contains(':') ? cast.Split(':')[1] : null,
                });

            if (DevArgs.LayoutProbe) director.AddChild(Layouts.OfPlay(director));
        }

        public static void AtBuilder(MapBuilder builder)
        {
            if (DevArgs.WorkshopProbe) builder.AddChild(new WorkshopProbe { Name = "WorkshopProbe", Builder = builder });

            if (DevArgs.MapProbe) builder.AddChild(new MapBuilderProbe { Name = "MapBuilderProbe", Builder = builder });

            if (DevArgs.Tool != null && Core.Words.EnumWords.TryName(DevArgs.Tool, out Content.Maps.MapTool tool))
            {
                builder.Editor.Tool = tool;
                builder.CallDeferred(MapBuilder.MethodName.Chose);
            }

            if (DevArgs.TrialPanel) builder.CallDeferred(MapBuilder.MethodName.ShowTrial);

            // `--picker`: the bar's button for it, pressed (cc_task_m2 Part 8)
            Access.Act? picker = DevArgs.Picker switch
            {
                "buildings" => Access.Act.BuildingsPicker,
                "npcs" => Access.Act.NpcsPicker,
                "props" => Access.Act.PropsPicker,
                "walls" => Access.Act.WallsAndDoors,
                "problems" => Access.Act.BuilderProblems,
                _ => null,
            };

            if (picker is Access.Act act) Callable.From(() => builder.Do(act)).CallDeferred();
        }

        public static void AtAccessDesk(AccessDesk desk)
        {
            if (DevArgs.AccessProbe) desk.AddChild(new AccessProbe { Name = "AccessProbe", Desk = desk });
        }
    }
}
