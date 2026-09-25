"""Set English rows in game/locale/game.csv, keeping the file sorted by key.

Usage: python tools/locale_set.py rows.csv   (rows.csv: key,en lines, CSV-quoted as usual)
   or: import and call set_rows({key: text})

A development helper for bulk edits (the spell renames of 2026-09-24). The game never runs it.
"""
import csv
import io
import sys

PATH = 'game/locale/game.csv'


def read():
    with open(PATH, encoding='utf-8', newline='') as f:
        rows = list(csv.reader(f))
    header, body = rows[0], rows[1:]
    return header, {r[0]: r[1:] for r in body if r}


def write(header, table):
    out = io.StringIO()
    w = csv.writer(out, lineterminator='\n')
    w.writerow(header)
    for key in sorted(table):
        w.writerow([key] + table[key])
    with open(PATH, 'w', encoding='utf-8', newline='') as f:
        f.write(out.getvalue())


def set_rows(rows, remove=()):
    header, table = read()
    for key, text in rows.items():
        cells = table.get(key, [''] * (len(header) - 1))
        cells[0] = text
        table[key] = cells
    for key in remove:
        table.pop(key, None)
    write(header, table)


if __name__ == '__main__':
    with open(sys.argv[1], encoding='utf-8', newline='') as f:
        set_rows({r[0]: r[1] for r in csv.reader(f) if r})
