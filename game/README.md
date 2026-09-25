# game/ — the Godot layer

The only part of the project that touches Godot. Everything under `core/` and `content/` is pure
C# and stays that way; this is where those become a table you can look at.

Open it in Godot. The main scene is **`launch.tscn`** (the title and the campaign book), which goes to **`table.tscn`** to play.

## What is here

```
Campaigns/  finds campaign folders on disk and loads their locale; discovery only

Screens/    every screen, built in code from containers and the theme (ui/lanorim_theme.tres)
  Launch.cs       the main scene (launch.tscn): title, the campaign book, tutorials, settings, creation
  CreationScreen  one page a creation step
  TableScreens    the cards over the table: level-up, pack + merchant, sheet, menus, the Ask prompt
  CombatHudUi     the turn strip, the action bar and pips, End Turn, the log
  DialoguePopup   the story's card: who, the line, Continue or the choices

Play/       the campaign at the table
  GameState       what survives a scene change: campaigns, saves, settings, the run
  PlayDirector    the story, fights, shops, level-ups, death and the end, on the table
  CombatDirector  the fight: board steps paced at the enemy speed, the hero's hands
  RulesThread     the rules on their own thread, so the tray can throw mid-attack
  TrayDice        the hero's dice ARE the tray's

Table/      the table itself
  Table.cs        the seam between the things on the table and the rules underneath
  GmScreen.cs     the GM's screen beyond the board's far edge; the hidden rolls rattle behind it
  TableCamera.cs  three-quarter overhead, ninety-degree snaps, handheld drift
  TableView.cs    tilts a card with words on it toward the player
  Shot.cs         saves a picture of the table and quits

Dice/       the dice. ART_DIRECTION says build these first, and they are
  DieSolid.cs     d4 d6 d8 d10 d12 d20 generated as geometry - mesh, hull, numerals and the
                  face table are ONE description, so a die cannot lie about what it shows
  DieBody.cs      the rigid body: throw, settle, cocked, lost, and the recovery from each
  DieParts.cs     turns a solid into a mesh, a convex hull and one Label3D per numeral
  DiceProbe.cs    is the physics d20 fair? (check-dice.ps1)

Tray/       the vessel the dice land in
  DiceTray.cs     measures the scene, dresses it from a skin, throws, reads the felt
  TrayRoll.cs     what came up - and AsRng(), which is the whole seam to the rules
  skins/          a tray is a .tres: two surfaces, each with a look, a bounce and a sound

Board/      the map on the table
  Board.cs        draws a MapLayout, stands a Mini on every occupied square
  BoardTiles.cs   floor, rough, walls on the lines, doors - models if it has them, boxes if not
  Mini.cs         a piece is an OBJECT THAT GETS MOVED: it arcs, sets down, strikes, wobbles,
                  topples and stays down
  CellLights.cs   the squares you may walk to, held up while you decide

audio/      dice impacts driven by how hard they actually hit
Access/     captions, contrast, legible text, the narrator, key bindings
Localization/   the one place a key turns into text, and the probe that proves Godot has them
shaders/    painted_miniature.gdshader - the visual signature, and painted.tres to drop on things
models/     minis, the dice trays and the GM screens (see ../THIRD_PARTY.md for where each came from)
textures/   table, tray, map and die materials
fonts/ ui/  the fonts and the Kenney panels, and ui/lanorim_theme.tres - the project's theme
```

## The one thing to understand

**The tray is the random number generator.** Nothing rolls a number and then animates a die onto
it. The die is thrown, the felt is read, and the faces go to core through a `ScriptedRng`:

```csharp
var resolver = new StandardResolver(roll.AsRng());
Attempt attempt = Checks.Check(resolver, actor, Skill.Stealth, dc, advantage);
```

That is the entire coupling between the table and the rules, and it is why `check-dice.ps1`
measures a tumbling solid rather than just the generator. A tray that favoured its low faces would
be a game that favoured them, and every test in the repo would still be green.

## Running it headlessly

The scene takes flags after `--`, which is how the checks drive it:

```bash
godot --headless --path game res://table.tscn -- --locale
godot --headless --path game res://table.tscn -- --dice d20 --throws 500
godot --path game res://table.tscn -- --shot table.png
```

The last one is not headless on purpose: headless has no renderer, and a picture is the point.

**After editing `locale/game.csv`, re-import:**

```bash
godot --headless --path game --import
```

Godot reads the compiled `.translation` beside the CSV, so a new key is missing at runtime while
every test still passes. `--locale` is the check that catches it.

## What is a stand-in

Most of the visuals. The board now stands real minis (the hero is `rogue_v2.glb`, the enemy
`goblin_male.gltf`), the tray is the modeled one and the table has a wood material, but the tiles
are still boxes and the lamp is a directional light. The GM-screen models are imported and not yet
on the table. Anything not yet judged by eye in the editor should be treated as a stand-in.

The shader is the exception: it is the real one, harvested whole, and every number in it is a dial
meant to be turned against a screenshot.
