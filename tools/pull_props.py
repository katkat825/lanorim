"""Pull the map builder's palette out of the asset packs and into the game.

Reads content/srd/props/props.json - every prop names its pack zip (in lanorim/assets, gitignored) and
the model inside it - and copies each model into game/models/props/<pack>/ with every file the model
refers to (a .gltf's .bin buffers and its texture images; an .obj's .mtl and the pictures it names),
keeping their names, so the model still finds them. One folder per pack, because two packs can both
ship a texture called 'texture.png'.

A pack with no glTF (Quaternius's Farm Buildings, House Interior, Modular Ruins, Survival, Textured
Trees) gives its .obj and .mtl (cc_task_l Part 3). Blender wrote the .mtl's colours in linear light and
Godot reads them as sRGB, so each Kd is brought to sRGB on the copy (as pull-models.ps1 does); and the
textured packs' .mtl files name no picture at all, so the bark and leaf textures are written in here
(TEXTURES), with Kd white so the picture is the colour.

Run from lanorim/:  python tools/pull_props.py            (pull)
                    python tools/pull_props.py --dry-run  (say what, and how big, without copying)
                    python tools/pull_props.py --force    (pull again over files already in game/)

A FILE ALREADY IN game/ IS LEFT AS IT IS (cc_task_l): it may have been baked to the palette or shrunk, and the zip's
copy is neither. Only what isn't there yet is written, unless --force.

The game finds a prop's model as res://models/props/<pack folder>/<model file name> - see
game/Board/PropModels.cs, which computes the same folder name as folder_of() below.
"""
import json
import os
import posixpath
import re
import subprocess
import sys
import zipfile

ROOT = os.path.join(os.path.dirname(__file__), "..")
ASSETS = os.path.join(ROOT, "assets")
OUT = os.path.join(ROOT, "game", "models", "props")


def folder_of(pack):
    """kaykit_dungeon_pack_1_1_free, fantasy_props_megakit_standard - lower case, a-z 0-9 and _ only. A Google Drive
    download's stamp ("Survival Pack - Sept 2020-20260921T173759Z-1-001.zip") is left off: survival_pack_sept_2020."""
    name = pack[:-4] if pack.lower().endswith(".zip") else pack
    name = re.sub(r"-\d{8}T\d{6}Z-\d+-\d+$", "", name)
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


# A PICTURE BIGGER THAN THIS A SIDE IS BROUGHT DOWN TO IT (tools/shrink-texture.ps1): ART_DIRECTION section 9's 1K for
# walls and props. The megakits ship 2K and 4K, which is most of their megabytes
MAX_SIDE = 1024


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


# the picture each textured .mtl material should have had, by material name, first found in the pack wins
TEXTURES = {
    "Birch_Bark": ["Birch_Bark.png"],
    "Birch_Leaves": ["Birch_Leaves_Green.png"],
    "Tree_Leaves": ["Tree_Leaves.png"],
    "Pine_Leaves": ["Pine_Leaves.png"],
    "Bark": ["Tree_Bark.jpg", "Bark_Texture.jpg"],
    "Texture_Leaves": ["Leaf_Texture.png"],
    "Leaf_Texture": ["Leaf_Texture.png"],
}


def srgb(c):
    return 12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def mtl_text(zf, inner, names):
    """an .mtl with every Kd in sRGB, and a map_Kd for each textured material; and the pictures it now names"""
    by_base = {posixpath.basename(n): n for n in names}
    out, pictures, material = [], {}, None
    for line in zf.read(inner).decode("utf-8", "ignore").splitlines():
        parts = line.split()
        if parts[:1] == ["newmtl"]:
            material = parts[1] if len(parts) > 1 else None
        if parts[:1] == ["Kd"] and len(parts) >= 4:
            picture = next((by_base[t] for t in TEXTURES.get(material, []) if t in by_base), None)
            if picture:
                out.append("Kd 1.000000 1.000000 1.000000")
                out.append("map_Kd " + posixpath.basename(picture))
                pictures[picture] = None
                continue
            line = "Kd " + " ".join("%.6f" % srgb(float(v)) for v in parts[1:4])
        out.append(line)
    return ("\n".join(out) + "\n").encode("ascii", "ignore"), pictures


def needs(zf, inner):
    """the model (its text, with the unused maps taken out) and every file it still points at."""
    files = {}
    if inner.lower().endswith(".obj"):
        files[inner] = None
        mtl = posixpath.splitext(inner)[0] + ".mtl"
        if mtl in zf.namelist():
            text, pictures = mtl_text(zf, mtl, zf.namelist())
            files[mtl] = text
            files.update(pictures)
        return files
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


def shrink(pictures):
    """the big pictures, down to MAX_SIDE a side, in place"""
    if not pictures:
        return
    script = os.path.join(ROOT, "tools", "shrink-texture.ps1")
    listed = ",".join("'" + os.path.abspath(p).replace("'", "''") + "'" for p in pictures)
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                    f"& '{script}' -Max {MAX_SIDE} -Path {listed}"], check=True, stdout=subprocess.DEVNULL)


def main():
    dry = "--dry-run" in sys.argv
    force = "--force" in sys.argv
    props = json.load(open(os.path.join(ROOT, "content", "srd", "props", "props.json"), encoding="utf-8"))["props"]

    by_pack = {}
    for prop in props:
        by_pack.setdefault(prop["pack"], set()).add(prop["model"])

    total = 0
    landed = 0
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
            pictures = []
            for f, text in sorted(wanted.items()):
                to = os.path.join(into, posixpath.basename(f))
                if os.path.exists(to) and not force:
                    continue
                with open(to, "wb") as out:
                    out.write(text if text is not None else zf.read(f))
                if to.lower().endswith((".png", ".jpg", ".jpeg")) and os.path.getsize(to) > 256 * 1024:
                    pictures.append(to)
            shrink(pictures)
            landed += sum(os.path.getsize(os.path.join(into, posixpath.basename(f))) for f in wanted)

    print(f"{'total':45}      {total / 1e6:6.1f} MB" + ("" if dry else f", {landed / 1e6:.1f} MB once shrunk"))

    for m in missing:
        print("MISSING", m)

    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
