"""Writes campaigns/sample_millbrook/ - the TEST campaign (not a real one, and not drawn on
campaign_authoring/). It exists so every system a campaign can reach is exercised end to end:
maps, checks, saves, fights, an encounter table, loot tables, a merchant, rests, milestone levels
up to the level-4 ability score improvement, companion lines, save and load.

Run from lanorim/:  python tools/make_sample_campaign.py

The English for every dialogue line is written ONCE, in the .yarn, and this script lifts it into
the campaign's locale csv, so the two cannot drift apart. Re-running overwrites the folder's files.
"""
import csv
import io
import json
import os
import re

ID = "sample_millbrook"
ROOT = os.path.join(os.path.dirname(__file__), "..", "campaigns", ID)


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# --- maps -----------------------------------------------------------------------------------
# a room drawn as squares; the walls round the outside are added here. '@' the hero, 1-9 spawns,
# '~' difficult ground, '#' rock (outside the room)
def room(rows, inner_walls=()):
    height = len(rows)
    width = len(rows[0])
    lines = []
    for r in range(height + 1):
        # the line of walls above row r
        line = "+"
        for c in range(width):
            above = rows[r - 1][c] if r > 0 else "#"
            below = rows[r][c] if r < height else "#"
            solid = (above == "#") != (below == "#")
            if (r, c, "h") in inner_walls:
                solid = True
            line += ("-" if solid else " ") + "+"
        lines.append(line)
        if r == height:
            break
        row = ""
        for c in range(width + 1):
            left = rows[r][c - 1] if c > 0 else "#"
            right = rows[r][c] if c < width else "#"
            solid = (left == "#") != (right == "#")
            if (r, c, "v") in inner_walls:
                solid = True
            row += "|" if solid else " "
            if c < width:
                row += rows[r][c]
        lines.append(row)
    return "\n" + "\n".join(lines)


def map_file(rows, props=(), inner_walls=()):
    return json.dumps({
        "format": 1,
        "columns": len(rows[0]),
        "rows": len(rows),
        "map": room(rows, inner_walls),
        "props": [{"id": p, "x": x, "y": y, "turn": 0} for (p, x, y) in props],
    }, indent=2) + "\n"


MAPS = {
    # the mill's cellar: sacks, a flooded corner, rats in the far end
    "mill_cellar": map_file([
        "@...~~..",
        "....~~.1",
        "........",
        "..#....2",
        "..#...3.",
        "......4.",
    ], props=[("barrel", 1, 4), ("crate", 5, 0)]),
    # the road out of Millbrook, through a clearing
    "road_clearing": map_file([
        "##......##",
        "#...~~...#",
        "@....~..1.",
        "........2.",
        "#..~.....#",
        "##......##",
    ]),
    # the goblins' camp in the old mill yard
    "mill_yard": map_file([
        "@.........",
        "..~~......",
        "......#..1",
        "......#.2.",
        "..~.....3.",
        "..........",
        "........4.",
    ], props=[("cauldron", 7, 5), ("crate", 3, 6)]),
}

# --- tables -------------------------------------------------------------------------------------
ENCOUNTERS = {
    "tables": [
        {
            # the story's set-piece fights: a table nobody rolls, so each entry is a fight by id
            "id": "set_pieces",
            "entries": [
                {"id": "cellar_rats", "kind": "fight", "map": "mill_cellar", "loot": "rat_nest",
                 "monsters": [{"monster": "giant_rat", "count": "2"}]},
                {"id": "goblin_camp", "kind": "fight", "map": "mill_yard", "loot": "goblin_chest",
                 "monsters": [{"monster": "goblin_boss"}]},
            ],
        },
        {
            # the road: always something, seldom nothing
            "id": "mill_road",
            "trigger": {"roll": "1d20", "at_least": 4},
            "entries": [
                {"id": "crows", "kind": "line", "weight": 1},
                # one wolf: two knock a level-2 hero prone and bite with Pack Tactics, and the sim
                # has them winning more often than not (BALANCE_2026-09-25.md)
                {"id": "wolves", "kind": "fight", "weight": 3, "map": "road_clearing",
                 "monsters": [{"monster": "wolf"}]},
                {"id": "bandits", "kind": "fight", "weight": 2, "map": "road_clearing",
                 "loot": "bandit_purse",
                 # two since 2026-09-25: a monster attacks once a turn now (its SRD statblock's
                 # turn, not the hero's two actions), and one bandit alone was a formality
                 "monsters": [{"monster": "bandit", "count": "2"}]},
            ],
        },
    ]
}

