# FIND DUPLICATE CODE: a rough clone finder for C#, no dependencies.
#   python tools/find_duplicate_code.py                 # core content game sim
#   python tools/find_duplicate_code.py core.tests content.tests
#   python tools/find_duplicate_code.py --check         # the guard: check-duplicates.ps1 runs this
# it lists: methods identical once local names are ignored; groups of methods >= 80% alike; and
# one-line (=>) members with the same body in two files. a hit is a lead, not a verdict: Undo/Redo,
# Row/Column, Buy/Sell look alike on purpose. slow on purpose too (difflib): a few seconds.
#
# --check (cc_task_dedupe-methods.md #16) reads the game (core content game sim) and the tests
# (core.tests content.tests) as two separate pools and FAILS on an exact duplicate or a shared =>
# body that ALLOWED below doesn't name. near-duplicate groups are listed, never failed. BEFORE YOU
# ADD a method, a helper or a one-liner, run it (BUILD_FOR_CLAUDE_CODE.md, agreement #9).
import re,glob,hashlib,collections,difflib,sys,os
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..'))

GAME=('core','content','game','sim')
TESTS=('core.tests','content.tests')

# LOOK-ALIKES, KEPT ON PURPOSE: each entry names the members ("FileStem.Method", the file's name
# without .cs; a constructor is its class's name) and says in one line why they stay two. a finding
# is allowed when every member of it is in one entry. an entry that no longer matches anything is
# said, so the list can't rot
ALLOWED=[
    ({'MapDraft.Undo.Undo','MapDraft.Undo.Redo'}, 'mirror images: a step from one stack to the other'),
    ({'Ui.Column','Ui.Row'}, 'a vertical box and a horizontal one'),
    ({'PackView.Buy','PackView.Sell'}, 'the two directions of a trade, each with its own refusals'),
    ({'CreationScreen.NextPage','CreationScreen.PreviousPage'}, 'forward and back'),
    ({'HintLadder.Next','HintLadder.Peek'}, 'one climbs a rung, the other only looks at it'),
    ({'Encounter.Actions.Afflict','Encounter.Actions.Relieve'}, 'putting a condition on and taking it off'),
    ({'Encounter.Actions.Reveal','Encounter.GrappleShove.Release'}, 'two different things ending, each told to the observers'),
    ({'MapDraft.Painting.Paint','MapDraft.Painting.Wall'}, 'the same undoable stroke over squares and over lines'),
    ({'SpellZone.Within','SpellZone.Inside'}, 'reach beside a zone against the squares it covers'),
    ({'Battlefield.Distance','Battlefield.CanSee'}, 'two questions about two creatures'),
    ({'CombatSession.Preview.AttackChance','CombatSession.Preview.SaveFailChance'}, 'the odds of an attack against the odds of a save'),
    ({'Incantation.Escape.BreakFree','Incantation.Escape.Study'}, 'an Athletics check to escape and an Investigation check to see through'),
    ({'ManifestReader.Fields.Format','ManifestReader.Fields.Engine'}, 'two version fields with different rules'),
    ({'Json.Strings','Json.Items'}, 'a list of strings against a list of objects'),
    ({'SpellReader.Records.ReadExtraDice','SpellReader.Records.ReadRaises'}, 'two upcast shapes'),
    ({'TableResolver.TableResolver','BarkBank.Speaking','SaveLibrary.SaveLibrary','Encounter.Encounter','TrayDice.TrayDice'},
     'constructors that share only their null guards'),
    ({'StandardResolver.StandardResolver','TableResolver.Digital'},
     'one-line null guards; the two share no base (cc_task_dedupe-methods.md #10)'),
    ({'Observers.Struck','ScreenLog.Rolled'}, "two logs' events, each written as its own ToString"),
    ({'BeatBook.Read','HintBook.Read'}, "each book's own list, example and fields, over ListFile.ReadFolder"),
    ({'EncounterReader.ReadOne','LootReader.ReadOne'}, "each table's own keys; the shared steps are TableReader's"),
    ({'BoardTiles.Panel','BoardTiles.Rubble'}, 'two models stood up by Standing, turned differently'),
    ({'Background.TryRead','ClassReader.TryRead','Form.TryRead','ItemReader.TryRead','MonsterReader.TryRead',
      'ConsequenceReader.TryRead','Species.TryRead','SpellReader.TryRead','MerchantReader.TryRead'},
     "each reader's ListReader entry point, handing its own file to its own EntryList (the shared steps are EntryList's)"),
]

def files_in(dirs):
    return [f for d in dirs for f in glob.glob(d+'/**/*.cs',recursive=True)
            if os.sep+'bin'+os.sep not in f and os.sep+'obj'+os.sep not in f and '/bin/' not in f and '/obj/' not in f]

sig=re.compile(r'^\s*(?:(?:public|private|protected|internal|static|override|virtual|sealed|async|readonly|new|partial|abstract)\s+)*([\w<>\[\],.? ()]+?)\s+([A-Z]\w*)\s*(<[^>]*>)?\s*\(([^)]*)\)\s*(?:where [^{]*)?$')
def strip(s):
    s=re.sub(r'//[^\n]*','',s); s=re.sub(r'/\*.*?\*/','',s,flags=re.S)
    s=re.sub(r'"(?:\\.|[^"\\])*"','"S"',s); return s

