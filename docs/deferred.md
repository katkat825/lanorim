# deferred to future features
- feats (SRD "ASI or feat" levels give the ability score improvement only in v1; the player chooses where the +2 goes)

- unprompted/freeform skill checks

- possible encumberance (only if player-base really wants it. personally, I hate dealing with carrying-capacity issues)

- possible microtransactions. premium tier currency can be grinded for. nothing is gated behind only real-life money, but you can spend real-life money if you don't want to grind for the premium tier currency

- container-specific inventory

- somehow losing 1/some but not all inventory containers

- allow inventory containers to possess properties other then inventory slots (requires managing possible overflow of inventory if Bob has every inventory slot filled and a saddle with 30 inventory slots and wants this new saddle that has a speed boost but only 25 inventory slots)

- discarded items are dropped and can be retrieved later

- offer a way for an item to be equipped in multiple slots at player choice (skills that allow use of a main-hand weapon in an off-hand. items that can be either main-hand or off-hand)
    - *2026-10-03: the off hand itself is built (a one-handed Light weapon, for the Light bonus attack: `inventory_decisions.md`). What stays parked is any other main-hand item in the off hand, and skills that allow it.*

- player can mark favorite items that are then not sellable and have a favorite icon (also player can toggle off the favorite status on an item)

- allow campaigns to decide that merchant(s) has a finite amount of gold

- allow campaigns to decide that merchant(s) will only buy certain categories/items

- allow items to be restricted to properties beyond class and level

- campaigns choose if player can multi-class

