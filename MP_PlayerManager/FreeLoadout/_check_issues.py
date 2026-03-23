# -*- coding: utf-8 -*-
import os, glob

src = 'K:/杀戮尖塔mod制作/STS2_mod/MP_PlayerManager/FreeLoadout/src'
bad = []
for fp in glob.glob(src + '/**/*.cs', recursive=True):
    with open(fp, 'r', encoding='utf-8') as f:
        c = f.read()
    for pattern in ['FreeLoadout.', 'res://FreeLoadout', '[Nullable', 'NullableAttribute', 'TupleElementNames']:
        if pattern in c:
            bad.append((os.path.relpath(fp, src), pattern))
for fp, pat in bad:
    print(f'{fp}: {repr(pat)}')
