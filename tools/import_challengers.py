"""Import the arena's challenger lists from Kathleen's vault into a campaign pack (cc_task_cg Part 1).

    python tools/import_challengers.py PACK                 read the vault, write the pack's challengers and English
    python tools/import_challengers.py PACK --check         the same, writing nothing; exit 1 if the pack is behind
    python tools/import_challengers.py PACK --vault "D:/somewhere/Challengers"

PACK is a campaign folder (the one with pack.json). The vault (../campaign_authoring/The Crucible/Challengers by
default) is READ, never written: one markdown file per list, `Normal Fights.md`, each challenger type a `# heading`:

    # goblin warrior                       the type: its id is the heading in snake case, goblin_warrior
    stats: a single goblin warrior         who it puts in the arena (the shapes are below)
    ## descriptions                        numbered, one per line
    1. A wiry goblin with a chipped sword.
    ## names                               numbered; any name goes with any description
    1. Grik the Knife                      (for a team, these are the team's names)

The `stats:` line, in one of three shapes (docs/campaign_authoring_rules.md section 13):
    a single goblin warrior / mage           one monster: the challenger's name is the monster's in the fight
    a single thug or scout / gladiator or berserker     one of these, whichever fits the hero's level when it's drawn
    two guards / 2 guards / two guards and a goblin warrior     a fixed group; the monsters keep their own names
    team of goblin warrior, goblin archer   drawn from these to a budget for the hero's level when the roster is drawn

A heading with no stats: line is a SHARED LIST, names and descriptions for any stat block: its '## Single Challengers'
and '## Teams' sections, each with '### Names' and '### Descriptions'. Every single draws from the singles' list beside
its own, and every group and team from the teams' list.

A monster is named by its English statblock name, matched against monster.<id>.name in the game's locale and the
pack's (case and a plural -s or -es don't matter), or by its id written as words. A name that matches nothing, or more
than one monster, is a problem that names the file and the line; every problem is listed, and then nothing is written.

It writes:
  - PACK/challengers/<file>.json, one per vault file (normal_fights.json), in the shape ChallengerReader reads;
  - the names and descriptions into PACK/locale/<pack id>.csv as challenger.<type>.name.<NNN> and
    challenger.<type>.description.<NNN>, every other row byte for byte.

**bold** and *italics* are taken off (the roster shows plain text).
"""

import argparse
import csv
import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VAULT = os.path.join(os.path.dirname(ROOT), 'campaign_authoring', 'The Crucible', 'Challengers')
GAME_LOCALE = os.path.join(ROOT, 'game', 'locale', 'game.csv')

NUMBERS = {'a': 1, 'an': 1, 'one': 1, 'single': 1, 'a single': 1, 'two': 2, 'three': 3, 'four': 4, 'five': 5,
           'six': 6, 'seven': 7, 'eight': 8, 'nine': 9, 'ten': 10}

# a word that says one, before an 'or' list: 'a single thug or scout'
ONE = ('a single', 'a', 'an', 'one', 'single')

NUMBERED = re.compile(r'^\s*(\d+)\.\s*(.*?)\s*$')
HEADING = re.compile(r'^#\s+(.*?)\s*$')
SECTION = re.compile(r'^##\s+(.*?)\s*$')
LIST = re.compile(r'^###\s+(.*?)\s*$')
STATS = re.compile(r'^\s*stats\s*:\s*(.*?)\s*$', re.IGNORECASE)
COUNTED = re.compile(r'^(a single|an|a|one|single|two|three|four|five|six|seven|eight|nine|ten|\d+)\s+(.+)$',
                     re.IGNORECASE)


class Problem(Exception):
    pass


def snake(words):
    return re.sub(r'[^a-z0-9]+', '_', words.strip().lower()).strip('_')


def plain(text):
    return re.sub(r'\*\*(.+?)\*\*|\*(.+?)\*', lambda m: m.group(1) or m.group(2), text).strip()


