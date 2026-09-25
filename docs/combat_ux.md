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
 ┌──────────────── turn-order strip (top centre) ────────────────┐
 │  [Hero ♥ 14/20]  [goblin 7/7]  [goblin boss 21/21]   Round 2  │
 └───────────────────────────────────────────────────────────────┘

                  the board (the map, three quarters of the frame)            the tray

 ┌ combat log (bottom left, collapsible) ┐        ┌ action bar (bottom centre) ┐  ┌ End Turn ┐
 │ goblin hits you for 5                 │        │ 1 Longsword  2 Fire Bolt … │  │ (Space)  │
 └───────────────────────────────────────┘        │ ●● actions ● bonus ● react │  └──────────┘
                                                  └────────────────────────────┘
```

- **Turn-order strip, top centre** — every combatant in initiative order: name, hit points (a foe's as a bar
  only; the numbers are the player's), the current turn raised. Downed combatants stay, greyed. `CombatHud.Order`.
- **Action bar, bottom centre** — the session's options, **1–9** on the first nine. An option that can't be
  taken is **greyed, never hidden**, and its tooltip is the reason (`ActionOption.WhyNotKey` → `ui.combat_why.<reason>.name`).
  Under it the **pips**: actions (usually two; three with Extra Attack), bonus action, reaction.
  `CombatHud.Bar`, `Actions`, `BonusActions`, `Reactions`.
- **End Turn, bottom right** — also **Space** (see *Keys*).
- **Combat log, bottom left** — the last few lines, open to the whole fight with **L**. `CombatHud.LastLines`,
  `FightLog`. Collapsed by default; the settings remember.
- **The tray** stays where it is; the player's own rolls are thrown on it (see *Dice*).

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
     (`Preview.HitChance`, `FailChance`, `Damage`, `ExpectedDamage`). Click to confirm.
   - **Creatures** (Magic Missile, Eldritch Blast, Bless) — click targets up to the option's `MaxTargets`, then
     confirm with **Enter** or the button.
   - **Square** (Fireball) — the template follows the pointer, clipped to `LegalSquares`; everything caught is
     ringed and the preview counts them. Click to confirm.
   - **Direction** (Burning Hands, Lightning Bolt) — the template points from the hero; **scroll, Q or E**
     turn it a quarter (`Rotate`). A cone that catches nobody previews **0** damage.
   - **None** (Dash, Disengage, Second Wind, a potion) — happens on the press.
   A spell with **modes** or a **damage choice** (Chromatic Orb) asks first, in a small menu on the bar.
   A spell that can be **upcast** gets a slot-level picker; the preview updates with it.
4. **Cancel** — **right-click or Esc** at any point before confirming (`Cancel`). Nothing is spent.
5. **Confirming** — the hero's dice are thrown on the tray (unless *skip physical dice* is on), the mini
   strikes, the result lands, the log says it, the pips drop. `Confirm`.
6. **End Turn** — the button or Space (`EndTurn`). The enemies play (see below) and the next hero turn starts.

## Reactions

A reaction the hero could take (Shield, Counterspell, an opportunity attack, a smite on a hit) is governed by
the **reaction policy** for that reaction, set in **Settings → Reactions**:

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
faces on the felt are the numbers (`TableResolver` → `IDiceSource` → the tray). **Space** throws when the tray
is waiting. With **Skip physical dice** on, they're rolled digitally and shown on the tray card only.
Advantage throws two d20s and both stay on the felt.

## Fleeing

The open edge squares a hero can leave the map from carry an **edge marker** (`CombatSession.Exits`). Walking
onto one enables **Flee** on the bar; taking it ends the fight as fled (`Outcome.Fled`), and the story takes the
fled branch.

## Keys

| Key | Does |
|---|---|
| **1–9** | the action bar's options |
| **Space** | the "go on" key: throw the dice when the tray is waiting; on the hero's turn **End Turn**; during the enemies' turns, skip to the end of them |
| **Esc / right-click** | cancel the choice in hand; with nothing in hand, the pause menu |
| **Q / E**, scroll | turn a direction template; with none in hand, turn the table |
| **Enter** | confirm (a multi-target choice, the Ask prompt's Yes) — the Access layer's *Touch* act |
| **Tab / Shift+Tab** | move the focus through the bar's options, then the legal targets or squares — the Access layer's *Reach next / back* acts |
| **L** | open / close the combat log |
| **- / =** | zoom |
| arrow keys + Enter | move a cursor square by square and confirm it — the keyboard-only way to move and aim |

Space ending the turn is only ever **one** press after the last throw has landed and been read — a throw
still tumbling swallows the press rather than ending the turn under it.

All of them are **acts** in `game/Access/Act.cs`, bound in the InputMap and rebindable on the Access page; the
screen reader (`Narrator`) says the focused option, its reason when greyed, the preview, and each log line.

## What's built and what isn't (2026-09-24)

- Built and tested (no Godot): the session, previews, legal targets and squares, reachability and paths,
  reactions with policies and the Ask question, the HUD view model, the log, settings, the auto-player.
- Godot: see `game/Combat/` and the run log for what exists; **everything visual needs Kathleen's eyes**.
