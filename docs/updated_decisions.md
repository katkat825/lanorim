note: examples are simply one of many examples and used to express the point rather than all-encompassing scenarios

# ui is simplified for v1
- traditional ui menu (map, inventory, equipment, etc.) is a campaign book and the nav options are the table of contents in that campaign book

- companions will all be variations on the kaykit skeleton pack. this pack will not be used anywhere else in the game.

- companions will be the size of minis, but never on the map. they can sit or stand or walk or lay down elsewhere on the table.

- entire room idea is gone, hands/arms are gone. you see the table only. on that table is:
    - grid map taking up 3/4 of the screen/viewable window
    - dice tray, small (too small to read when it's on the table. it lifts up to roll, blocking part of the grid map, then settles back down again an appropriate amount of time after the roll stops so the player can read/see the roll before it gets small again by going back onto the table)
    - gm screen (with appropriate background image. image should bend with the bends in the gm screen)
    - companion
    - help or settings button

- no separate campaign book and rules book. only campaign book. the rules/help stuff is a section of the campaign book

- dialog will be presented in a more traditional video-game manner rather than the original diegetic choice

# gameplay decisions
- use all the available ttrpg dice

- short rest: recover anything recoverable from short rest. optionally spend hit dice to recover health.
- long rest: recover anything recoverable from short rest or long rest. hp restored to max

- outside combat, the player can initiate a short or long rest at any time unless the current location restricts resting.
    - dungeons do not allow resting by default.
    - a campaign can explicitly configure a dungeon to allow resting.

- campaign specifies whether you can leave a dungeon before completion at all (per dungeon or per campaign). if yes,
    - campaign specifies whether you come back to it as-is/in progress or come back to it as if it never happened

- dying results in reloading to the last save point

- at 0 hp player has the option of death saving throw. on failures character is dead > game is reloaded to last save. on success, character revives with 1 hp
    - saving throw is a d20 with no modifiers. rolling >= 10 is successful

## skill checks
- use skills for checks like dnd 5e does. what skill is used is defined by the campaign.
    - include a check for ensuring that every campaign that asks for a skill check has a skill selected and that the skill is one of the available skills from the character sheet.

- campaign sets the difficulty for that skill check.
    - predetermined 5e srd dc ladder, or custom number.

- nat 1 and nat 20 on a skill check:
    - pass or fail the skill check based purely on the numbers (no auto-fail or auto-succeed)
    - have other consequences from a pool of possibilities regardless of pass/fail
        - for example: roll a nat 1 and you stub your toe taking 1d4 damage, or that attempt exhausted you and now you have minus 1 to wisdom until you take a rest (short or long). even if it's easy and your modifiers mean you passed the actual skill check
        - for example: roll a nat 20 and when you turn away you find a pouch with 5+level gold in it, or the attempt improved your confidence and you have a plus 1 to charisma until you take a rest (short or long). even if it's impossible and with your modifiers you still failed the actual skill check
        - note: this also means there is the ability to take damage outside of combat

### dialog options
- dialog will support branching.
    - for example: "The study is dusty and apparently undisturbed. A large oak desk sits beneath the window." can produce the options of Continue, [INVESTIGATION] Search Desk

- dialog will also support required checks.
    - for example: "You can make out the vague shape of something on the far side of the room. Roll a perception check." which produces only one option: Roll Perception

- campaign sets whether additional checks are allowed and whether the same check can be repeated.
    - for example: trying to pick a lock on a door might be allowed to be repeated as many times as you have lock picks for
    - for example: you may be stopped by the guards and you have options to intimidate, persuade, evade, attack. and if you pick intimidate and fail you can't do an intimidation check again, but you can try to persuade them. alternately you can try that first intimidation check, fail, and the only available options are now to evade or attack (so failed intimidation also removes the option to try to persuade)

- campaign choices can be conditional based on the player's inventory.
    - a choice can require the player to possess a specified item or quantity of an item before the choice is available.
    - a choice can consume a specified quantity of an item when selected.
    - after returning to a previous point in the narrative flow, available choices are reevaluated based on the player's current state.
    - ability scores do not automatically bypass skill checks. If the campaign defines a skill check, the player makes the skill check regardless of their raw ability score.

## Campaign Flow

* a campaign's main structure is a narrative flow.

* gameplay events can branch away from the current narrative flow into a separate defined flow, resolve there, and then return to the original narrative flow at a defined point.
  * skill checks use this structure.
  * combat encounters use this structure.
  * other gameplay systems may use the same structure as needed.

* a skill check must define both a success and failure path.
  * either path can contain additional narrative, choices, events, or other gameplay.
  * either path can simply return to the main narrative and continue.
  * success and failure do not have to produce substantially different outcomes.
  * the campaign must explicitly define what happens for both rather than relying on an undefined/default outcome.

* example skill check flow:
  * main narrative: "The study is dusty and apparently undisturbed. A large oak desk sits beneath the window."
  * player chooses: `[INVESTIGATION] Search Desk`
  * branch to the defined Investigation check.
  * success: follow the defined success content, then return to the main narrative at the defined point.
  * failure: follow the defined failure content, which may simply be Continue, then return to the main narrative at the defined point.

* combat follows the same general structure.
  * the main narrative reaches a defined combat encounter.
  * combat begins, including initiative and the combat encounter itself.
  * when combat resolves, return to the main narrative at the defined point.
  * campaigns may define different post-combat paths based on the result when applicable.

# player characters
- offer skin tone options
- offer hair color options

# open questions
- continue button on the dialog?

- dialog as a pop-up or as a CC style bottom of the screen?

- is fleeing combat an option?

- [DEFERRED] Are backpack/horse/cart inventories separate containers or simply additive character capacity?
    - is inventory globally accessible? maybe there's a lost and found in every town where you can get all of your inventory back?  

- skin tone and hair color use color picker or pre-determined list?
    - skin tone list (if list): #F4DDC5, #E1B992, #C88A5E, #9A5E3B, #3B241a, #3A3D36
    - hair color list (if list): tbd

