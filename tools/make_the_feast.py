"""Writes The Feast's shared files: campaigns/the_feast. Kathleen's script is
campaign_authoring/One-Shots/The Feast/ (Scene 1 - Scene 8 and the working notes); this is the game's copy.

Run from lanorim/:  python tools/make_the_feast.py

Like tools/make_tutorials.py: the story is the pack's dialogue/*.yarn, written by hand and never touched here. This
script writes pack.json, the fights and the locale csv (every line's English lifted out of the .yarn, so the two
cannot drift apart), and re-running overwrites those. The maps are different: a map is written only when its file
isn't there yet, so a map Kathleen has redrawn in the map builder is never overwritten. The new maps are
placeholders for her to redraw; the_meeting and briarwood are hers, from her Workshop draft.

It also checks the story: every <<jump>>, <<map>> and <<fight>> names something that exists, and no line id is used
twice.
"""
import csv
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "campaigns", "the_feast")
CAMPAIGN = "the_feast"
CHAPTER = "the_meeting"

NAME = "The Feast"
BLURB = ("A level 1 one-shot adventure. Lord Percival Thistledown is throwing himself a grand celebration, and the "
         "villagers are paying for it. Literally. With winter approaching, the self-important lord has seized "
         "livestock, grain, supplies, and even farm carts to fund a week of extravagant festivities. Meanwhile, his "
         "subjects are struggling to put food on their tables. But the villagers of Briarwood have had enough. With a "
         "little courage, a questionable plan, and the help of a passing adventurer, they might just pull off the most "
         "audacious feast in the lord's history. A lighthearted fantasy heist featuring colorful villagers, ridiculous "
         "nobility, questionable disguises, and a chance to give an entire village something worth celebrating.")

# speakers named in the story; the GM (dm) and the companion need no row
ACTORS = {
    "abel": "Abel", "amos": "Amos Harker", "barnaby": "Barnaby", "elsie": "Elsie", "grist": "Grist",
    "harold": "Harold", "hester": "Hester", "lionel": "Lionel Spratt", "lord_percival_thistledown": "Lord Percival",
    "mabel": "Mabel", "maisie": "Maisie", "nell": "Nell", "rufus": "Rufus Pickle", "tobias": "Tobias",
}


# --- maps -------------------------------------------------------------------------------------------------
# docs/campaign_authoring_rules.md section 10. a map here is a grid of square glyphs, which sides are walled (an open
# side is a way off, so a fight on it can be fled), the doors on a walled side, a ground per row and its props

def draw(cols, rows, squares, closed="", doors=(), gaps=()):
    """the engine's map string: `squares` maps (x, y) to a glyph ('.' elsewhere); `closed` is any of 'nesw';
    `doors` and `gaps` are (side, index) on a closed side, a door or an opening there"""
    def edge(side, i):
        if side not in closed or (side, i) in gaps:
            return " "
        if (side, i) in doors:
            return "x"
        return "-" if side in "ns" else "|"

    lines = []
    for y in range(rows + 1):
        if y == 0 or y == rows:
            side = "n" if y == 0 else "s"
            lines.append("+" + "".join(edge(side, x) + "+" for x in range(cols)))
        else:
            lines.append("+" + " +" * cols)
        if y < rows:
            cells = [squares.get((x, y), ".") for x in range(cols)]
            lines.append(edge("w", y) + " ".join(cells) + edge("e", y))
    return "\n" + "\n".join(lines) + "\n"


def styled(map_text, letter):
    """the walls string: the map's own shape, each wall and door given the style letter, each square plain floor"""
    out = []
    for line in map_text.split("\n"):
        chars = list(line)
        for j, ch in enumerate(chars):
            if ch in "-|x":
                chars[j] = letter
            elif ch in "@~#_123456789":
                chars[j] = "."
        out.append("".join(chars))
    return "\n".join(out)


def prop(pid, x, y, turn=0):
    return {"id": pid, "x": x, "y": y, "turn": turn}


def water(squares, cells, deep=()):
    """shallow water is a ground; deep water is that ground on a chasm square"""
    for c in deep:
        squares[c] = "_"
    return set(cells) | set(deep)


def ground(cols, rows, base, patches):
    grid = [[base] * cols for _ in range(rows)]
    for letter, cells in patches:
        for x, y in cells:
            grid[y][x] = letter
    return "\n".join("".join(r) for r in grid) + "\n"


def box(x0, y0, x1, y1):
    return [(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)]


MAPS = {}


