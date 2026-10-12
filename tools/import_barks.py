"""Import the companions' bark pools from Kathleen's vault into the game (cc_task_i Part 2).

    python tools/import_barks.py            read the vault, write the banks and the English, report
    python tools/import_barks.py --check    the same, writing nothing; exit 1 if the game is behind the vault
    python tools/import_barks.py --vault "D:/somewhere/Bark Pools"

The vault (../campaign_authoring/Bark Pools by default) is READ, never written: one folder per pool, one file per
companion, `<Pool>/<companion>.md`:

    dragon                       line 1, the body
    class(es): fighter, barbarian
    voice: warm, blunt, loyal
                                 (blank)
    1. Now THAT was a fight!     one bark per numbered line

It writes, for the whole game:
  - content/srd/barks/<companion>.json, one bank per companion in the shape BarkReader reads
    ({"speaker": ..., "banks": {"victory": 30, ...}});
  - the lines into game/locale/game.csv as dialogue.<companion>.bark.<pool>.<NNN>, every other row byte for byte.

**bold** and *italics* become [b]...[/b] and [i]...[/i]: the companion's bubble is a RichTextLabel with BBCode on
(the log and the read-aloud strip them, BarkText.Plain). CAPS stay as written.

A pool folder the game doesn't know yet is a warning, not a failure; so is a file it can't place.
"""

import argparse
import csv
import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VAULT = os.path.join(os.path.dirname(ROOT), 'campaign_authoring', 'Bark Pools')
BANKS = os.path.join(ROOT, 'content', 'srd', 'barks')
LOCALE = os.path.join(ROOT, 'game', 'locale', 'game.csv')
POOLS_CS = os.path.join(ROOT, 'content', 'Dialogue', 'BarkPools.cs')
CLASSES = os.path.join(ROOT, 'content', 'srd', 'classes')
DIALOG_POOLS = os.path.join(ROOT, '_design_docs', 'DIALOG_POOLS.md')

# her file names are the companion ids, but for this one (don't rename her file)
FILE_NAMES = {'borrowed_shape': 'borrowed_shape_spirit'}

# DIALOG_POOLS.md names the danger pool by her Bramble notes' word
DOC_NAMES = {'trouble_ahead': 'danger'}

NUMBERED = re.compile(r'^\s*(\d+)\.\s*(.*?)\s*$')
HEADER = re.compile(r'^\s*(class(\(es\))?|voice)\s*:', re.IGNORECASE)


def snake(name):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', name.strip()).replace(' ', '_').lower()


def known_pools():
    """the pools the engine speaks in v1, from BarkPools.Order, so the list lives in one place"""
    text = io.open(POOLS_CS, encoding='utf-8').read()
    block = re.search(r'Order\s*=\s*\{(.*?)\}', text, re.S).group(1)
    return [snake(n) for n in re.findall(r'Bark\.(\w+)', block)]


def companions():
    found = set()
    for name in sorted(os.listdir(CLASSES)):
        if name.endswith('.json'):
            found.update(re.findall(r'"companion"\s*:\s*"(\w+)"', io.open(os.path.join(CLASSES, name), encoding='utf-8').read()))
    return found


def minimums():
    """'minimum to ship' per pool, from DIALOG_POOLS.md's tables"""
    mins = {}
    column = None
    if not os.path.exists(DIALOG_POOLS):
        return mins
    for row in io.open(DIALOG_POOLS, encoding='utf-8'):
        cells = [c.strip() for c in row.strip().strip('~').strip('|').split('|')]
        if not row.startswith('|') or len(cells) < 3:
            column = None if not row.startswith('|') else column
            continue
        if any(c.lower() == 'minimum' or c.lower().startswith('minimum to ship') for c in cells):
            column = next(i for i, c in enumerate(cells) if c.lower().startswith('minimum'))
            continue
        name = re.match(r'`(\w+)`', cells[0])
        if column is not None and name and column < len(cells):
            number = re.match(r'(\d+)', cells[column])
            if number:
                pool = snake(name.group(1))
                mins.setdefault(DOC_NAMES.get(pool, pool), int(number.group(1)))
    return mins


def emphasis(line):
    line = re.sub(r'\*\*(.+?)\*\*', r'[b]\1[/b]', line)
    line = re.sub(r'(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', r'[i]\1[/i]', line)
    return line


def read_pool_file(path, report):
    lines = []
    text = io.open(path, encoding='utf-8-sig').read().replace('\u00a0', ' ')
    for number, raw in enumerate(text.splitlines(), 1):
        said = NUMBERED.match(raw)
        if said:
            words = said.group(2)
            if not words:
                report.append(f'  skipped {path_of(path)}:{number}: a numbered line with no words')
                continue
            lines.append(emphasis(words))
        elif raw.strip() and number > 1 and not HEADER.match(raw):
            report.append(f'  skipped {path_of(path)}:{number}: not a numbered line - "{raw.strip()[:60]}"')
    return lines


def path_of(path):
    return os.path.relpath(path, os.path.dirname(VAULT_IN_USE)).replace('\\', '/')


