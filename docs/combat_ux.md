# Combat UX — how a fight is played at the table

*Design record, written 2026-09-24 (Tier 3a of the unattended run). What the player sees and presses in a
fight, and which piece of code answers each thing. The rules underneath are `core/`; the command surface is
`content/Combat/CombatSession.cs`; the HUD's view model is `content/Screens/CombatHud.cs`; the Godot layer is
`game/Combat/`. Where this doc and the code disagree, the doc is the intent — flag it.*

## The one rule

**Nothing on screen decides anything.** Every button reads the session (`Options`, `LegalTargets`,
`Preview`, `Reachable`, `PathTo`) and every press is a session call (`Select`, `Confirm`, `Move`, `Cancel`,
`EndTurn`). A thing the HUD shows that the session didn't say is a bug.

## Layout

```
┌ Log ▸ (top left) ─────────┐ ┌──── turn-order strip (top centre) ─────┐      Q and E turn the table…
│ Hit: you hit the goblin   │ │ ▸ Hero ▬▬▬ 14/20   Goblin ▬▬▬   Round 2 │      Hit! 17 against Armor Class 15
│ (17 against Armor Class…) │ └─────────────────────────────────────────┘      Your dice are waiting - Space…
└───────────────────────────┘

                  the board (the map, the biggest thing on screen)             the tray

                ┌ action bar (bottom centre, flush to the edge) ───────────────────┐
                │ 1 Longsword  2 Second Wind  Spells ▾  More actions ▾ │ End Turn  │
                │ Actions ● ○   Bonus action ●   Reaction ●   Moved 3 of 6 squares │
                └──────────────────────────────────────────────────────────────────┘
```

