# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

rp = HISTORY_DIR / "1773490470.run"
with rp.open(encoding="utf-8") as f:
    d = json.load(f)

print(f"存档: 1773490470  |  win={d.get('win')}  |  build={d.get('build_id')}")
print()

floor = 0
for ai, act in enumerate(d.get("map_point_history", [])):
    for ni, node in enumerate(act):
        floor += 1
        mpt = node.get("map_point_type", "")
        rooms = node.get("rooms", [])
        room_ids = [(r.get("room_type"), r.get("model_id")) for r in rooms]

        # 只打印 boss / elite / boss+ancient 的节点
        is_boss = any("BOSS" in (mid or "") for _, mid in room_ids)
        is_neow = any("NEOW" in (mid or "") for _, mid in room_ids)
        if floor == 16 or is_boss or is_neow:
            print(f"floor={floor:3d} act={ai} node={ni}  mpt={mpt!r:12s}  rooms={room_ids}")
            for pi, ps in enumerate(node.get("player_stats", [])):
                picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                if picks > 0:
                    print(f"  stats[{pi}] gold={ps.get('gold_gained')} picks={picks} "
                          f"choices={[c.get('choice') for c in ps.get('relic_choices',[]) if c.get('was_picked')]}")
            print()