def stem(f): return os.path.basename(f)[:-3]

def read_methods(files):
    methods=[]
    for f in files:
        lines=open(f,encoding='utf-8-sig').read().split('\n')
        i=0
        while i<len(lines):
            m=sig.match(lines[i])
            if m and not re.match(r'\s*(if|for|foreach|while|switch|return|using|catch|lock|else)\b',lines[i]) and i+1<len(lines) and lines[i+1].strip()=='{':
                depth=0;j=i+1;body=[]
                while j<len(lines):
                    depth+=lines[j].count('{')-lines[j].count('}'); body.append(lines[j])
                    if depth==0: break
                    j+=1
                b=strip('\n'.join(body))
                toks=re.findall(r'[A-Za-z_]\w*|\d+|\S',b)
                if len(toks)>=30:
                    norm=[('ID' if re.match(r'[a-z_]\w*$',t) else t) for t in toks]  # locals normalized
                    methods.append((f,i+1,m.group(2),len(body),toks,norm))
                i=j
            i+=1
    return methods

def exact(methods):
    ex=collections.defaultdict(list)
    for m in methods: ex[hashlib.md5(' '.join(m[5]).encode()).hexdigest()].append(m)
    return [[(m[0],m[1],m[2],m[3]) for m in v] for v in ex.values() if len(v)>1]

def near(methods):
    parent={}
    def find(x):
        parent.setdefault(x,x)
        while parent[x]!=x: parent[x]=parent[parent[x]]; x=parent[x]
        return x
    ms=sorted(methods,key=lambda m:len(m[5]))
    for a in range(len(ms)):
        for b in range(a+1,len(ms)):
            A,B=ms[a],ms[b]
            if len(B[5])>len(A[5])*1.35: break
            sm=difflib.SequenceMatcher(None,A[5],B[5],autojunk=False)
            if sm.real_quick_ratio()<0.8 or sm.quick_ratio()<0.8: continue
            if sm.ratio()>=0.8:
                ka=(A[0],A[1],A[2],A[3]);kb=(B[0],B[1],B[2],B[3])
                parent[find(ka)]=find(kb)
    groups=collections.defaultdict(list)
    for k in list(parent): groups[find(k)].append(k)
    return sorted((sorted(g) for g in groups.values()),key=len,reverse=True)

def one_liners(files):
    body=collections.defaultdict(list)
    for f in files:
        s=open(f,encoding='utf-8-sig').read()
        s=re.sub(r'//[^\n]*','',s)
        for m in re.finditer(r'(?:static|private|public|internal)[^;{=\n(]*?\s([A-Z]\w*)\s*\(([^)]*)\)\s*=>\s*([^;]{25,}?);',s,re.S):
            b=re.sub(r'\s+',' ',m.group(3)).strip()
            params=[p.strip().split()[-1] for p in m.group(2).split(',') if p.strip()]
            for i,p in enumerate(params): b=re.sub(r'\b'+re.escape(p)+r'\b',f'P{i}',b)
            body[b].append((f,s[:m.start()].count(chr(10))+1,m.group(1)))
    return [l for l in body.values() if len(set(x[0] for x in l))>=2]

def names(group): return {f'{stem(m[0])}.{m[2]}' for m in group}

def allowed(group, used):
    for i,(members,why) in enumerate(ALLOWED):
        if names(group)<=members:
            used.add(i); return why
    return None

def show(group): return ' | '.join(f'{m[0]}:{m[1]} {m[2]}' for m in group)

def report(dirs, used=None):
    files=files_in(dirs)
    methods=read_methods(files)
    print(len(methods),'methods of 30+ tokens in',' '.join(dirs))
    failing=0
    for title,groups,fails in (('exact duplicates (after renaming locals)',exact(methods),True),
                               ('near-duplicate groups (>=0.80), listed but never failed',near(methods),False),
                               ('one-line (=>) members with the same body in two or more files',one_liners(files),True)):
        print('\n== '+title)
        for g in groups:
            why=allowed(g,used) if used is not None else None
            if why: print(f'   kept: {show(g)}\n         - {why}')
            elif fails and used is not None: print(f'FAIL [{len(g)}] {show(g)}'); failing+=1
            else: print(f'[{len(g)}] {show(g)}')
    return failing

if __name__=='__main__':
    args=[a for a in sys.argv[1:] if a!='--check']
    if '--check' not in sys.argv:
        report(tuple(args) or GAME)
        sys.exit(0)
    used=set(); failing=0
    for pool in (GAME,TESTS):
        failing+=report(pool,used); print()
    stale=[' / '.join(sorted(m)) for i,(m,_) in enumerate(ALLOWED) if i not in used]
    for s in stale: print('note: nothing matches the allow-list entry',s,'- it can go')
    if failing:
        print(f'\n{failing} duplicate(s) - merge them, or add the pair to ALLOWED with the reason they stay two')
        sys.exit(1)
    print('\nno duplicates outside the allow-list')