LOOT = {
    "tables": [
        {"id": "rat_nest", "entries": [
            {"id": "gnawed_purse", "kind": "find", "weight": 3, "gold": "2d6", "line": True},
            {"id": "old_torches", "kind": "find", "weight": 1,
             "items": [{"item": "torch", "count": "1d4"}], "gold": "3"},
        ]},
        {"id": "bandit_purse", "entries": [
            {"id": "coin", "kind": "find", "gold": {"roll": "2d6", "times": 5}},
        ]},
        {"id": "goblin_chest", "entries": [
            {"id": "stolen_takings", "kind": "find", "gold": {"roll": "4d6", "times": 10},
             "items": [{"item": "potion_of_healing", "count": "1"}], "line": True},
        ]},
    ]
}

MERCHANTS = {
    "merchants": [
        {"id": "millbrook_store",
         "stock": ["potion_of_healing", "torch", "rope", "antitoxin", "dagger", "shortbow",
                   "shield", "leather_armor"],
         "sell_percent": 50},
    ]
}

MANIFEST = {
    "id": ID,
    "kind": "campaign",
    "format": 1,
    "engine": "0.1",
    "author": "Lanorim test content",
    "tags": ["test"],
    "chapters": [
        {"id": "old_mill", "maps": ["mill_cellar", "road_clearing", "mill_yard"]},
    ],
    "start": "old_mill",
}

# --- the story ----------------------------------------------------------------------------------
# a chapter id is also the node the chapter opens on
STORY = r"""title: old_mill
speaker: dm
---
Millbrook is a village of one street, one well, and one mill that has stopped turning. #line:arrive
The miller is waiting on the bridge, wringing a floury cap in both hands. #line:miller_waits
Thank the stars. Something is in my cellar, and the grain is going missing by the sack. #line:oda_asks #speaker:oda
Something with teeth, by the look of those sacks. #line:comp_teeth #speaker:companion
-> What's in the cellar? #line:opt_whats_there
    Rats. Big ones. And tracks out the back that no rat made. #line:oda_rats #speaker:oda
-> I'll need supplies first. #line:opt_supplies
    Hesk's store is across the street. Tell her I sent you. #line:oda_hesk #speaker:oda
    <<shop millbrook_store>>
    You step back out into the street, pack heavier than before. #line:after_shop
-> Show me the door. #line:opt_go
Oda leads you round to a low door under the millrace. #line:to_the_door
<<jump cellar>>
===
title: cellar
speaker: dm
---
The cellar door is swollen shut with damp. #line:door_stuck
<<check athletics 12>>
<<if $passed>>
    It gives with a crack, and the smell of wet grain rolls out. #line:door_gives
<<else>>
    It will not budge. You find the coal chute instead and slither down into the dark. #line:coal_chute
    <<save dex 10>>
    <<if not $passed>>
        You land badly among the sacks. Nothing broken, but everything in there heard you. #line:land_badly
    <<endif>>
<<endif>>
Eyes in the lamplight. Two pairs, low down. #line:rat_eyes
<<fight cellar_rats>>
<<if $fight == "fled">>
    You scramble back up into daylight. Oda looks at you, then at the door, and says nothing. #line:fled_rats
    <<jump road>>
<<endif>>
The last rat goes still. Behind the grain bins, a nest of chewed sacking. #line:rats_done
Something was feeding them. Those are goblin boots. #line:comp_boots #speaker:companion
<<loot rat_nest>>
<<if $loot_gold > 0>>
    Somebody lost a purse down here, and the rats found it first. #line:rat_purse
<<endif>>
<<level 2>>
<<rest short>>
You catch your breath on the cellar steps and follow the tracks out onto the road. #line:follow_tracks
<<jump road>>
===
title: road
speaker: dm
---
The road north runs between hedges gone wild. #line:road_north
<<encounter mill_road>>
<<if $encounter_fight>>
    Something moves in the clearing ahead. #line:ambush
    <<fight {$encounter}>>
    <<if $fight == "won">>
        The road is quiet again. #line:road_quiet
    <<endif>>
<<elseif $encounter == "crows">>
    A crowd of crows lifts off a dead ewe, complaining. Nothing else stirs. #line:crows
<<else>>
    The miles pass without trouble. #line:no_trouble
<<endif>>
<<roll 1d20>>
<<if $roll >= 15>>
    The sun comes out as you crest the hill, and the old mill yard is below you. #line:sun_out
<<else>>
    Rain sets in as you crest the hill, and the old mill yard is below you. #line:rain_in
<<endif>>
<<level 3>>
<<rest short>>
You rest in the lee of a wall before going down. #line:rest_before_camp
<<jump camp>>
===
title: camp
speaker: dm
---
Goblins have made a camp of the ruined mill: a cauldron, a stolen cart, and a big one in a stolen coat. #line:the_camp
Just the big one. The rest must be out on the road. #line:comp_count #speaker:companion
-> Creep closer along the ditch. #line:opt_creep
    <<check stealth 13>>
    <<if $passed>>
        You are among the carts before anyone looks up. #line:creep_ok
    <<else>>
        A pot clatters under your boot. Every head turns. #line:creep_bad
    <<endif>>
-> Walk straight in. #line:opt_walk
    The big one grins and draws a scimitar. #line:walk_in
<<fight goblin_camp>>
<<if $fight == "fled">>
    You run, and the goblins' laughter follows you down the road. #line:fled_camp
    <<jump the_end>>
<<endif>>
The goblin boss drops the coat, and Oda's grain money spills out of the lining. #line:boss_down
<<loot goblin_chest>>
<<level 4>>
<<jump the_end>>
===
title: the_end
speaker: dm
---
You walk back into Millbrook as the mill wheel creaks and turns again. #line:wheel_turns
You'll eat with us tonight. And take this - it's not much. #line:oda_thanks #speaker:oda
<<gold 25>>
<<give potion_of_healing 1>>
<<rest long>>
Good bread. Good beds. I could get used to this. #line:comp_beds #speaker:companion
This is the end of the test campaign. #line:test_over
===
"""

