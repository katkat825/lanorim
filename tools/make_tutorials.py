"""Writes the three tutorial campaigns' shared files: campaigns/tutorial_beginner, tutorial_intermediate and
tutorial_expert. Kathleen's scripts are campaign_authoring/Tutorial M&MR Campaigns/<tier>/; the tiers are how much
the player knows about tabletop RPGs, not a series (TUTORIAL_BEGINNER_CONTENTS.md), so each is a whole campaign.

Run from lanorim/:  python tools/make_tutorials.py

The story is each pack's dialogue/*.yarn, written by hand and never touched here. This script writes everything
else, the same for all three tiers - pack.json, the maps, the fights, the road table, the loot, the store - and
lifts every line's English out of the .yarn into the pack's locale csv, so the two cannot drift apart. Re-running
overwrites those files (not the .yarn). The maps are placeholders for Kathleen to redraw in the map builder.
"""
import csv
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
CAMPAIGNS = os.path.join(HERE, "..", "campaigns")

# the tier's pack id, the picker's tag (TutorialPicker: tutorial_<TutorialLevel>), and its name in the book
TIERS = [
    ("tutorial_beginner", "tutorial_beginner", "Tutorial: new to tabletop games",
     "Never played a tabletop RPG? Learn what the dice, the GM and your hero are, and every control."),
    ("tutorial_intermediate", "tutorial_intermediate", "Tutorial: played a little",
     "Know roughly how a tabletop RPG goes? A quick refresher on the rules, and how this table plays them."),
    ("tutorial_expert", "tutorial_advanced", "Tutorial: know the rules well",
     "Know the rules already? Just the controls, and where Lanorim plays differently."),
]

CHAPTER = "hollin"

# speakers named in the story; the companion and the GM need no row
ACTORS = {"pell": "Pell", "dobb": "Dobb", "thief": "Man in a muddy coat"}


# --- maps -------------------------------------------------------------------------------------------------
# each map is drawn in the engine's own notation (docs/campaign_authoring_rules.md section 10). the walls string is
# the map's own shape with every wall and door given the style letter

def grid(text):
    return "\n" + "\n".join(line for line in text.strip("\n").split("\n")) + "\n"


def styled(map_text, letter):
    out = []
    for i, line in enumerate(map_text.split("\n")):
        chars = list(line)
        for j, ch in enumerate(chars):
            if ch in "-|x":
                chars[j] = letter
            elif ch in "@~#_123456789":
                # a square: the walls string writes every square as plain floor
                chars[j] = "."
        out.append("".join(chars))
    return "\n".join(out)


def ground(rows):
    return "\n".join(rows) + "\n"


def prop(pid, x, y, turn=0):
    return {"id": pid, "x": x, "y": y, "turn": turn}


MAPS = {}

MAPS["hollin_village"] = {
    "name": "Hollin Village",
    "columns": 14, "rows": 10,
    "map": grid("\n".join(
        ["+ " * 14 + "+"] + sum([[" " + " ".join("@" if (r == 9 and c == 6) else "." for c in range(14)) + " ",
                                  "+ " * 14 + "+"] for r in range(10)], []))),
    "ground": ground(["gggggg" + "dd" + "gggggg" if r not in (2, 3, 4) else "ggggcc" + "dd" + "gggggg"
                      for r in range(10)]),
    "props": [prop("inn", 1, 1), prop("barn", 9, 1), prop("well", 5, 3), prop("house_small", 1, 5),
              prop("stall", 4, 6), prop("house_small", 10, 6), prop("house", 12, 6),
              prop("gatehouse", 5, 8), prop("tree_clump", 0, 8), prop("barrel_apples", 3, 5)],
}

GRANARY_FLOOR = grid("""
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
|. . . . . . .|
+ + + + + + + +
|. . . . . . .|
+ + + + + + + +
|. . . . . 1 .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+
""")
MAPS["granary_floor"] = {
    "name": "Granary Ground Floor",
    "columns": 7, "rows": 5,
    "map": GRANARY_FLOOR,
    "ground": ground(["ppppppp"] * 5),
    "walls": styled(GRANARY_FLOOR, "t"),
    "props": [prop("stairs", 6, 0), prop("sack", 3, 2), prop("bucket", 4, 4), prop("crate", 1, 4),
              prop("barrel", 6, 4)],
}