- campaigns can have time-sensitive consequences (like a quest where you're chasing down a thief can be negatively impacted by pausing for a rest or detouring to a different quest for a while)

- on death allow the player to choose to reload last save or create a new character

- allow player to create a new character in a campaign and continue the campaign with the new character

- inventory slots come from items:
    - standard clothing gives you 5 slots
    - starting backpack gives you 20 slots
        - therefore a starting character has 25 inventory slots
    - backpacks have a defined number of inventory slots.
        - backpacks can exist at different capacity/quality levels.
        - a backpack's capacity does not automatically increase as the character levels.
        - increasing backpack capacity requires acquiring a new/better backpack through purchase, loot, or another campaign-defined reward.
    - clothing items can potentially also give increased slots, but that's rare (for example, a quest might reward a rogue with a cloak that gives +2 inventory slots from hidden pockets for a total clothing slot count of 7)
    - a horse can provide an additional 20 inventory slots through saddlebags.
    - a cart can provide an additional 50 inventory slots.

- items, skills, etc. can potentially have the ability to increase hp, ac, ability scores, etc. beyond max
    - can also have a certain number of uses/charges/whatever per rest (per long rest or per short/long rest)
    - can also have the ability to intercept death. at 0 hp instead of a death saving throw you instantly have 5 hp
        - if more than one item/skill/whatever intercepts death player chooses which item to use

- merchants can offer a discount on a specific item(s) or on everything. this is determined in the campaign and can be conditional on completing a quest, time spent in the campaign, player level, etc.

- merchant should have a game-level (rather than campaign-level) pool of dialog for inventory being full. create tests to enforce the dialog pool

- hovering over an item shows the side by side stats of both the currently equipped item and the non-equipped item. or just the stats of the single item if hovering over an equipped item. if multiple items can be compared (for example currently equipped a main-hand sword and an off-hand shield, when hovering over a two-handed weapon you see both of the existing equipped items)

- merchant quality of life
    - merchant UI supports selecting multiple items to buy/sell in a single transaction.
    - stackable items allow the player to select the quantity to buy/sell.
    - UI displays the total gold the player will spend/receive before confirming the sale.
    - equipped items can be included in a bulk sale, but each equipped item still requires explicit confirmation before the transaction can complete.
    

- deferred spells (Kathleen, 2026-09-25). SRD 5.2.1 spells taken out of the functioning v1 set. They're not learnable, not castable and not on any class list. The work isn't lost: each one's data is in `content/deferred/spells/deferred.json`, which the loader doesn't ship. Their English was taken out of the locale; each entry's `note` says what the spell did. Like any unbuilt SRD spell, each can still appear as a reference card under its SRD name.
    - Feather Fall (level 1): v1 has no falling or altitude, so its reaction has nothing to answer.
    - Heat Metal (level 2): items and statblocks don't say what is metal.
    - Plane Shift (level 7): v1 travels only to campaign-authored places and has no other planes.
    - Reverse Gravity (level 7): no altitude or falling damage.
    - Antimagic Field (level 8): suppressing every spell and magic item inside it is a large engine change.
    - Earthquake (level 8): no fissures, collapsing structures or physics sandbox.
    - True Polymorph (level 9): curated forms and authored hooks only, not free transformation.

- dim light (2026-09-27, `cc_task_dedupe-effects.md` Phase 2). Light is told, not played, in a fight, so nothing in the engine read the spells' `dim_radius` and it was deleted. When dim light is played, these are the numbers to bring back (each is "Dim Light for an additional 20 feet", 4 squares, beyond the bright radius):
    - Light (cantrip): 4 squares. SRD 5.2.1 p.144.
    - Produce Flame (cantrip): 4 squares. SRD 5.2.1 p.156.
    - Flaming Sphere (level 2): 4 squares. SRD 5.2.1 p.132.

- deferred by Kathleen's answers (2026-10-01, `_design_docs/OPEN_QUESTIONS.md`, recorded 2026-10-03; each is also a line in `decisions_checklist.md`):
    - **[DEFER]** Massive damage killing outright (SRD 5.2.1: damage that leaves you at 0 HP with some left over equal to or more than your HP maximum). Whether damage at 0 HP brings a fresh single death save is open (`_design_docs/OPEN_QUESTIONS.md` §0); as built, it doesn't. (`09-25 Q3`) *(2026-10-03: aligned with decisions_checklist.md)*
    - **[DEFER]** Spell components: material and costly components aren't tracked. (`09-24 Q6`, `09-25 Q5`)
    - **[DEFER]** Monster senses (Darkvision, Blindsight…), sizes past one square, and other speeds (fly, climb, swim). (`09-25 Q7`)
    - **[DEFER]** The SRD conditions v1 doesn't play yet, and their small clauses. (OPEN_QUESTIONS §1)
    - **[DEFER]** Firearms (Musket, Pistol): SRD, left out of v1. (`09-25 Q16`)
    - **[DEFER]** A Bard class, maybe for good: its spells are on the Mage's list. (`09-25 Q19`)

- "build if easy, defer if not" (Kathleen, 2026-10-01): the ones that weren't easy (2026-10-03, `cc_task_open-questions-answers.md`; what was built is in `_design_docs/RUN_LOG_2026-10-03_answers.md`):
    - **[DEFER]** **Weapon Mastery** (SRD 5.2.1, the biggest narrate-only class feature). What it would take: the eight mastery properties as attack riders (Cleave, Graze, Nick, Push, Sap, Slow, Topple, Vex; Topple, Sap, Vex, Push and Slow fit existing riders and boons, Cleave, Graze and Nick are new turn rules), a mastery word on every SRD weapon, each class's count of mastered weapons by level and a creation and level-up step to choose them, saves, the bar saying which weapon has which, and tests per property. About three to four days.
    - **[DEFER]** Monster traits not built (Pack Tactics, Bloodied Fury, Magic Resistance, Undead Fortitude and Sunlight Sensitivity's attack half are): **Spider Climb** (v1 has no climbing), **Web Walker** (a web's restraint sparing the spider: a tag rule on the Web zone, an hour's work once spiders cast webs), **Sunlight Sensitivity's ability-check half** (no checks are rolled in a fight), **monster reactions** (the goblin boss's Redirect Attack, the mage's Protective Magic as one 3/day pool: the reaction window would have to retarget an attack mid-swing, and a statblock's uses would need a shared pool), **Shape-Shift** (werewolf, imp: a second statblock to switch to), the **+1d4 with Advantage** rider (goblin, goblin boss), the **boar's Charge** and the **werewolf's curse**. About two days for all of them, Redirect Attack the largest.
    - **[DEFER]** **The goblin boss's Redirect Attack**: not easy (above) and not reusable beyond goblins; the boss is a campaign that won't ship. Its Shortbow is already in the statblock.
    - **[DEFER]** **Burning webs** (SRD Web: fire burns a 5-foot cube of it away in a round, 2d4 Fire to whoever starts a turn in the fire). A zone is one whole area; burning part of it means cutting squares out of a zone. About a day. Collapsing webs need nothing: v1 always lays a web across a floor, which the SRD says holds it up.
    - **[DEFER]** **Gaseous Form entering and occupying another creature's space**: the battlefield holds one creature a square, and movement, targeting, areas and the board all lean on that. Two to three days; until then Gaseous Form stays on the allow-list.
    - **[DEFER]** **A `<<cast spell>>` request**, so a campaign can ask the hero to cast a spell outside a fight (Spare the Dying on a dying guard, Raise Dead on a murdered innkeeper, Identify, Speak with Dead). A new request verb read like the others, a yes/no card, the slot spent through the incantation with no fight, and the answer in a Yarn variable. About a day. Until it exists, Spare the Dying and Raise Dead aren't offered to a party of one.
    - Decided as they are, not deferred: **Telekinesis on unattended objects** (the narrator's; a fight's only object is a door), **Enhance Ability's per-target ability** (a party of one only ever targets itself), **Shillelagh and True Strike** taking the better option (the one a player would choose), **Hunter's Mark's tracking advantage** (finding is exploration, the narrator's).

- attunement for magic items (2026-10-01, `09-25 Q18`): until campaigns carry enough magic items to need it. What it would
  take: a cap of three attuned items on the sheet, a short rest to attune, and an `attunement` flag on the items that need
  it - about a day.

- Kathleen's answers to task E (2026-10-05, `_design_docs/OPEN_QUESTIONS.md` §0, recorded 2026-10-06; each is also a line in `decisions_checklist.md`):
    - **[DEFER]** **A real Heroic Inspiration button** (SRD 5.2.1, the Human's Resourceful): reroll *any* die the hero rolls, at the player's choice, once a long rest. v1 has it as Indomitable's reroll of the first failed save (`Actor.SaveRerolls`). What it would take: a "reroll?" offer after any hero roll that the player can spend it on (attack, damage, check, save), the hero's choice through the same pause the reaction window would use, the spent flag saved, and tests. About a day. (`10-05 Q2`)
    - **[DEFER]** **The route the board draws.** Hovering a square in a fight lights a route that can be longer than the one the hero walks. What a click costs and how far a turn reaches agree, and the walk pays exactly the promised cost (task E, `RUN_LOG_2026-10-05.md` 2.5): only the drawn path is off. The drawing is `CombatDirector.Hover` (`game/Play/CombatDirector.Hands.cs`) → `Board.Flash(Session.PathTo(cell))` → `CombatSession.PathTo` → `Battlefield.RouteFor` → `Route.Between` (`core/Space/Route.cs`, A* by cost, diagonals one square). Not reproduced yet: start by logging the route `PathTo` returns on the hover Kathleen saw against the squares `Board.Flash` lights. (`10-05 Q4`)

- deferred by cc_task_f (2026-10-06, `_design_docs/RUN_LOG_2026-10-06.md`):
    - **[DEFER]** **"Play this map" in the map builder**: a test fight on the map being built. Not easy: play runs only inside a campaign (`CampaignRun`: a manifest, a Yarn node that sends `<<fight map encounter>>`, an encounter table naming the monsters). What it would take: a throwaway campaign written to `user://` (as the builder's probe does) with the map saved into it, a one-line Yarn node that starts the fight, an encounter of one goblin per spawn, a test hero from `Creation.SuggestTraits()` and the defaults `--begin` uses, then `GameState.Begin` and the table; and back to the builder after the fight, not the book. About half a day.
    - **[DEFER]** **Steam** (Kathleen's side): the Steamworks SDK and a C# wrapper (Steamworks.NET or GodotSteam .NET) with its own credits entry, the app id and a `steam_appid.txt` for local runs, depots built from `build/release/` (`checks/check-export.ps1`), Steam Cloud for `user://saves` (Auto-Cloud by path, or the API), and achievements (`Edition` already says only the full game counts them).
    - **[DEFER]** **Code signing** the Windows build: a code-signing certificate (OV or EV), `codesign/enable` and its identity in the Windows preset (`game/export_presets.cfg`), and signtool on the build machine. Without it, SmartScreen warns on first run outside Steam.
    - **[DEFER]** **A Linux / Steam Deck preset**: a "Linux" preset (x86_64) beside the Windows ones, the 4.7.1 .NET Linux templates, a test pass on Proton as well as native (the Deck runs either), the Deck's controls (every act already has an InputMap entry, `game/Access`), and its 1280x800 screen in `check-layout.ps1`'s sizes.
