"""Regenerate the spell-name review tables from the data and the locale.

    python tools/spell_review.py

Writes _design_docs/REVIEW_spell_names.md and rewrites the "Renamed approximations - as built" table
at the end of docs/v1_spell_list.md (and its build-status note). A development helper written for the
2026-09-24 run; the game never runs it. The "why" and "mechanic" sentences below are hand-written and
belong to the spells as they stood that day - update them with the data.
"""
import csv
import glob
import json
import re
import sys

sys.dont_write_bytecode = True

ROOT = __file__.replace('\\', '/').rsplit('/tools/', 1)[0] + '/'

# why each remaining approximation cannot be the SRD spell, in one plain sentence
WHY = {
    'find_familiar': "v1 has fixed familiar archetypes and a scouting menu, not any creature of your choosing (decided in v1_spell_list.md).",
    'fly': "v1 has no general 3D flight; flying is a terrain-ignoring state on authored maps (decided).",
    'slow': "Everything else is built, but its clause on casting spells with a Somatic component isn't certain from memory and v1 doesn't track components.",
    'banishment': "v1 has no demiplanes; the target is marked out of the fight instead (decided). Could now be faithful - see Questions.",
    'wall_of_force': "Walls snap to grid edges in fixed shapes; SRD's freely shaped panels aren't possible (decided).",
    'teleport': "v1 travels only to discovered places, and the mishap table is kept only for unvisited ones (decided).",
    'dominate_monster': "v1 gives a short command set, not full obedience of another creature (decided).",
    'wish': "No free-text reality change; it duplicates a spell or picks from an authored menu (decided).",
    'feather_fall': "v1 has no falling or altitude, so there is nothing for its reaction to answer.",
    'heat_metal': "Items and statblocks don't say what is metal, so there is nothing for it to heat.",
    'suggestion': "Its 25-word course of action is free text that a fight cannot play.",
    'gaseous_form': "Its 10-foot fly speed runs into the no-general-flight decision; the rest could be built.",
    'polymorph': "v1 uses curated form cards, not any beast's statblock (decided).",
    'plane_shift': "v1 travels only to campaign-authored places, and has no banishing to another plane (decided).",
    'reverse_gravity': "v1 has no altitude or falling damage (the no-physics-sandbox decision).",
    'antimagic_field': "It suppresses (not ends) every spell and magic item inside it - a large engine change not made this run.",
    'earthquake': "No fissures, collapsing structures or physics sandbox (decided).",
    'maze': "No demiplanes (decided); could now be faithful - see Questions.",
    'shapechange': "v1 uses curated high-level form cards, not any creature's statblock (decided).",
    'true_polymorph': "v1 uses curated forms and authored hooks, not free transformation (decided).",
}

# what made each spell faithful this run
MECHANIC = {
    'charm_person': "Charmed condition (with who charmed it), creature tags (Humanoid), advantage on the save while fought, ends on the caster's side's damage",
    'sleep': "Incapacitated then Unconscious (escalating repeat save), ends on damage, shake-awake action, Trance/sleepless immunity",
    'hold_person': "Paralyzed condition (crit from 5 feet), Humanoid tag gate",
    'hold_monster': "Paralyzed condition",
    'mirror_image': "Decoys on a boon: a d6 per duplicate on each hit",
    'haste': "Speed doubling, the narrow extra action, effects on the spell's end (lethargy)",
    'hypnotic_pattern': "Charmed + Incapacitated + speed 0, ends on damage, shake awake, needs sight",
    'greater_restoration': "Modes (a choice at the cast), Charmed/Petrified, curse tags, ability-score restore",
    'sunbeam': "Blinded until the caster's next turn (timed conditions)",
    'sunburst': "Blinded with a repeat save; dispels magical darkness",
    'shillelagh': "Weapon rewrites (ability, die by cantrip tier, force)",
    'spare_the_dying': "Stable state; cantrip range scaling",
    'produce_flame': "Lasting (non-concentration) repeatable spells; repeat-only effects",
    'true_strike': "Strike primitive: a weapon attack made by a spell",
    'chromatic_orb': "Damage type chosen at the cast; leaping on matched dice (dice faces from the resolver)",
    'command': "Direct primitive: the five words play the creature's next turn",
    'divine_smite': "Struck trigger: a bonus action right after your own melee hit; creature tags (fiend, undead)",
    'fog_cloud': "Obscurement (heavily obscured zones block sight); radius by slot level",
    'goodberry': "Conjure primitive: items into the pack that vanish on a long rest; using consumables",
    'hideous_laughter': "Pinned Prone + Incapacitated, a repeat save that damage also calls (with advantage)",
    'searing_smite': "Struck trigger; recurring damage with a save after each burn",
    'aid': "Hit point maximum raised by a boon",
    'blindness_deafness': "Blinded and Deafened conditions; modes",
    'darkness': "Magical darkness (heavily obscured, Truesight sees through)",
    'enlarge_reduce': "Modes, size steps, Strength leans, weapon-only dice, a save only for the unwilling",
    'flaming_sphere': "A moved zone that rams the first creature in its way",
    'pass_without_trace': "Auras: a boon on allies while they stand inside an emanation",
    'call_lightning': "Zone on the caster; later bolts must fall under it; fight setting (storm)",
    'fear': "Dropping what is held, a forced flight each turn, a repeat save only out of sight",
    'remove_curse': "Spells tagged as curses; a dispel of curses only",
    'sleet_storm': "Obscurement, difficult terrain, a failed save breaking concentration",
    'stinking_cloud': "Obscurement; 'until the end of this turn'; no actions",
    'black_tentacles': "(already possible: zones on appear/enter/end of turn, escape checks)",
    'death_ward': "A 0-HP intercept and instant-kill negation",
    'vitriolic_sphere': "Delayed damage at the end of the target's next turn",
    'cloudkill': "Obscurement; a zone that drifts away from its caster",
    'raise_dead': "Reviving the dead; a penalty that eases each long rest",
    'telekinesis': "Forced movement, size gate, disarm contest, one target at a time",
    'blade_barrier': "Placed walls (lines and rings of squares), three-quarters cover, difficult terrain",
    'flesh_to_stone': "Petrified; three-successes/three-failures escalation; speed 0 on a success; Construct auto-save",
    'globe_of_invulnerability': "A zone that blocks spells of a level or lower cast from outside",
    'true_seeing': "Truesight (sees the Invisible and through magical darkness)",
    'forcecage': "Enclosures: real board edges (bars or solid) and a Charisma save for magic travel out",
    'power_word_stun': "Hit-point gates; a save at the end of each turn with no first save",
    'wall_of_fire': "Placed walls with a burning side; opaque walls block sight",
}