GRANARY_LOFT = grid("""
+-+-+-+-+-+-+-+-+
|@ . . .|. ~ ~ .|
+ + + + + + + + +
|. . . .x. ~ ~ 1|
+ + + + + + + + +
|. . . .|. . . .|
+-+ +-+-+ + + + +
|. . . . . . . .|
+ + + + + + + + +
|. ~ ~ . . . . 2|
+ + + + + + + + +
|. ~ ~ . . . . .|
+-+-+-+-+-+-+-+-+
""")
MAPS["granary_loft"] = {
    "name": "Granary Loft",
    "columns": 8, "rows": 6,
    "map": GRANARY_LOFT,
    "ground": ground(["pppppppp"] * 6),
    "walls": styled(GRANARY_LOFT, "t"),
    "props": [prop("stairs", 1, 0), prop("crates_stacked", 6, 3), prop("crates_stacked", 6, 4),
              prop("sack", 0, 3), prop("sack", 4, 5), prop("barrel", 3, 5)],
}

NORTH_ROAD = grid("""
+-+-+-+-+-+-+-+-+-+-+
|. . . . . . . . . .|
+ + + + + + + + + + +
 . . . . . . . . . .
+ + + + + + + + + + +
 @ . . . . . . . . 1
+ + + + + + + + + + +
 . . . . . . . . . .
+ + + + + + + + + + +
|. . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+
""")
MAPS["north_road"] = {
    "name": "Hollin North Road",
    "columns": 10, "rows": 5,
    "map": NORTH_ROAD,
    "ground": ground(["gggggggggg", "dddddddddd", "dddddddddd", "dddddddddd", "gggggggggg"]),
    "walls": styled(NORTH_ROAD, "h"),
    "props": [prop("bush_large", 2, 0), prop("bush_large", 5, 0), prop("tree_clump", 7, 0),
              prop("bush_large", 1, 4), prop("bush_large", 6, 4)],
}

DOBB_FARM = grid("""
+-+-+-+-+-+-+-+-+-+-+
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . 1 . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
 . . . . . . . . . .|
+ + + + + + + + + + +
 @ . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+
""")
MAPS["dobb_farm"] = {
    "name": "Dobb Farm",
    "columns": 10, "rows": 7,
    "map": DOBB_FARM,
    "ground": ground(["gggggggggg", "gggggggggg", "ggdddddggg", "ggdddddggg", "ggdddddggg",
                      "dddddddggg", "dddggggggg"]),
    "walls": styled(DOBB_FARM, "e"),
    "props": [prop("house_small", 0, 0), prop("barn_small", 7, 0), prop("chicken_coop", 4, 1),
              prop("bucket", 3, 4), prop("farm_crate", 5, 5), prop("log_pile", 7, 6)],
}

CHAPTER_MAPS = ["hollin_village", "granary_floor", "granary_loft", "north_road", "dobb_farm"]


# --- the tables -----------------------------------------------------------------------------------------

def fight(fid, map_id, monster, count=1, loot=None):
    entry = {"id": fid, "level": 1, "difficulty": "low", "kind": "fight", "map": map_id,
             "monsters": [{"monster": monster} if count == 1 else {"monster": monster, "count": str(count)}]}
    if loot:
        entry["loot"] = loot
    return entry


ENCOUNTERS = {"tables": [
    {"id": "set_pieces", "entries": [
        fight("granary_rat", "granary_floor", "giant_rat"),
        fight("granary_loft_rats", "granary_loft", "giant_rat", 2),
        fight("chicken_thief", "dobb_farm", "bandit"),
    ]},
    {"id": "north_road", "trigger": {"roll": "1d20", "at_least": 8}, "entries": [
        {"id": "birds", "kind": "line", "weight": 3},
        dict(fight("wolf", "north_road", "wolf"), weight=1),
    ]},
]}