def read_csv(path):
    if not os.path.exists(path):
        return {}
    with io.open(path, encoding='utf-8', newline='') as f:
        return {row[0]: row[1] for row in csv.reader(f) if len(row) >= 2}


def monster_names(pack_locale):
    """every way a monster can be named: its locale name and its id as words, lowercased, to the ids"""
    names = {}
    for table in (read_csv(GAME_LOCALE), read_csv(pack_locale)):
        for key, text in table.items():
            parts = key.split('.')
            if len(parts) == 3 and parts[0] == 'monster' and parts[2] == 'name':
                for said in (text.strip().lower(), parts[1].replace('_', ' ')):
                    names.setdefault(said, set()).add(parts[1])
    return names


def monster(name, names, where):
    said = re.sub(r'\s+', ' ', name.strip().lower())
    for form in (said, said[:-2] if said.endswith('es') else None, said[:-1] if said.endswith('s') else None):
        if form and form in names:
            found = names[form]
            if len(found) > 1:
                raise Problem(f"{where}: '{name}' could be any of {', '.join(sorted(found))} - name the statblock")
            return next(iter(found))
    raise Problem(f"{where}: '{name}' is not a monster's statblock name in the game or this pack")


def parts(text):
    return [p.strip() for p in re.split(r',|\band\b', text) if p.strip()]


def counted(part, names, where):
    """'two guards' is ('guard', 2); a bare 'mage' is one"""
    match = COUNTED.match(part)
    if not match:
        return monster(part, names, where), 1
    word = match.group(1).lower()
    count = int(word) if word.isdigit() else NUMBERS[word]
    if count < 1:
        raise Problem(f"{where}: '{part}' is nobody")
    return monster(match.group(2), names, where), count


def stats(line, names, where):
    """the shape and the monsters of one stats: line. Every monster it can't name is a problem, not just the first"""
    text = line.strip().rstrip('.')
    unknown = []

    def named(part):
        try:
            return counted(part, names, where)
        except Problem as problem:
            unknown.append(str(problem))
            return None, 1

    team = re.match(r'^(a\s+)?team\s+of\s+(.+)$', text, re.IGNORECASE)
    choices = [c.strip() for c in re.split(r'\s+or\s+', text, flags=re.IGNORECASE) if c.strip()]

    if team:
        pool = []
        for part in parts(team.group(2)):
            found, _ = named(re.sub(r'^(a|an)\s+', '', part, flags=re.IGNORECASE))
            if found and found not in pool:
                pool.append(found)
        shape, monsters = 'team', [{'monster': m} for m in pool]
    elif len(choices) > 1:
        # 'a single thug or scout', 'gladiator or berserker': one of these, whichever fits the hero's level
        first = COUNTED.match(choices[0])
        if first and first.group(1).lower() in ONE:
            choices[0] = first.group(2)
        either = []
        for choice in choices:
            if len(parts(choice)) > 1 or (COUNTED.match(choice) and COUNTED.match(choice).group(1).lower() not in ONE):
                raise Problem(f"{where}: '{text}' - 'or' is between single monsters ('a single thug or scout')")
            found, _ = named(choice)
            if found and found not in either:
                either.append(found)
        shape, monsters = 'single', [{'monster': m, 'count': '1'} for m in either]
    else:
        monsters = []
        for part in parts(text):
            found, count = named(part)
            if found:
                monsters.append({'monster': found, 'count': str(count)})
        if not monsters and not unknown:
            raise Problem(f"{where}: the stats line is empty")
        shape = 'single' if sum(int(b['count']) for b in monsters) == 1 else 'group'

    if unknown:
        raise Problem('\n'.join(unknown))
    return shape, monsters


def shared_shape(section, where):
    """a generic list's section, '## Single Challengers' or '## Teams', says which shape draws from it"""
    words = section.lower()
    if 'single' in words:
        return 'single'
    if 'team' in words:
        return 'team'
    raise Problem(f"{where}: '## {section}' - a list for any stat block is '## Single Challengers' or '## Teams'")


