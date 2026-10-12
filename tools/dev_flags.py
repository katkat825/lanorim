"""The README's list of command-line flags, from the dev registry (cc_task_j-split-classes-dev-hooks.md Part 2).

    python tools/dev_flags.py            rewrite the README's list from game/Dev/DevHooks.cs
    python tools/dev_flags.py --check    the same, writing nothing; exit 1 if the README is behind the registry

The registry (DevHooks.Flags) is the one place the flags are written down: each is a line

    new("--show", "<screen>", Where.Launch, true, "opens a launch screen: ..."),

under a comment that names its group. The README's list sits between the two markers below and is replaced whole.
It also checks the registry against game/DevArgs.cs, the one reader of the command line: a flag DevArgs reads that the
registry doesn't list, or one listed that nothing reads, is a failure either way, with or without --check.
"""

import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REGISTRY = os.path.join(ROOT, 'game', 'Dev', 'DevHooks.cs')
READER = os.path.join(ROOT, 'game', 'DevArgs.cs')
README = os.path.join(ROOT, 'README.md')

BEGIN = '<!-- dev flags: written by `python tools/dev_flags.py` from game/Dev/DevHooks.cs - edit the registry, not this -->'
END = '<!-- end of dev flags -->'

WHERE = {
    'Launch': 'launch.tscn',
    'Table': 'table.tscn on its own',
    'Play': 'at the table, in a campaign',
    'Builder': 'in the map builder',
    'Any': 'wherever it applies',
}

ENTRY = re.compile(r'^\s*new\("(--[a-z0-9-]+)",\s*"([^"]*)",\s*Where\.(\w+),\s*(true|false),\s*"((?:[^"\\]|\\.)*)"\),\s*$')


def read(path):
    with open(path, encoding='utf-8-sig') as f:
        return f.read()


def registry():
    """[(group, [(flag, takes, where, release, does)])] in the registry's order"""
    text = read(REGISTRY)
    start = text.index('Flags = new DevFlag[]')
    end = text.index('};', start)
    groups = []

    for line in text[start:end].splitlines()[1:]:
        stripped = line.strip()

        if stripped.startswith('//'):
            groups.append((stripped[2:].strip(), []))
            continue

        m = ENTRY.match(line)

        if m:
            if not groups: groups.append(('', []))
            groups[-1][1].append((m.group(1), m.group(2), m.group(3), m.group(4) == 'true', m.group(5)))
        elif stripped not in ('', '{'):
            sys.exit(f'dev_flags: a line in DevHooks.Flags this script can\'t read (one entry a line, please):\n  {stripped}')

    return groups


def read_by_devargs():
    return set(re.findall(r'\(\s*"(--[a-z0-9-]+)"\s*\)', read(READER)))


def section(groups):
    lines = [BEGIN, '']

    for group, flags in groups:
        if not flags: continue

        lines.append(f'*{group[0].upper() + group[1:]}*' if group else '')
        lines.append('')
        lines.append('| flag | where | in a release | what it does |')
        lines.append('|---|---|---|---|')

        for flag, takes, where, release, does in flags:
            name = f'`{flag} {takes}`' if takes else f'`{flag}`'
            lines.append(f'| {name} | {WHERE.get(where, where)} | {"yes" if release else ""} | {does} |')

        lines.append('')

    lines.append(END)
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true', help='write nothing; exit 1 if the README is behind')
    check = parser.parse_args().check

    groups = registry()
    listed = [f for _, flags in groups for f in flags]
    names = [f[0] for f in listed]
    failed = False

    twice = sorted({n for n in names if names.count(n) > 1})
    if twice:
        print('listed twice in DevHooks.Flags: ' + ', '.join(twice))
        failed = True

    read_by = read_by_devargs()
    unlisted = sorted(read_by - set(names))
    unread = sorted(set(names) - read_by)

    if unlisted:
        print('read by DevArgs but not in DevHooks.Flags: ' + ', '.join(unlisted))
        failed = True

    if unread:
        print('in DevHooks.Flags but nothing in DevArgs reads it: ' + ', '.join(unread))
        failed = True

    readme = read(README)

    if BEGIN not in readme or END not in readme:
        print(f'README.md has no list to write into: put the two markers where it goes\n  {BEGIN}\n  {END}')
        return 1

    before = readme[:readme.index(BEGIN)]
    after = readme[readme.index(END) + len(END):]
    written = before + section(groups) + after

    print(f'{len(listed)} flags in {len([g for g in groups if g[1]])} groups; DevArgs reads {len(read_by)}')

    if written != readme:
        if check:
            print('README.md\'s list of flags is behind the registry: run python tools/dev_flags.py')
            failed = True
        else:
            with open(README, 'w', encoding='utf-8', newline='\n') as f:
                f.write(written)
            print('README.md\'s list of flags rewritten')

    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