# names this run could not confirm are in SRD 5.2.1 (they are not in the 5.1 SRD as I remember it)
UNSURE = {
    'Hex': "a 2014 PHB warlock spell; not in SRD 5.1",
    'Hellish Rebuke': "a 2014 PHB warlock spell; not in SRD 5.1",
    'Chromatic Orb': "2014 PHB; I can't confirm it is in either SRD",
    'Divine Smite': "a class feature in 5.1; a spell in the 2024 PHB - confirm the SRD has the spell",
    'Searing Smite': "2014 PHB; confirm it is in SRD 5.2.1",
    'Vitriolic Sphere': "from Xanathar's in 2014, in the 2024 PHB; confirm it is in SRD 5.2.1",
    "Hunter's Mark": "confirm it is in SRD 5.2.1 (I believe 5.1 had it)",
    'Eldritch Blast': "confirm (I believe 5.1 had it)",
    'Blight': "confirm",
    'Shatter': "confirm",
}


def loc():
    with open(ROOT + 'game/locale/game.csv', encoding='utf-8', newline='') as f:
        return {r[0]: r[1] for r in csv.reader(f) if r}


def spells():
    for f in sorted(glob.glob(ROOT + 'content/srd/spells/*.json')):
        tier = 'SHOULD' if 'should_' in f else 'MUST'
        for s in json.load(open(f, encoding='utf-8'))['spells']:
            yield tier, s


def old_names():
    """the names the approximations shipped under before the run, from the doc's old table"""
    text = open(ROOT + 'docs/v1_spell_list.md', encoding='utf-8').read()
    return dict(re.findall(r'\| `([a-z_]+)` \| ([^|]+?) \|', text))


def srd_title(sid):
    words = sid.split('_')
    small = {'of', 'the', 'to', 'with', 'without'}
    out = [w if (i and w in small) else w.capitalize() for i, w in enumerate(words)]
    title = ' '.join(out)
    return {'Blindness Deafness': 'Blindness/Deafness', 'Enlarge Reduce': 'Enlarge/Reduce'}.get(title, title)


