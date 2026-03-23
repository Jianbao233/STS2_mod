# -*- coding: utf-8 -*-
"""专项验证：ancient 节点 picks=10 和 boss picks=2 的存档"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

cases = {
    "1773723718": {"act": 0, "node": 0},   # RelicMultiPick: monster picks=10? C#说是ancient
    "1773490470": {"act": 0, "node": 0},   # RelicMultiPick: boss picks=2
    "1773836144": {"act": 2, "node": None}, # RelicMultiPick: unknown picks=3
    "1773771912": {"act": 0, "node": 1},   # NonShopLargeGold: monster gold=1000
}

for fname, info in cases.items():
    rp = HISTORY_DIR / (fname + ".run")
    if not rp.exists():
        print(f"[{fname}] NOT FOUND")
        continue
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)

    print(f"\n{'='*65}")
    print(f"[{fname}] win={d.get('win')} build={d.get('build_id')}")

    act_idx = info["act"]
    target_node_idx = info["node"]
    acts = d.get("map_point_history", [])

    if target_node_idx is None:
        # 找 unknown mpt 且 picks > 2 的节点
        floor = 0
        for ai, act in enumerate(acts):
            for ni, node in enumerate(act):
                mpt = node.get("map_point_type", "")
                if not mpt or mpt.lower() == "none":
                    mpt_display = "unknown"
                else:
                    mpt_display = mpt
                picks = sum(1 for ps in node.get("player_stats", [])
                           for c in ps.get("relic_choices", []) if c.get("was_picked"))
                if picks > 2:
                    print(f"\n  >> floor={floor+1} act={ai} node={ni} mpt={mpt_display} picks={picks}")
                    for pi, ps in enumerate(node.get("player_stats", [])):
                        pid = ps.get("player_id", "?")
                        picks_all = [c for c in ps.get("relic_choices", []) if c.get("was_picked")]
                        print(f"     stats[{pi}] pid={pid} gold={ps.get('gold_gained')} "
                              f"picks={[c.get('choice') for c in picks_all]}")
                        if ps.get("bought_relics") or ps.get("bought_colorless"):
                            print(f"     stats[{pi}] HAS_SHOP_TRANSACTION")
                    print(f"     rooms: {[(r.get('room_type'), r.get('model_id')) for r in node.get('rooms',[])]}")
                floor += 1
    else:
        floor = 0
        for ai, act in enumerate(acts):
            for ni, node in enumerate(act):
                if ai == act_idx and ni == target_node_idx:
                    mpt = node.get("map_point_type", "")
                    if not mpt or mpt.lower() == "none":
                        mpt_display = "unknown"
                    else:
                        mpt_display = mpt
                    print(f"\n  >> floor={floor+1} act={ai} node={ni} mpt={mpt_display}")
                    print(f"     rooms: {[(r.get('room_type'), r.get('model_id')) for r in node.get('rooms',[])]}")
                    for pi, ps in enumerate(node.get("player_stats", [])):
                        pid = ps.get("player_id", "?")
                        picks = sum(1 for c in ps.get("relic_choices",[]) if c.get("was_picked"))
                        picks_all = [c.get("choice") for c in ps.get("relic_choices",[]) if c.get("was_picked")]
                        print(f"     stats[{pi}] pid={pid} gold={ps.get('gold_gained')} "
                              f"gold_spent={ps.get('gold_spent')} current_gold={ps.get('current_gold')} "
                              f"picks={picks} was_picked={[c.get('choice') for c in ps.get('relic_choices',[]) if c.get('was_picked')]}")
                        if ps.get("bought_relics") or ps.get("bought_colorless"):
                            print(f"     stats[{pi}] HAS_SHOP_TRANSACTION")
                        cr = [c.get("id") for c in ps.get("cards_removed",[])]
                        if cr:
                            print(f"     stats[{pi}] cards_removed: {cr}")
                        cg = [c.get("id") for c in ps.get("cards_gained",[])]
                        if cg:
                            print(f"     stats[{pi}] cards_gained: {cg}")
                floor += 1