def gather(vault, report, warnings):
    pools = known_pools()
    ids = companions()
    banks = {}   # companion -> pool -> [lines]

    for folder in sorted(os.listdir(vault)):
        where = os.path.join(vault, folder)
        if not os.path.isdir(where):
            continue
        pool = snake(folder)
        if pool not in pools:
            warnings.append(f'  the pool folder "{folder}" is one the game does not know yet - left out '
                            f'(the game speaks {", ".join(pools)})')
            continue
        for name in sorted(os.listdir(where)):
            if not name.endswith('.md'):
                continue
            stem = name[:-3]
            companion = FILE_NAMES.get(stem, stem)
            if companion not in ids:
                warnings.append(f'  could not place {folder}/{name}: "{companion}" is not a companion '
                                f'({", ".join(sorted(ids))})')
                continue
            lines = read_pool_file(os.path.join(where, name), report)
            if lines:
                banks.setdefault(companion, {})[pool] = lines
            else:
                warnings.append(f'  {folder}/{name} has no barks in it')
    return pools, banks


def bank_json(companion, pools, lines):
    counts = {pool: len(lines[pool]) for pool in pools if pool in lines}
    return json.dumps({'speaker': companion, 'banks': counts}, indent=2) + '\n'


def render_row(key, text):
    buffer = io.StringIO()
    csv.writer(buffer, lineterminator='\n').writerow([key, text])
    return buffer.getvalue().rstrip('\n')


def is_game_bark(key, companions_written):
    parts = key.split('.')
    return len(parts) == 5 and parts[0] == 'dialogue' and parts[2] == 'bark' and parts[1] in companions_written


def new_locale(old, banks):
    lines = old.split('\n')
    header, body = lines[0], [l for l in lines[1:] if l]
    kept = [l for l in body if not is_game_bark(l.split(',', 1)[0], banks)]
    rows = []
    for companion, pools in banks.items():
        for pool, said in pools.items():
            for index, text in enumerate(said, 1):
                rows.append(render_row(f'dialogue.{companion}.bark.{pool}.{index:03d}', text))
    every = sorted(kept + rows, key=lambda l: l.split(',', 1)[0])
    return header + '\n' + '\n'.join(every) + '\n'


VAULT_IN_USE = VAULT


def main():
    global VAULT_IN_USE
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--vault', default=VAULT)
    parser.add_argument('--check', action='store_true', help='write nothing; exit 1 if the game is behind the vault')
    parser.add_argument('--banks', default=BANKS, help='where the bank files go (a test points it elsewhere)')
    parser.add_argument('--locale', default=LOCALE, help='the locale csv to write the lines into')
    args = parser.parse_args()
    VAULT_IN_USE = os.path.abspath(args.vault)
    banks_dir, locale_csv = args.banks, args.locale

    if not os.path.isdir(VAULT_IN_USE):
        print(f'no bark pools at {VAULT_IN_USE} - nothing imported')
        return 0

    report, warnings = [], []
    pools, banks = gather(VAULT_IN_USE, report, warnings)
    mins = minimums()

    print(f'bark pools in {VAULT_IN_USE}:')
    total = 0
    for companion in sorted(banks):
        sizes = []
        for pool in pools:
            count = len(banks[companion].get(pool, []))
            total += count
            short = mins.get(pool)
            mark = '' if short is None or count >= short else f' (under {short})'
            sizes.append(f'{pool} {count}{mark}')
        print(f'  {companion}: {", ".join(sizes)}')
    print(f'  {total} lines for {len(banks)} companions; minimums to ship from DIALOG_POOLS.md: '
          + ', '.join(f'{p} {mins[p]}' for p in pools if p in mins))
    for line in warnings:
        print('warning' + line[1:] if line.startswith('  ') else line)
    for line in report:
        print(line)

    stale = []
    files = {os.path.join(banks_dir, f'{c}.json'): bank_json(c, pools, banks[c]) for c in sorted(banks)}
    for path, text in files.items():
        if not os.path.exists(path) or io.open(path, encoding='utf-8', newline='').read() != text:
            stale.append(os.path.relpath(path, ROOT))
    old = io.open(locale_csv, encoding='utf-8', newline='').read()
    locale = new_locale(old, banks)
    if locale != old:
        stale.append(os.path.relpath(locale_csv, ROOT))

    if args.check:
        if stale:
            print('the game is behind the vault: ' + ', '.join(stale).replace('\\', '/') +
                  ' - run python tools/import_barks.py')
            return 1
        print('the game has every bark the vault has')
        return 0

    os.makedirs(banks_dir, exist_ok=True)
    for path, text in files.items():
        io.open(path, 'w', encoding='utf-8', newline='').write(text)
    if locale != old:
        io.open(locale_csv, 'w', encoding='utf-8', newline='').write(locale)
    print('wrote ' + (', '.join(stale).replace('\\', '/') if stale else 'nothing: already up to date'))
    if locale != old and os.path.samefile(locale_csv, LOCALE):
        print('re-import the locale in Godot: godot --headless --path game --import')
    return 0


if __name__ == '__main__':
    sys.exit(main())