def add(map_id, name, cols, rows, squares, props, base="g", patches=(), closed="", doors=(), gaps=(), wall=None):
    text = draw(cols, rows, squares, closed, doors, gaps)
    body = {"format": 2, "name": name, "columns": cols, "rows": rows, "map": text,
            "ground": ground(cols, rows, base, patches)}
    if wall and closed:
        body["walls"] = styled(text, wall)
    body["props"] = props
    MAPS[map_id] = body


# the inn's public room (Scenes 2, 3, 4, 4.5 and 8)
add("briarwood_inn", "Briarwood Inn", 12, 9, {(6, 8): "@"},
    [prop("fireplace", 0, 3, 1), prop("kitchen_counter", 3, 0), prop("kitchen_counter", 4, 0),
     prop("kitchen_counter", 5, 0), prop("keg", 6, 0), prop("barrel", 7, 0), prop("stairs", 11, 0),
     prop("table_round", 3, 3), prop("chair_wooden", 2, 3), prop("chair_wooden", 4, 3, 2),
     prop("table_round", 8, 3), prop("chair_wooden", 7, 3), prop("chair_wooden", 9, 3, 2),
     prop("table_round", 3, 6), prop("chair_wooden", 2, 6), prop("table_round", 9, 6),
     prop("chair_wooden", 10, 6, 2), prop("candlestick", 6, 4)],
    base="p", closed="nesw", doors=[("s", 6)], wall="p")

# Grist's mill yard (Scene 3's fight, and the stuck wheel in 4.5): the pond to the west, the lane off to the south
mill_squares = {(7, 9): "@", (8, 4): "1", (10, 5): "2"}
mill_water = water(mill_squares, box(0, 4, 2, 8), deep=box(0, 5, 1, 7))
add("mill_yard", "Grist's Mill Yard", 14, 10, mill_squares,
    [prop("village_mill", 2, 0), prop("cart", 9, 4), prop("sack", 10, 3), prop("sack", 11, 3),
     prop("barrel", 6, 1), prop("log_pile", 11, 0), prop("bush_large", 13, 8), prop("tree_clump", 11, 7)],
    patches=[("w", mill_water), ("d", box(3, 2, 13, 3) + box(6, 4, 8, 9))], closed="n", wall="e")

# Tobias's farm (4.5)
add("tobias_farm", "Tobias's Farm", 12, 9, {(5, 8): "@"},
    [prop("farmstead", 0, 0), prop("barn_small", 8, 0), prop("ploughed_field", 8, 5), prop("chicken_coop", 4, 1),
     prop("wagon", 3, 4), prop("log_pile", 0, 7), prop("bucket", 6, 3)],
    patches=[("d", box(4, 3, 7, 8))], closed="nesw", gaps=[("s", 5), ("s", 6)], wall="e")

# the pond in the woods (the hunt and the bear, 4.5): the bear comes out of the trees to the north-east
bear_squares = {(2, 8): "@", (10, 2): "1"}
bear_water = water(bear_squares, box(4, 4, 8, 7), deep=box(5, 5, 7, 6))
add("briarwood_pond", "Briarwood Pond", 14, 10, bear_squares,
    [prop("bush_large", 1, 7), prop("bush_large", 3, 8), prop("oak_1", 13, 0), prop("pine_1", 12, 3),
     prop("oak_2", 8, 0), prop("pine_2", 0, 0), prop("tree_clump", 0, 2), prop("birch_1", 13, 6),
     prop("rock_medium_1", 9, 8), prop("fern", 11, 7)],
    patches=[("w", bear_water)])

# the manor's front drive (Scene 5): the stone drive loops around the fountain, hedges either side
drive = [c for c in box(3, 3, 12, 9) if not (5 <= c[0] <= 10 and 5 <= c[1] <= 7)]
drive_squares = {(7, 11): "@"}
fountain = water(drive_squares, box(6, 5, 9, 7), deep=[(7, 6), (8, 6)])
add("manor_drive", "Thistledown Manor Drive", 16, 12, drive_squares,
    [prop("manor", 6, 0), prop("statue_stag", 6, 6), prop("cart", 4, 9), prop("cart", 5, 9), prop("cart", 10, 9),
     prop("cart", 11, 9), prop("banner_hanging", 5, 1), prop("banner_hanging", 10, 1), prop("bench", 13, 4),
     prop("bench", 13, 6), prop("bench", 13, 8)],
    patches=[("c", drive + box(7, 10, 8, 11)), ("w", fountain)], closed="ew", wall="h")

