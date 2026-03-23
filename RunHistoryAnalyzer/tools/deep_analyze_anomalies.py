# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")
run_files = sorted(HISTORY_DIR.glob("*.run"))

def get_map_point_type(node):
    mpt = node.get("map_point_type", "")
    if mpt and mpt.lower() not in ("unknown", "none", ""):
        return mpt.lower()
    for room in node.get("rooms", []):
        rt = (room.get("room_type") or "").strip().lower()
        if rt and rt not in ("none", ""):
            return rt
    return "unknown"

# 1. monster 节点 gold_gained 异常详情
print("=== monster gold_gained 异常 (>100) ===")
for rp in run_files:
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)
    for act in d.get("map_point_history", []):
        for node in act:
            if get_map_point_type(node) != "monster":
                continue
            for ps in node.get("player_stats", []):
                gold = ps.get("gold_gained", 0)
                if gold > 100:
                    rooms = [r.get("room_type","") for r in node.get("rooms",[])]
                    picks = sum(1 for c in ps.get("relic_choices",[]) if c.get("was_picked",False))
                    # 找含 relicChoices 的遗物名
                    relics = []
                    for c in ps.get("relic_choices", []):
                        if c.get("was_picked"):
                            relics.append(c.get("relic", {}).get("id","?"))
                    print(f"  [{rp.name}] gold={gold:4d} rooms={rooms} picks={picks} relics={relics}")

# 2. 找 anomaly gold_gained=999 的 ancient 节点（验证合法）
print()
print("=== ancient gold_gained=999 节点详情 ===")
for rp in run_files:
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)
    for act_idx, act in enumerate(d.get("map_point_history", [])):
        for node_idx, node in enumerate(act):
            if get_map_point_type(node) != "ancient":
                continue
            for ps in node.get("player_stats", []):
                gold = ps.get("gold_gained", 0)
                if gold >= 333:
                    rooms = [r.get("room_type","") for r in node.get("rooms",[])]
                    picks = sum(1 for c in ps.get("relic_choices",[]) if c.get("was_picked",False))
                    relics = []
                    for c in ps.get("relic_choices", []):
                        if c.get("was_picked"):
                            relics.append(c.get("relic", {}).get("id","?"))
                    cards = [c.get("id","?") for c in ps.get("cards_gained",[])]
                    print(f"  [{rp.name}] act={act_idx} gold={gold} picks={picks} relics={relics} cards_gained={cards[:5]}")

# 3. ancient 节点多 relic_picks 详情
print()
print("=== ancient relic_picks > 1 详情 ===")
for rp in run_files:
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)
    for act_idx, act in enumerate(d.get("map_point_history", [])):
        for node in act:
            if get_map_point_type(node) != "ancient":
                continue
            for ps in node.get("player_stats", []):
                picks = sum(1 for c in ps.get("relic_choices",[]) if c.get("was_picked",False))
                if picks > 1:
                    rooms = [r.get("room_type","") for r in node.get("rooms",[])]
                    relics = []
                    for c in ps.get("relic_choices", []):
                        if c.get("was_picked"):
                            relics.append(c.get("relic", {}).get("id","?"))
                    print(f"  [{rp.name}] act={act_idx} picks={picks} rooms={rooms} relics={relics}")

# 4. event 节点 relic_picks > 1 详情
print()
print("=== event relic_picks > 1 详情 ===")
for rp in run_files:
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)
    for act_idx, act in enumerate(d.get("map_point_history", [])):
        for node in act:
            if get_map_point_type(node) != "event":
                continue
            for ps in node.get("player_stats", []):
                picks = sum(1 for c in ps.get("relic_choices",[]) if c.get("was_picked",False))
                if picks > 1:
                    rooms = [r.get("room_type","") for r in node.get("rooms",[])]
                    event_id = ""
                    for r in node.get("rooms",[]):
                        event_id = r.get("model_id", "")
                        if event_id: break
                    relics = []
                    for c in ps.get("relic_choices", []):
                        if c.get("was_picked"):
                            relics.append(c.get("relic", {}).get("id","?"))
                    print(f"  [{rp.name}] act={act_idx} picks={picks} event={event_id} relics={relics}")