LOCALE = {
    f"campaign.{ID}.name": "Millbrook (test campaign)",
    f"campaign.{ID}.description":
        "A short test campaign: a cellar, a road and a goblin camp. It exercises every system a campaign can use.",
    f"quest.{ID}.old_mill.title": "The Old Mill",
    "merchant.millbrook_store.name": "Hesk's Store",
    "actor.oda.name": "Oda",
}


def lines_of(story):
    """(key, english) for every #line: in the story, keyed the way DialogueKeys.Line does."""
    speaker = "dm"
    for raw in story.splitlines():
        s = raw.strip()
        m = re.match(r"speaker:\s*(\w+)", s)
        if m:
            speaker = m.group(1)
            continue
        if s.startswith("title:"):
            speaker = "dm"
            continue
        m = re.search(r"#line:(\w+)", s)
        if not m:
            continue
        who = re.search(r"#speaker:(\w+)", s)
        text = s[:s.index("#line:")].strip()
        if text.startswith("->"):
            text = text[2:].strip()
        # "Oda: words" - Yarn's character-name prefix is not part of the line
        text = re.sub(r"^[A-Z][a-z]+:\s*", "", text)
        yield f"dialogue.{who.group(1) if who else speaker}.line.{ID}.{m.group(1)}", text


def main():
    for name, text in MAPS.items():
        write(f"maps/{name}.map", text)
    write("encounters/encounters.json", json.dumps(ENCOUNTERS, indent=2) + "\n")
    write("loot/loot.json", json.dumps(LOOT, indent=2) + "\n")
    write("merchants/merchants.json", json.dumps(MERCHANTS, indent=2) + "\n")
    write("pack.json", json.dumps(MANIFEST, indent=2) + "\n")
    write("dialogue/story.yarn", STORY)

    rows = dict(LOCALE)
    rows.update(dict(lines_of(STORY)))
    for extra in EXTRA_KEYS:
        rows.setdefault(extra[0], extra[1])

    out = io.StringIO()
    w = csv.writer(out, lineterminator="\n")
    w.writerow(["keys", "en"])
    for key in sorted(rows):
        w.writerow([key, rows[key]])
    write(f"locale/{ID}.csv", out.getvalue())


# the table and loot keys a pack promises (Package.Keys) - filled in from the test that lists them
EXTRA_KEYS = [
    ("encounter.set_pieces.line.cellar_rats", "Rats pour out from behind the grain bins."),
    ("encounter.set_pieces.line.goblin_camp", "The goblin boss kicks over the cauldron and comes for you."),
    ("encounter.mill_road.line.crows", "Crows lift off the verge, complaining."),
    ("encounter.mill_road.line.wolves", "A wolf slinks out of the hedge."),
    ("encounter.mill_road.line.bandits", "Two figures step into the road with drawn blades."),
    ("encounter.rat_nest.loot.gnawed_purse", "A gnawed purse, still heavy with coin."),
    ("encounter.rat_nest.loot.old_torches", "A bundle of old torches, damp but usable."),
    ("encounter.bandit_purse.loot.coin", "A few coins in a bandit's purse."),
    ("encounter.goblin_chest.loot.stolen_takings", "The mill's stolen takings, and a potion wrapped in a rag."),
]

if __name__ == "__main__":
    main()
