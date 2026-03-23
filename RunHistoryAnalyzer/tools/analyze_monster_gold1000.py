# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

rp = HISTORY_DIR / "1773771912.run"
with rp.open(encoding="utf-8") as f:
    d = json.load(f)

print("存档 players:", [(p.get('id'), p.get('character')) for p in d.get('players', [])])
print("win:", d.get('win'), "ascension:", d.get('ascension'))
print("build_id:", d.get('build_id'))
print()

for ai, act in enumerate(d.get("map_point_history", [])):
    for ni, node in enumerate(act):
        if node.get("map_point_type", "").lower() != "monster":
            continue
        rooms = node.get("rooms", [])
        room_ids = [(r.get("room_type"), r.get("model_id")) for r in rooms]
        for ps in node.get("player_stats", []):
            gold = ps.get("gold_gained", 0)
            if gold > 50:
                picks = [c.get("choice", "") for c in ps.get("relic_choices", []) if c.get("was_picked")]
                final_relics = [r.get("id", "") for r in ps.get("relics", [])]
                print(f"  act={ai} node={ni} rooms={room_ids}")
                print(f"    gold_gained={gold}  current_gold={ps.get('current_gold')}")
                print(f"    relic_choices was_picked: {picks}")
                print(f"    final_relics: {final_relics}")
                print()