def blocks_of(path, problems):
    """the file's # headings, each with its stats: line and its ## sections' numbered names and descriptions"""
    file = os.path.basename(path)
    blocks, current = [], None
    with io.open(path, encoding='utf-8') as f:
        lines = f.read().splitlines()
    for number, line in enumerate(lines, 1):
        where = f'{file}:{number}'
        heading = HEADING.match(line)
        if heading:
            current = {'heading': heading.group(1), 'where': where, 'stats': None, 'sections': [], 'list': None}
            blocks.append(current)
            continue
        if current is None:
            continue
        stat, section, sub, item = STATS.match(line), SECTION.match(line), LIST.match(line), NUMBERED.match(line)
        if stat:
            current['stats'] = (stat.group(1), where)
        elif section:
            current['sections'].append({'name': section.group(1), 'where': where, 'names': [], 'descriptions': []})
            current['list'] = snake(section.group(1)) if snake(section.group(1)) in ('names', 'descriptions') else None
        elif sub:
            current['list'] = snake(sub.group(1))
            if current['list'] not in ('names', 'descriptions'):
                problems.append(f"{where}: '### {sub.group(1)}' - a list is '### Names' or '### Descriptions'")
                current['list'] = None
        elif item and current['list'] and current['sections']:
            current['sections'][-1][current['list']].append(plain(item.group(2)))
    return blocks


def read_list(path, names, problems):
    """one vault file: its types (a # heading with a stats: line) and its shared lists (a # heading with none: each
    '## Single Challengers' or '## Teams' section's '### Names' and '### Descriptions' are drawn from by every type of
    that shape, beside the type's own)"""
    file = os.path.basename(path)
    types = []
    for b in blocks_of(path, problems):
        try:
            if b['stats']:
                shape, monsters = stats(b['stats'][0], names, b['stats'][1])
                lists = {'names': [], 'descriptions': []}
                for section in b['sections']:
                    if snake(section['name']) not in lists:
                        raise Problem(f"{section['where']}: '## {section['name']}' - a type has '## descriptions' and "
                                      "'## names'")
                    for what in lists:
                        lists[what] += section[what]
                types.append({'id': snake(b['heading']), 'heading': b['heading'], 'where': b['where'], 'shape': shape,
                              'monsters': monsters, **lists})
                continue
            kept = [s for s in b['sections'] if s['names'] or s['descriptions']]
            if not kept:
                raise Problem(f"{b['where']}: '# {b['heading']}' has no stats: line")
            for section in kept:
                shape = shared_shape(section['name'], section['where'])
                types.append({'id': snake(b['heading']) + '_' + shape, 'heading': f"{b['heading']} / {section['name']}",
                              'where': section['where'], 'shape': shape, 'monsters': [],
                              'names': section['names'], 'descriptions': section['descriptions']})
        except Problem as problem:
            problems.extend(str(problem).split('\n'))
    for t in types:
        for what in ('descriptions', 'names'):
            if not t[what]:
                problems.append(f"{t['where']}: '# {t['heading']}' has no numbered {what}")
    ids = [t['id'] for t in types]
    for twice in sorted({i for i in ids if ids.count(i) > 1}):
        problems.append(f"{file}: two types are called '{twice}'")
    return types


def challengers_json(types):
    entries = []
    for t in types:
        entry = {'id': t['id'], 'shape': t['shape']}
        if t['monsters']:
            entry['monsters'] = t['monsters']
        entry.update({'names': len(t['names']), 'descriptions': len(t['descriptions'])})
        entries.append(entry)
    return json.dumps({'challengers': entries}, indent=2, ensure_ascii=False) + '\n'


def render_row(key, text):
    buffer = io.StringIO()
    csv.writer(buffer, lineterminator='\n').writerow([key, text])
    return buffer.getvalue().rstrip('\n')