# the great hall (Scenes 6.1 and 6.2): the high table at the top, a fire at either end
add("great_hall", "Thistledown Great Hall", 14, 10, {(7, 9): "@"},
    [prop("fireplace", 0, 4, 1), prop("fireplace", 13, 4, 3), prop("table_large", 5, 1), prop("table_large", 7, 1),
     prop("chair_high", 6, 0), prop("carpet", 9, 0), prop("table_long", 3, 4), prop("table_long", 4, 4),
     prop("table_long", 5, 4), prop("table_long", 8, 4), prop("table_long", 9, 4), prop("table_long", 10, 4),
     prop("table_long", 3, 7), prop("table_long", 4, 7), prop("table_long", 9, 7), prop("table_long", 10, 7),
     prop("candlestick_triple", 1, 1), prop("candlestick_triple", 12, 1), prop("banner", 3, 0), prop("banner", 10, 0)],
    base="s", closed="nesw", doors=[("s", 7), ("e", 8)], wall="f")


# the kitchen yard (Scene 7): the manor wall to the north and west, the back gate open to the east. two copies: the
# hound starts at its kennel, the guards at the kitchen door
def kitchen_yard(map_id, spawns):
    squares = {(12, 8): "@"}
    squares.update(spawns)
    add(map_id, "Thistledown Kitchen Yard", 14, 10, squares,
        [prop("warehouse", 1, 5), prop("wagon", 6, 6), prop("wagon", 7, 6), prop("wagon", 8, 6),
         prop("wagon", 9, 6), prop("crate_village", 11, 2), prop("campfire", 5, 2), prop("barrel", 1, 1),
         prop("barrel", 2, 1), prop("sack", 5, 5), prop("kitchen_counter", 8, 0)],
        base="d", patches=[("c", box(0, 0, 13, 0))], closed="nws", doors=[("n", 3)], gaps=[("s", 12)], wall="f")


kitchen_yard("kitchen_yard", {(12, 3): "1"})
kitchen_yard("kitchen_yard_guards", {(3, 1): "1", (4, 1): "2"})

# the village green (Scene 8)
add("village_green", "Briarwood Village Green", 16, 12, {(8, 11): "@"},
    [prop("campfire", 7, 5), prop("table_long", 3, 3), prop("table_long", 4, 3), prop("table_long", 5, 3),
     prop("table_long", 10, 3), prop("table_long", 11, 3), prop("table_long", 12, 3), prop("table_long", 3, 8),
     prop("table_long", 4, 8), prop("table_long", 11, 8), prop("table_long", 12, 8), prop("keg", 14, 5),
     prop("keg", 14, 6), prop("barrel_apples", 1, 6), prop("wagon", 0, 9), prop("inn", 0, 0), prop("well", 14, 1),
     prop("house_small", 13, 10)],
    patches=[("d", box(7, 9, 8, 11))])

CHAPTER_MAPS = ["the_meeting", "briarwood", "briarwood_inn", "mill_yard", "tobias_farm", "briarwood_pond",
                "manor_drive", "great_hall", "kitchen_yard", "kitchen_yard_guards", "village_green"]


# --- the fights -------------------------------------------------------------------------------------------

def fight(fid, map_id, monsters, difficulty="low", morale=None):
    entry = {"id": fid, "level": 1, "difficulty": difficulty, "kind": "fight", "map": map_id, "monsters": monsters}
    if morale:
        entry["morale"] = morale
    return entry


ENCOUNTERS = {"tables": [
    {"id": "set_pieces", "entries": [
        fight("mill_collectors", "mill_yard",
              [{"monster": "bandit", "leader": True}, {"monster": "bandit"}], morale="leader"),
        fight("pond_bear", "briarwood_pond", [{"monster": "wolf"}]),
        fight("kitchen_hound", "kitchen_yard", [{"monster": "wolf"}]),
        fight("kitchen_guards", "kitchen_yard_guards", [{"monster": "guard", "count": "2"}], morale="half"),
    ]},
]}

ENCOUNTER_LINES = {
    "encounter.set_pieces.line.mill_collectors": "Rufus Pickle and his partner draw their blades.",
    "encounter.set_pieces.line.pond_bear": "A wolf, as hungry as everyone else this winter.",
    "encounter.set_pieces.line.kitchen_hound": "One of the largest hounds you've ever seen.",
    "encounter.set_pieces.line.kitchen_guards": "Two of his lordship's guards.",
}


# --- the story's own checks -------------------------------------------------------------------------------

