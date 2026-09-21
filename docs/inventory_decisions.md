# inventory
- narrative should be able to add items to the character's inventory.
    - the item(s) can be either be:
        - a specified/defined item(s)
        - drawn from a pool of specified/defined items
        - drawn from a game-defined pool of loot

- game-defined pools of loot should be specified to class
- it should also be either specified to level range or be able to auto-adjust the stats to match the character level

- items, skills, etc. can potentially have the ability to increase hp, ac, ability scores, etc. up to max
    - they also specify if there is an action/reaction consumed during combat and how many actions/reactions are consumed

- items stack up to 99,999. the ui of the full inventory display truncates those after 999 so that if you have anywhere between 1,000 - 1,999 the inventory display will show 1k. inspecting the item details will show the full actual quantity. then the ui only has to plan for a max of 3 chars (up to 99k)

- quest-specific items that don't provide character benefit do not consume inventory slots (such as a key or a signet ring)

- characters have 40 inventory slots

- stackable item consume one inventory slot per stack (1 health potion = 1 inventory slots. 99,999 health potions = 1 inventory slot. 199,999 health potions = 2 inventory slots because of the 99k breakpoint on a stack)

- if acquiring an item would exceed available inventory slots, the player must immediately choose enough items to discard to make room.
    - the newly acquired item is included among the items that can be discarded, but is shown as a separate section in the ui.
    - discarding the new item leaves the player's existing inventory unchanged.
    - the player cannot continue until inventory is within capacity.

- merchants have unlimited gold and the ui never even has a slot/area/display for an amount of total gold that a merchant has. only how much he will give you per item/bulk sale

- discarded items are gone/destroyed
    - a warning should surface and the warning should be permenantly dismissable per campaign character, not per player regardless of campaign

- item itself specifies if it's stackable or not. if yes, then all items with that id are stacked per the 99,999 rule

- item can be sold. item itself specifies the amount it can be bought for and the game specifies that items can be sold for X percentage of the buy price.
    - item can override the default sell price up to 100% of the buy price

- player is prevented from purchasing items if inventory is full. merchant should have a game-level dialog line for inventory being full. create tests to enforce the buy prevention. create tests to enforce the dialog line

- player may sell equipped items, but provide a warning/confirmation every time they attempt this (cannot permenantly dismiss the warning for the rest of that campaign or even that merchant interaction)

- ui indicates equipped items with an icon

- purchase options include Buy as well as Buy & Equip (which will immediately be equipped and the equipped icon is removed from the original and added to the new one)

- v1 does not offer a way for an item to be equipped in multiple slots at player choice (no skills that allow use of a main-hand weapon in an off-hand. no items that can be either main-hand or off-hand)

- items can be restricted to only usable by certain class(es) or require a minimum level. test ensures unuseable items aren't surfaced in shops and as loot