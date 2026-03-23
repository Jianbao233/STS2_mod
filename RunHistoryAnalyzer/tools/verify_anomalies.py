# -*- coding: utf-8 -*-
import json
from pathlib import Path
from collections import defaultdict

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

def get_mpt(node):
    mpt = node.get("map_point_type", "")
    if mpt and mpt.lower() not in ("unknown", "none", ""):
        return mpt.lower()
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").strip().lower()
        if rt and rt not in ("none", ""):
            return rt
    return "unknown"

# 模拟 RelicMultiPickRule.MaxLegitRelicPicks
ceilings = {"monster": 1, "elite": 2, "treasure": 1, "ancient": 5, "event": 4, "rest": 1, "boss": 1, "shop": 999}

# 模拟 NonShopLargeGoldGainRule 跳过规则
SKIP_TYPES = {"shop", "ancient"}

# 扫描所有存档，列出所有异常
anomalies = []  # (type, file, act, mpt, gold, picks, rooms, players)

for rp in sorted(HISTORY_DIR.glob("*.run")):
    try:
        with rp.open(encoding="utf-8") as f:
            d = json.load(f)
    except:
        continue

    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            mpt = get_mpt(node)

            for ps in node.get("player_stats", []):
                gold = ps.get("gold_gained", 0)
                picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))

                # NonShopLargeGoldRule: 非商店节点 gold >= 250 且非 ancient 跳过
                if mpt not in SKIP_TYPES and gold >= 250:
                    rooms = [(r.get("room_type"), r.get("model_id")) for r in node.get("rooms", [])]
                    anomalies.append(("NonShopLargeGold", rp.stem, ai, mpt, gold, picks, rooms))

                # RelicMultiPickRule: 非商店节点 picks > ceiling
                ceiling = ceilings.get(mpt, 2)
                if picks > ceiling:
                    rooms = [(r.get("room_type"), r.get("model_id")) for r in node.get("rooms", [])]
                    anomalies.append(("RelicMultiPick", rp.stem, ai, mpt, gold, picks, rooms))

print("=" * 70)
print(f"检测到的异常总数: {len(anomalies)}")
print()

from itertools import groupby
anomalies.sort(key=lambda x: x[0])
for atype, group in groupby(anomalies, key=lambda x: x[0]):
    items = list(group)
    print(f"【{atype}】 共 {len(items)} 条")
    for item in items[:10]:
        print(f"  [{item[1]}] act={item[2]} mpt={item[3]:10s} gold={item[4]:4d} picks={item[5]} rooms={item[6][:2]}")
    if len(items) > 10:
        print(f"  ... 还有 {len(items)-10} 条")
    print()
