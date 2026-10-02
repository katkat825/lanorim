"""Write the SRD's class lists into content/srd/reference/spell_names.json.

    python tools/srd_spell_lists.py

Reads _design_docs/SRD_CC_v5.2.1.txt, the SRD's spell chapter: every spell is a heading followed by a
'Level N School (Classes)' or 'School Cantrip (Classes)' line. Writes each spell's classes, as printed,
under "lists", beside the names the file already holds (SrdSpellNames reads both). Fails if a name the
file lists has no class line, or the other way round. A development helper (cc_task_f 1.2); the game never
runs it.
"""
import json
import re
import sys

sys.dont_write_bytecode = True

ROOT = __file__.replace('\\', '/').rsplit('/tools/', 1)[0] + '/'
SRD = ROOT + '_design_docs/SRD_CC_v5.2.1.txt'
OUT = ROOT + 'content/srd/reference/spell_names.json'

CLASS_LINE = re.compile(r'^(?:Level \d [A-Z][a-z]+|[A-Z][a-z]+ Cantrip) \(([A-Za-z, ]+)\)\s*$')


def straight(name):
    return name.replace('’', "'").strip()


def lists():
    lines = open(SRD, encoding='utf-8').read().splitlines()
    found = {}
    for i, line in enumerate(lines):
        match = CLASS_LINE.match(line)
        if not match or i == 0:
            continue
        name = straight(lines[i - 1])
        found[name] = [c.strip().lower() for c in match.group(1).split(',')]
    return found


def main():
    data = json.load(open(OUT, encoding='utf-8'))
    found = lists()
    names = data['names']
    missing = [n for n in names if n not in found]
    extra = [n for n in found if n not in names]
    if missing or extra:
        sys.exit(f'names and class lines disagree: no class line for {missing}; not a listed name: {extra}')
    data['lists'] = {n: found[n] for n in names}
    data['_lists'] = ("Each spell's classes, as the SRD prints them under its heading ('Level 1 Transmutation "
                      "(Bard, Druid, Ranger, Wizard)'), lowercased. Written by tools/srd_spell_lists.py from the "
                      "SRD text. Which of these feed a v1 class is docs/v1_class_roster.md's, and "
                      "SpellClassListTests holds the data to it.")
    # the names as they were, then one spell to a line
    head = json.dumps({k: v for k, v in data.items() if k != 'lists'}, ensure_ascii=False, indent=2)
    rows = ',\n'.join(f'    {json.dumps(n, ensure_ascii=False)}: {json.dumps(c)}' for n, c in data['lists'].items())
    text = head[:head.rfind('}')].rstrip() + ',\n  "lists": {\n' + rows + '\n  }\n}\n'
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print(f'{len(names)} spells, class lists written')


if __name__ == '__main__':
    main()
