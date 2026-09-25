"""Pull the map builder's palette out of the asset packs and into the game.

Reads content/srd/props/props.json - every prop names its pack zip (in lanorim/assets, gitignored) and
the model inside it - and copies each model into game/models/props/<pack>/ with every file the model
refers to (a .gltf's .bin buffers and its texture images), keeping their names, so the model still
finds them. One folder per pack, because two packs can both ship a texture called 'texture.png'.

Run from lanorim/:  python tools/pull_props.py            (pull)
                    python tools/pull_props.py --dry-run  (say what, and how big, without copying)

The game finds a prop's model as res://models/props/<pack folder>/<model file name> - see
game/Board/PropModels.cs, which computes the same folder name as folder_of() below.
"""
import json
import os
import posixpath
import re
import sys
import zipfile

ROOT = os.path.join(os.path.dirname(__file__), "..")
ASSETS = os.path.join(ROOT, "assets")
OUT = os.path.join(ROOT, "game", "models", "props")


def folder_of(pack):
    """kaykit_dungeon_pack_1_1_free, fantasy_props_megakit_standard - lower case, a-z 0-9 and _ only."""
    name = pack[:-4] if pack.lower().endswith(".zip") else pack
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


def colour_only(doc):
    """THE PAINTED SHADER RELIGHTS EVERYTHING (ART_DIRECTION section 8), so a pack's normal, ORM and
    emissive maps are weight the game never uses - in the Fantasy Props kit, three quarters of the
    megabytes. They are dropped from the model here, and only the base colour is kept."""
    for material in doc.get("materials", []):
        for key in ("normalTexture", "occlusionTexture", "emissiveTexture"):
            material.pop(key, None)
        material.get("pbrMetallicRoughness", {}).pop("metallicRoughnessTexture", None)

    # which textures are still used, renumbered, and the images behind them
    used = sorted({m["pbrMetallicRoughness"]["baseColorTexture"]["index"]
                   for m in doc.get("materials", [])
                   if "baseColorTexture" in m.get("pbrMetallicRoughness", {})})
    texture_at = {old: new for new, old in enumerate(used)}
    textures = [doc["textures"][i] for i in used] if used else []
    images_used = sorted({t["source"] for t in textures if "source" in t})
    image_at = {old: new for new, old in enumerate(images_used)}

    for t in textures:
        if "source" in t:
            t["source"] = image_at[t["source"]]

    for m in doc.get("materials", []):
        colour = m.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        if colour is not None:
            colour["index"] = texture_at[colour["index"]]

    if textures:
        doc["textures"] = textures
        doc["images"] = [doc["images"][i] for i in images_used]
    else:
        doc.pop("textures", None)
        doc.pop("images", None)

    return doc


def needs(zf, inner):
    """the model (its text, with the unused maps taken out) and every file it still points at."""
    files = {}
    if inner.lower().endswith(".gltf"):
        doc = colour_only(json.loads(zf.read(inner).decode("utf-8")))
        base = posixpath.dirname(inner)
        files[inner] = json.dumps(doc).encode("utf-8")
        for entry in doc.get("buffers", []) + doc.get("images", []):
            uri = entry.get("uri", "")
            if uri and not uri.startswith("data:"):
                files[posixpath.normpath(posixpath.join(base, uri))] = None
    else:
        files[inner] = None
    return files


def main():
    dry = "--dry-run" in sys.argv
    props = json.load(open(os.path.join(ROOT, "content", "srd", "props", "props.json"), encoding="utf-8"))["props"]

    by_pack = {}
    for prop in props:
        by_pack.setdefault(prop["pack"], set()).add(prop["model"])

    total = 0
    missing = []

    for pack, models in sorted(by_pack.items()):
        path = os.path.join(ASSETS, pack)
        if not os.path.exists(path):
            missing.append(pack)
            continue

        into = os.path.join(OUT, folder_of(pack))
        with zipfile.ZipFile(path) as zf:
            names = set(zf.namelist())
            wanted = {}
            for model in models:
                if model not in names:
                    missing.append(f"{pack}: {model}")
                    continue
                for f, text in needs(zf, model).items():
                    if f in names:
                        wanted[f] = text

            size = sum(len(t) if t is not None else zf.getinfo(f).file_size for f, t in wanted.items())
            total += size
            print(f"{folder_of(pack):45} {len(wanted):3} files  {size / 1e6:6.1f} MB")

            if dry:
                continue

            os.makedirs(into, exist_ok=True)
            for f, text in sorted(wanted.items()):
                with open(os.path.join(into, posixpath.basename(f)), "wb") as out:
                    out.write(text if text is not None else zf.read(f))

    print(f"{'total':45}      {total / 1e6:6.1f} MB")

    for m in missing:
        print("MISSING", m)

    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