*(2026-09-30, `cc_task_table-ui-minis-zoom-damage.md` 3: the canvas is 1920x1080, the panels are flush to their
edges, End Turn is the bar's right end, and the log is top left. Sizes are `res://ui/hud_layout.tres`.)*

- **Turn-order strip, top centre** — every combatant in initiative order: name and a health bar, the hero's and a
  foe's alike, with the hero's numbers beside theirs (the numbers are the player's); the current turn marked.
  Downed combatants stay, greyed. `CombatHud.Order`.
- **Action bar, bottom centre** — what a turn reaches for as buttons (attacks, features, items, a shape, Surge,
  Flee), the spells in one menu and the manoeuvres every creature has in another (*More actions*).
  **An option that can't be taken right now is left off the bar**, and the pips say why (no action left); the
  menus still list it, greyed, with its reason as the tooltip: a menu is where you look for what exists. With
  `HudLayout.HideUnavailable` off it stays on the bar instead, hollow, faded and without its number. *(Kathleen,
  2026-09-30: "clearly distinct or not there". This replaces "greyed, never hidden".)*
  Under the bar the **pips**, sentence case: a filled dot for each one left and a hollow one for each spent —
  actions, bonus action, reaction — and the movement as "Moved 3 of 6 squares". `CombatHud.Bar`, `Actions`,
  `ActionsGiven`, `SquaresMoved`, `SquaresGiven`, `CombatHud.Pips`.
- **End Turn** — the bar's right end; also **Space** (see *Keys*).
- **Combat log, top left** — the last few lines, open to the whole fight with **L** or its title. `FightLog`.
  Collapsed by default; the settings remember. A new fight starts a new log.
- **The tray** sits beside the board and **comes to the player** for their own rolls (see *Dice*).
- **The top corners** (2026-10-01): the log top left, the turn hint, the tray's caption and notices top right,
  each `HudLayout.LogWidth` / `HintWidth` wide; the turn strip lives between them and the action bar wraps
  rather than run under them. `checks/check-layout.ps1` holds every screen to this at six window sizes.

## A turn

1. **The hero's turn starts** — the strip raises the hero, the camera settles on them, the bar lights up,
   and the **reachable squares** show on the board (`Board.ShowReach` via `CellLights`, from
   `CombatSession.Reachable`).
2. **Moving** — hovering a lit square draws the **path** (`PathTo`) and its **cost** in squares
   (`ui.combat.path_cost`). Clicking it walks the mini there (`Move`); the lit squares shrink to what's left.
   Difficult ground costs double and the path shows it.
3. **Choosing an action** — click a button or press its number (`Select`). What happens next depends on
   `ActionOption.Targeting`:
   - **Creature** — legal targets are ringed (`LegalTargets`). Hovering one shows the **preview**:
     hit chance (or the target's chance to fail the save), the damage dice, **expected damage**
     (`Preview.HitChance`, `FailChance`, `Damage`, `ExpectedDamage`). Click to confirm. A click on a standing
     mini counts as its square (2026-10-01; it used to land on the square behind it). A spell that can go on the
     hero (Mage Armor, Cure Wounds) also shows **On yourself** on the bar, and **Enter** does the same.
   - **Creatures** (Magic Missile, Eldritch Blast, Bless) — click targets up to the option's `MaxTargets`, then
     confirm with **Enter** or the button.
   - **Square** (Fireball) — the template follows the pointer, clipped to `LegalSquares`; everything caught is
     ringed and the preview counts them. Click to confirm.
   - **Direction** (Burning Hands, Lightning Bolt) — the template points from the hero; **the wheel, Q or E**
     turn it a quarter (`Rotate`), and **Shift and the wheel** zoom while it is aimed; a line over the bar says so. A cone that catches nobody previews **0** damage.
   - **None** (Dash, Disengage, Second Wind, a potion) — happens on the press.
   A spell with **modes** or a **damage choice** (Chromatic Orb) asks first, in a small menu on the bar.
   A spell that can be **upcast** gets a slot-level picker; the preview updates with it.
4. **Cancel** — **right-click or Esc** at any point before confirming (`Cancel`). Nothing is spent.
5. **Confirming** — the hero's dice are thrown on the tray (unless *skip physical dice* is on), the mini
   strikes, the result lands, the log says it, the pips drop. `Confirm`.
6. **End Turn** — the button or Space (`EndTurn`). The enemies play (see below) and the next hero turn starts.

## Reactions

A reaction the hero could take (Shield, Counterspell, an opportunity attack, a smite on a hit) is governed by
the **reaction policy** for that reaction, set in **Settings → Reactions**. A smite rides the same policy but
spends the **bonus action**, not the reaction: Divine Smite's casting time is a bonus action taken right after
the hit. *(2026-10-03: aligned with SRD 5.2.1)*

| Policy | What happens |
|---|---|
| **Auto** (the default) | the engine's judgement: take it when it helps (`ReactionChoosers.WhenItHelps`). Smites are never cast unasked. |
| **Always** | take it whenever it's legal |
| **Ask** | the fight **stops** and asks — see below |
| **Never** | never |

**The Ask prompt** is a small card over the board, **with no timer**:

> **Cast Shield?** AC 14 → 19 turns the hit.
> **[Yes]  [No]**

`ReactionQuestion` gives the card everything: the reaction's name, `ArmorClassNow` → `ArmorClassWith`,
and whether that `TurnsTheHit`. Enter is Yes, Esc is No. The fight is paused behind it (the rules are waiting
on `IReactionAsker.Ask`).

## Enemy turns

- The **camera follows** each enemy as it moves (setting: *Camera follows enemies*) and returns to the hero.
- Each move and strike plays at the **enemy-turn speed** (setting: slow / normal / fast / instant;
  `GameSettings.SecondsPerEnemyStep`). **Space** skips to the end of the enemies' turns (the moves land at
  once; the log has everything).
- **The GM's rolls happen behind the screen**: a quiet roll sound from behind the GM screen, the result in the
  log, no dice on the tray.

## Dice

The player's own rolls — attacks, damage, saves, checks, death saves — are **thrown on the tray**: the
faces on the felt are the numbers (`TableResolver` → `IDiceSource` → the tray). **Space**, or a **click on the
tray**, throws when the tray is waiting. With **Skip physical dice** on, they're rolled digitally and shown on the
tray card only. Advantage throws two d20s and both stay on the felt. A spell's damage is the caster's roll, so the
hero's Fire Bolt and Burning Hands are thrown on the tray too; the target's saving throw is the GM's.

**Every attack says what happened, in order** (2026-09-30): the to-hit throw, then **Hit**, **Miss** or
**Critical** with the armor class (a save: **Saved** or **Failed** with the DC), then, on a hit, the damage
throw, then the damage line: "Greataxe hits Giant Rat: 1d12+3, for 9 Slashing damage." The verdict also stands
under the turn hint as the tray's caption, heading "Your dice are waiting" while the damage dice wait
(`ICombatObserver.Judged` and `Dealt`, `FightLog`, `PlayDirector.Verdict`). A hero's roll that falls back to
digital dice for any reason but the player's own setting logs a warning.

**The tray comes to you** (2026-10-01, `updated_decisions.md`): asked for the hero's dice, the tray lifts toward
the camera (tweens only, never while a die is moving), the dice are thrown up there, and it goes back
`HoldSeconds` (1.5) after they're read. The GM's rolls behind the screen never touch it. Settings → Game
"Bring the dice tray to me" turns it off. `TrayLift` in `table.tscn`, its dials in the inspector.

**Every cast says what it was and what it changed** (2026-10-01): "Tess casts Mage Armor on Tess." before its
dice, "Tess's Armor Class is now 14 (was 11)." after, and the caption "Mage Armor: your Armor Class is now 14"
(`ICombatObserver.Casts` / `Cast`, `Change`). **Before a fight**, a caster with a spell that outlasts it (Mage
Armor, Aid) is offered it on a "Before the fight" card, paid for as usual (`BeforeTheFight`).

## Fleeing

The open edge squares a hero can leave the map from carry an **edge marker** (`CombatSession.Exits`). Walking
onto one enables **Flee** on the bar; taking it ends the fight as fled (`Outcome.Fled`), and the story takes the
fled branch.

## Keys

| Key | Does |
|---|---|
| **1–9** | the action bar's options, in a fixed order: the bar's buttons from the left, then the Spells menu, then More actions. An option counts whether or not it can be taken now, so a number never moves mid-turn; one that only appears partway through a turn (a Light weapon's bonus attack, Flee) comes last (`CombatHud.Numbered`) |
| **Space** | the "go on" key: throw the dice when the tray is waiting; on the hero's turn **End Turn**; during the enemies' turns, skip to the end of them |
| **Esc / right-click** | cancel the choice in hand; with nothing in hand, the pause menu |
| **Q / E** | turn a direction template; with none in hand, turn the table |
| the wheel, a trackpad pinch, **- / =**, the number pad's **+ / -** | zoom, eased (`TableCamera.ZoomSeconds`); while a line or cone is aimed the wheel turns it and **Shift+wheel** zooms |
| **Enter** | confirm (a multi-target choice, the Ask prompt's Yes) — the Access layer's *Touch* act |
| **Tab / Shift+Tab** | move the focus through the bar's options, then the legal targets or squares — the Access layer's *Reach next / back* acts |
| **L** | open / close the combat log (the *Toggle log* act) |
| arrow keys + Enter | move a cursor square by square and confirm it — the keyboard-only way to move and aim |

Space ending the turn is only ever **one** press after the last throw has landed and been read — a throw
still tumbling swallows the press rather than ending the turn under it.

The acts (`game/Access/Act.cs`) are bound in the InputMap and listed, with their current keys and a **Change**
button, in **Settings → Controls** (2026-09-30), which saves a rebinding to `user://settings.txt`. The 1–9 hotkeys,
Space's "go on" and Esc are listed there but aren't acts. Not built yet: the arrow-key cursor, and Tab / Shift+Tab
walking the targets (Tab moves Godot's own focus), Help (F1), Where are we (F2), Read aloud (F3) and the `Narrator`
itself. The Controls page says "not working yet" beside each.

## What's built and what isn't (2026-09-24)

- Built and tested (no Godot): the session, previews, legal targets and squares, reachability and paths,
  reactions with policies and the Ask question, the HUD view model, the log, settings, the auto-player.
- Godot: see `game/Combat/` and the run log for what exists; **everything visual needs Kathleen's eyes**.
