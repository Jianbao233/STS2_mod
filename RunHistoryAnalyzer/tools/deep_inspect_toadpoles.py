# -*- coding: utf-8 -*-
import json
from pathlib import Path
HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")
rp = HISTORY_DIR / "1773723718.run"
with rp.open(encoding="utf-8") as f:
    d = json.load(f)

print("players:")
for p in d.get("players", []):
    print(f"  id={p.get('id')}  char={p.get('character')}  relics={len(p.get('relics',[]))}")

print()
print("=== node[0][0] (NEoW) ===")
n0 = d["map_point_history"][0][0]
print("mpt:", repr(n0.get("map_point_type")))
print("rooms:", [(r.get("room_type"), r.get("model_id")) for r in n0.get("rooms",[])])
for pi, ps in enumerate(n0.get("player_stats",[])):
    picks = [c.get("choice") for c in ps.get("relic_choices",[]) if c.get("was_picked")]
    gold = ps.get("gold_gained")
    print(f"  stats[{pi}] gold={gold} picks={len(picks)}: {picks}")

print()
print("=== node[0][2] (TOADPOLES, mpt=monster) ===")
n2 = d["map_point_history"][0][2]
print("mpt:", repr(n2.get("map_point_type")))
print("rooms:", [(r.get("room_type"), r.get("model_id")) for r in n2.get("rooms",[])])
for pi, ps in enumerate(n2.get("player_stats",[])):
    picks = [c.get("choice") for c in ps.get("relic_choices",[]) if c.get("was_picked")]
    gold = ps.get("gold_gained")
    hp = ps.get("current_hp")
    dmg = ps.get("damage_taken")
    cg = [c.get("id") for c in ps.get("cards_gained",[])]
    cr = [c.get("id") for c in ps.get("cards_removed",[])]
    print(f"  stats[{pi}] gold={gold} hp={hp} dmg_taken={dmg} picks={len(picks)}")
    if cg: print(f"    cards_gained: {cg}")
    if cr: print(f"    cards_removed: {cr}")
    if picks: print(f"    relic_picks: {picks}")