ENCOUNTER_LINES = {
    "encounter.set_pieces.line.granary_rat": "A rat the size of a small dog.",
    "encounter.set_pieces.line.granary_loft_rats": "Two more rats in the loft.",
    "encounter.set_pieces.line.chicken_thief": "The man in the muddy coat draws his weapon.",
    "encounter.north_road.line.birds": "A flock of small, dark birds in the ditch.",
    "encounter.north_road.line.wolf": "A thin, hungry wolf steps out of the hedge.",
}

LOOT = {"tables": [
    {"id": "granary_pouch", "entries": [
        {"id": "old_pouch", "kind": "find", "silver": "5", "items": [{"item": "potion_of_healing", "count": "1"}]},
    ]},
    {"id": "rat_nest", "entries": [
        {"id": "few_coppers", "kind": "find", "weight": 2, "copper": "2d10"},
        {"id": "lost_silver", "kind": "find", "weight": 1, "silver": "1d6"},
    ]},
]}

MERCHANTS = {"merchants": [
    {"id": "hollin_store", "stock": ["potion_of_healing", "rope", "torch", "dagger", "shield"],
     "sell_percent": 50},
]}


# --- writing ----------------------------------------------------------------------------------------------

def write(root, rel, text):
    path = os.path.join(root, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def as_json(value):
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


LINE = re.compile(r"#line:([a-z0-9_]+)")
SPEAKER = re.compile(r"#speaker:([a-z0-9_]+)")


def yarn_lines(root, campaign):
    """every line's key and English: the node's speaker unless the line names its own"""
    rows = {}
    folder = os.path.join(root, "dialogue")
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".yarn"):
            continue
        speaker = "dm"
        with open(os.path.join(folder, name), encoding="utf-8") as f:
            for raw in f:
                line = raw.strip()
                if line.startswith("speaker:"):
                    speaker = line.split(":", 1)[1].strip()
                    continue
                found = LINE.search(line)
                if not found:
                    continue
                text = line.split("#", 1)[0].strip()
                if text.startswith("->"):
                    text = text[2:].strip()
                own = SPEAKER.search(line)
                who = own.group(1) if own else speaker
                key = f"dialogue.{who}.line.{campaign}.{found.group(1)}"
                if key in rows:
                    raise SystemExit(f"{campaign}: line id {found.group(1)} is used twice")
                rows[key] = text
    return rows


def main():
    for campaign, tag, name, blurb in TIERS:
        root = os.path.join(CAMPAIGNS, campaign)
        if not os.path.isdir(os.path.join(root, "dialogue")):
            raise SystemExit(f"{campaign}: no dialogue/ yet - write the story first")

        manifest = {
            "id": campaign, "kind": "campaign", "format": 1, "engine": "0.1",
            "author": "Maps & Math Rocks", "tags": [tag],
            "chapters": [{"id": CHAPTER, "maps": CHAPTER_MAPS}],
            "start": CHAPTER, "gm_screen": "plains",
            "starting_shop": False,
        }
        write(root, "pack.json", as_json(manifest))

        for map_id, m in MAPS.items():
            body = {"format": 2, "name": m["name"], "columns": m["columns"], "rows": m["rows"], "map": m["map"]}
            for key in ("ground", "walls"):
                if key in m:
                    body[key] = m[key]
            body["props"] = m["props"]
            write(root, f"maps/{map_id}.map", as_json(body))

        write(root, "encounters/encounters.json", as_json(ENCOUNTERS))
        write(root, "loot/loot.json", as_json(LOOT))
        write(root, "merchants/merchants.json", as_json(MERCHANTS))

        rows = {
            f"campaign.{campaign}.name": name,
            f"campaign.{campaign}.description": blurb,
            f"quest.{campaign}.{CHAPTER}.title": "Errands in Hollin",
            "merchant.hollin_store.name": "Hollin General Store",
        }
        rows.update({f"actor.{a}.name": n for a, n in ACTORS.items()})
        rows.update(ENCOUNTER_LINES)
        rows.update(yarn_lines(root, campaign))

        out = io.StringIO()
        w = csv.writer(out, lineterminator="\n")
        w.writerow(["keys", "en"])
        for key in sorted(rows):
            w.writerow([key, rows[key]])
        write(root, f"locale/{campaign}.csv", out.getvalue())

        print(f"{campaign}: {len(rows)} locale rows")


if __name__ == "__main__":
    main()