def new_locale(old, types):
    """the pack's locale with every imported type's rows replaced, every other row as it was, kept sorted by key"""
    newline = '\r\n' if '\r\n' in old else '\n'
    lines = old.split(newline) if old else ['keys,en']
    header, body = lines[0], [l for l in lines[1:] if l]
    ours = {t['id'] for t in types}
    kept = [l for l in body if not (l.startswith('challenger.') and l.split('.')[1] in ours
                                    and l.split(',', 1)[0].split('.')[2] in ('name', 'description'))]
    rows = []
    for t in types:
        for what, key in (('names', 'name'), ('descriptions', 'description')):
            for index, text in enumerate(t[what], 1):
                rows.append(render_row(f"challenger.{t['id']}.{key}.{index:03d}", text))
    every = sorted(kept + rows, key=lambda l: l.split(',', 1)[0])
    return newline.join([header] + every) + newline


def said(t):
    """one type, as the report says it"""
    counts = f"{len(t['names'])} names, {len(t['descriptions'])} descriptions"
    if not t['monsters']:
        return f"{t['id']}: shared by every {t['shape']}, {counts}"
    if t['shape'] == 'single':
        return f"{t['id']}: single, {' or '.join(m['monster'] for m in t['monsters'])}, {counts}"
    if t['shape'] == 'team':
        return f"{t['id']}: team of {', '.join(m['monster'] for m in t['monsters'])}, {counts}"
    return f"{t['id']}: group of {', '.join(m['count'] + ' ' + m['monster'] for m in t['monsters'])}, {counts}"


def pack_id(pack):
    with io.open(os.path.join(pack, 'pack.json'), encoding='utf-8') as f:
        return json.load(f)['id']


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('pack', help='the campaign folder to write into')
    parser.add_argument('--vault', default=VAULT)
    parser.add_argument('--check', action='store_true', help='write nothing; exit 1 if the pack is behind the vault')
    args = parser.parse_args()

    pack = os.path.abspath(args.pack)
    if not os.path.exists(os.path.join(pack, 'pack.json')):
        print(f'{pack} has no pack.json - give the campaign folder')
        return 1
    vault = os.path.abspath(args.vault)
    if not os.path.isdir(vault):
        print(f'no challenger lists at {vault} - nothing imported')
        return 0

    locale = os.path.join(pack, 'locale', pack_id(pack) + '.csv')
    names = monster_names(locale)

    files, every, problems = {}, [], []
    for name in sorted(os.listdir(vault)):
        if not name.endswith('.md'):
            continue
        types = read_list(os.path.join(vault, name), names, problems)
        files[snake(name[:-3]) + '.json'] = challengers_json(types)
        every.extend(types)

    for t in every:
        print(said(t))

    if problems:
        for problem in problems:
            print(f'REFUSED {problem}')
        print(f'FAILED - {len(problems)} problem(s), nothing written')
        return 1

    seen = [t['id'] for t in every]
    twice = sorted({i for i in seen if seen.count(i) > 1})
    if twice:
        print(f"FAILED - a type id is in two files: {', '.join(twice)}")
        return 1

    old = io.open(locale, encoding='utf-8', newline='').read() if os.path.exists(locale) else ''
    wanted = {os.path.join(pack, 'challengers', f): text for f, text in files.items()}
    wanted[locale] = new_locale(old, every)

    behind = [p for p, text in wanted.items()
              if not os.path.exists(p) or io.open(p, encoding='utf-8', newline='').read() != text]

    if args.check:
        for p in behind:
            print(f'behind the vault: {os.path.relpath(p, pack)}')
        return 1 if behind else 0

    for p in behind:
        os.makedirs(os.path.dirname(p), exist_ok=True)
        io.open(p, 'w', encoding='utf-8', newline='').write(wanted[p])
        print(f'wrote {os.path.relpath(p, pack)}')
    if not behind:
        print('the pack is up to date with the vault')
    return 0


if __name__ == '__main__':
    sys.exit(main())