def main():
    english = loc()
    was = old_names()

    def name(spell):
        return english['spell.' + spell['id'] + '.name']
    everything = list(spells())

    not_srd = [(t, s) for t, s in everything if s.get('not_in_srd')]
    approx = sorted([(t, s) for t, s in everything if s.get('approximated')],
                    key=lambda p: (p[0] != 'MUST', p[1]['level'], p[1]['id']))
    restored = sorted([(t, s) for t, s in everything if s['id'] in MECHANIC and not s.get('approximated')],
                      key=lambda p: (p[1]['level'], p[1]['id']))

    lines = [
        '# Review — spell names (2026-09-24)',
        '',
        '*For Kathleen. Generated by `tools/spell_review.py` from the spell data and `game/locale/game.csv`',
        'at the end of Tier 1 of the unattended run. The rule it serves: a spell that is not exactly the SRD',
        '5.2.1 spell never wears an SRD name (`NoApproximationIsShownUnderAnSrdName`), and a spell that is',
        'faithful always does (`EveryFaithfulSpellIsShownUnderItsSrdName`). New names are fantasy names of my',
        'own and echo no WotC name that I know of — change any you like in `game/locale/game.csv`.*',
        '',
        f'**Where it stands:** {len(everything)} spells built. {len(approx)} ship renamed as approximations, '
        f'{len(not_srd)} are not in the SRD at all and ship under original names, and the rest are the SRD '
        'spell under its SRD name. This run made '
        f'{len(restored)} approximations faithful.',
        '',
        '## Table 1 — renamed, not in SRD 5.2.1',
        '',
        '| id | new name | what it does |',
        '|---|---|---|',
    ]
    for t, s in not_srd:
        desc = english['spell.' + s['id'] + '.description']
        lines.append(f"| `{s['id']}` | {name(s)} | {desc} |")

    lines += [
        '',
        '## Table 2 — renamed approximations',
        '',
        '| id | SRD name | new name | tier | lvl | why it can\'t be faithful |',
        '|---|---|---|---|---|---|',
    ]
    for t, s in approx:
        sid = s['id']
        lines.append(f"| `{sid}` | {srd_title(sid)} | {name(s)} | {t} | "
                     f"{s['level']} | {WHY.get(sid, '(see the note in the data)')} |")

    lines += [
        '',
        '## Table 3 — restored to the SRD name this run',
        '',
        '| id | SRD name | shipped as before | the mechanic that made it faithful |',
        '|---|---|---|---|',
    ]
    for t, s in restored:
        sid = s['id']
        lines.append(f"| `{sid}` | {name(s)} | {was.get(sid, '-')} | {MECHANIC[sid]} |")

    lines += [
        '',
        '## Names I am not sure are in SRD 5.2.1',
        '',
        '`content/srd/reference/spell_names.json` was written from memory, not copied from the PDF, and the',
        'naming tests trust it. These faithful spells ship under their SRD names; please check each against',
        'the SRD 5.2.1 PDF. If one is missing from the SRD it has to be renamed like Table 1.',
        '',
        '| name | why I\'m unsure |',
        '|---|---|',
    ]
    for n, why in UNSURE.items():
        lines.append(f'| {n} | {why} |')
    lines += [
        '',
        'Also: two names are capitalised "Speak With Animals" and "Speak With Dead" in the locale; the SRD',
        'writes "with" in lower case. Cosmetic.',
        '',
    ]
    open(ROOT + '_design_docs/REVIEW_spell_names.md', 'w', encoding='utf-8').write('\n'.join(lines))

    # the table at the end of v1_spell_list.md, and the status note at its top
    doc = open(ROOT + 'docs/v1_spell_list.md', encoding='utf-8').read()
    head = doc[:doc.index('## Renamed approximations')]
    must = sum(1 for t, _ in approx if t == 'MUST')
    table = [
        '## Renamed approximations — as built (regenerated 2026-09-24, end of Tier 1)',
        '',
        f'*Generated by `tools/spell_review.py` from `content/srd/spells/*.json` (`"approximated": true`) and '
        f'`game/locale/game.csv`. {len(approx)} of the {len(everything)} built spells ({must} MUST, '
        f'{len(approx) - must} SHOULD) ship under a new name; why each deviates is in its `note` fields and in '
        '`_design_docs/REVIEW_spell_names.md`. Two more (Dissonant Whispers, Dragon\'s Breath) are not SRD '
        'spells and ship under original names.*',
        '',
        '| Tier | Lvl | SRD spell (id) | Ships as |',
        '|---|---|---|---|',
    ]
    for t, s in approx:
        table.append(f"| {t} | {s['level']} | `{s['id']}` | {name(s)} |")
    table += ['', '| Not in SRD | Lvl | v1 list name (id) | Ships as |', '|---|---|---|---|']
    for t, s in not_srd:
        table.append(f"| {t} | {s['level']} | `{s['id']}` | {name(s)} |")
    doc = head + '\n'.join(table) + '\n'
    status = (f"> **Build status (2026-09-24, end of Tier 1):** all 131 are built. **Dissonant Whispers** and "
              f"**Dragon's Breath** are not in SRD 5.2.1 and ship as working spells under original names "
              f"(Murmur of Dread, Wyrmbreath Boon). **{len(approx)} ship as renamed approximations** "
              f"({must} MUST, {len(approx) - must} SHOULD) — down from 65 this morning; the other "
              f"{len(everything) - len(approx) - len(not_srd)} are the SRD spell under its SRD name. The table at "
              f"the end lists them.")
    doc = re.sub(r'> \*\*Build status \(2026-09-24[^\n]*', lambda m: status, doc, count=1)
    open(ROOT + 'docs/v1_spell_list.md', 'w', encoding='utf-8').write(doc)
    print(len(approx), len(not_srd), len(restored))


if __name__ == '__main__':
    main()