LINE = re.compile(r"#line:([a-z0-9_]+)")
SPEAKER = re.compile(r"#speaker:([a-z0-9_]+)")
CONDITION = re.compile(r"\s*<<if [^>]*>>\s*$")
EXPRESSION = re.compile(r"\{\$[a-z0-9_]+\}")
COMMAND = re.compile(r"<<(jump|map|fight) ([a-z0-9_]+)>>")


def read_story():
    """every line's key and English (the node's speaker unless the line names its own), every node, and every
    <<jump>>, <<map>> and <<fight>> the story names"""
    rows, nodes, named = {}, set(), []
    folder = os.path.join(ROOT, "dialogue")
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".yarn"):
            continue
        speaker = "dm"
        with open(os.path.join(folder, name), encoding="utf-8") as f:
            for number, raw in enumerate(f, 1):
                line = raw.strip()
                if line.startswith("title:"):
                    nodes.add(line.split(":", 1)[1].strip())
                    continue
                if line.startswith("speaker:"):
                    speaker = line.split(":", 1)[1].strip()
                    continue
                for verb, target in COMMAND.findall(line):
                    named.append((verb, target, f"{name}:{number}"))
                found = LINE.search(line)
                if not found:
                    continue
                text = line.split("#", 1)[0].strip()
                if text.startswith("->"):
                    text = CONDITION.sub("", text[2:].strip())
                count = iter(range(100))
                text = EXPRESSION.sub(lambda _: "{%d}" % next(count), text)
                own = SPEAKER.search(line)
                who = own.group(1) if own else speaker
                if who not in ACTORS and who not in ("dm", "companion"):
                    raise SystemExit(f"{name}:{number}: speaker '{who}' has no actor name")
                key = f"dialogue.{who}.line.{CAMPAIGN}.{found.group(1)}"
                if any(k.endswith("." + found.group(1)) for k in rows):
                    raise SystemExit(f"{name}:{number}: line id {found.group(1)} is used twice")
                rows[key] = text
    return rows, nodes, named


def check(nodes, named):
    fights = {e["id"] for t in ENCOUNTERS["tables"] for e in t["entries"]}
    known = {"jump": nodes, "map": set(CHAPTER_MAPS), "fight": fights}
    bad = [f"{where}: <<{verb} {target}>> names nothing" for verb, target, where in named
           if target not in known[verb]]
    if CHAPTER not in nodes:
        bad.append(f"the chapter '{CHAPTER}' has no node of that name to start at")
    if bad:
        raise SystemExit("\n".join(bad))


# --- writing ----------------------------------------------------------------------------------------------

def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def as_json(value):
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def main():
    if not os.path.isdir(os.path.join(ROOT, "dialogue")):
        raise SystemExit("campaigns/the_feast has no dialogue/ yet - write the story first")

    lines, nodes, named = read_story()
    check(nodes, named)

    manifest = {
        "id": CAMPAIGN, "kind": "campaign", "format": 1, "engine": "0.1",
        "author": "Maps & Math Rocks", "tags": ["short"],
        "chapters": [{"id": CHAPTER, "maps": CHAPTER_MAPS}],
        "start": CHAPTER, "gm_screen": "plains",
        "defeat": "continues",
    }
    write("pack.json", as_json(manifest))

    made = []
    for map_id, body in MAPS.items():
        if not os.path.exists(os.path.join(ROOT, "maps", map_id + ".map")):
            write(f"maps/{map_id}.map", as_json(body))
            made.append(map_id)
    missing = [m for m in CHAPTER_MAPS if not os.path.exists(os.path.join(ROOT, "maps", m + ".map"))]
    if missing:
        raise SystemExit("no map file for: " + ", ".join(missing))

    write("encounters/encounters.json", as_json(ENCOUNTERS))

    rows = {
        f"campaign.{CAMPAIGN}.name": NAME,
        f"campaign.{CAMPAIGN}.description": BLURB,
        f"quest.{CAMPAIGN}.{CHAPTER}.title": NAME,
    }
    rows.update({f"actor.{a}.name": n for a, n in ACTORS.items()})
    rows.update(ENCOUNTER_LINES)
    rows.update(lines)

    out = io.StringIO()
    w = csv.writer(out, lineterminator="\n")
    w.writerow(["keys", "en"])
    for key in sorted(rows):
        w.writerow([key, rows[key]])
    write(f"locale/{CAMPAIGN}.csv", out.getvalue())

    print(f"{CAMPAIGN}: {len(rows)} locale rows, {len(nodes)} nodes; new maps: {', '.join(made) or 'none'}")


if __name__ == "__main__":
    main()
