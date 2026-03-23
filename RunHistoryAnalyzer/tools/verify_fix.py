# -*- coding: utf-8 -*-
"""验证修复后的 C# 规则行为（模拟 AncientRuleLoader + RelicMultiPickRule）"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

# === 模拟 AncientRuleLoader ===
def should_skip_non_shop_gold(mpt):
    return mpt == "ancient"

# === 精确模拟 AncientRuleLoader.MaxLegitRelicPicks(mapPointType, modelId) ===
json_path = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\RunHistoryAnalyzer\Data\ancient_peoples_rules.json")

ceilings_map = {}  # (mpt, model_id) -> ceiling

if json_path.exists():
    with json_path.open(encoding="utf-8") as f:
        data = json.load(f)
    for ov in data.get("node_type_overrides", []):
        mpt = (ov.get("match", {}).get("map_point_type") or "").lower()
        mid = ov.get("match", {}).get("model_id")
        ceiling = ov.get("relic_pick_ceiling", -1)
        if ceiling >= 0:
            if mid:
                ceilings_map[(mpt, mid.upper())] = ceiling
            else:
                ceilings_map[(mpt, None)] = ceiling
    print(f"[AncientRuleLoader] 从 JSON 加载了 {len(ceilings_map)} 条上限规则")
    for k, v in sorted(ceilings_map.items(), key=lambda x: str(x[0])):
        print(f"  mpt={k[0]!r:12s} model_id={k[1]!r:12s} -> ceiling={v}")
else:
    print(f"[AncientRuleLoader] JSON 不存在，使用默认规则")

# 默认值（与 C# DefaultRelicPickCeiling 一致）
default_ceilings = {
    "monster": 1, "elite": 2, "treasure": 1, "ancient": 5,
    "event": 4, "rest": 1, "boss": 1, "shop": 999, "unknown": 2
}

def max_legit_relic_picks(mpt, model_id=None):
    upper_mpt = mpt.lower()
    upper_mid = model_id.upper() if model_id else None

    # 优先精确匹配 mpt + model_id
    if upper_mid and (upper_mpt, upper_mid) in ceilings_map:
        return ceilings_map[(upper_mpt, upper_mid)]

    # 回退：仅按 mpt
    if (upper_mpt, None) in ceilings_map:
        return ceilings_map[(upper_mpt, None)]

    return default_ceilings.get(upper_mpt, 2)

# === 模拟 MapNodeShopUtil ===
def is_shop_like(node):
    mpt = (node.get("map_point_type") or "").lower()
    if mpt == "shop": return True
    for r in node.get("rooms", []):
        if (r.get("room_type") or "").lower() == "shop": return True
    return False

def has_shop_transaction(node):
    for ps in node.get("player_stats", []):
        if ps.get("bought_relics") or ps.get("bought_colorless") or ps.get("bought_potions"):
            return True
    return False

def is_spoils_map_payout_node(stat, gold_gained):
    if gold_gained <= 0 or gold_gained > 1600: return False
    for q in stat.get("completed_quests", []):
        if q and "SPOILS_MAP" in q.upper(): return True
    for c in stat.get("cards_removed", []):
        if "SPOILS_MAP" in (c.get("id") or "").upper(): return True
    return False

def should_skip_non_shop_gold_node(node):
    if is_shop_like(node): return True
    mpt = (node.get("map_point_type") or "").lower()
    if should_skip_non_shop_gold(mpt): return True
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").lower()
        if should_skip_non_shop_gold(rt): return True
    if has_shop_transaction(node): return True
    return False

# === 主扫描 ===
print()
print("=" * 65)
print("修复后规则扫描结果")
print("=" * 65)

total_runs = 0
non_shop_large = []
relic_multi = []

for rp in sorted(HISTORY_DIR.glob("*.run")):
    try:
        with rp.open(encoding="utf-8") as f:
            d = json.load(f)
    except:
        continue

    total_runs += 1

    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            # NonShopLargeGold
            if not should_skip_non_shop_gold_node(node):
                for ps in node.get("player_stats", []):
                    g = ps.get("gold_gained", 0)
                    if g >= 250 and not is_spoils_map_payout_node(ps, g):
                        rooms = [(r.get("room_type"), r.get("model_id")) for r in node.get("rooms", [])]
                        non_shop_large.append((rp.stem, ai, node.get("map_point_type", ""), g, rooms))

            # RelicMultiPick
            if is_shop_like(node) or has_shop_transaction(node):
                continue
            mpt = (node.get("map_point_type") or "").lower()
            model_id = node.get("rooms", [{}])[0].get("model_id") if node.get("rooms") else None
            ceiling = max_legit_relic_picks(mpt, model_id)
            for ps in node.get("player_stats", []):
                picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                if picks > ceiling:
                    rooms = [(r.get("room_type"), r.get("model_id")) for r in node.get("rooms", [])]
                    relic_multi.append((rp.stem, ai, mpt, model_id, picks, ceiling, rooms))

print(f"\n扫描存档: {total_runs} 个")
print()
print(f"NonShopLargeGold 异常: {len(non_shop_large)} 条")
for x in non_shop_large:
    print(f"  [{x[0]}] act={x[1]} mpt={x[2]:12s} gold={x[3]:4d}  rooms={x[4]}")
print()
print(f"RelicMultiPick 异常: {len(relic_multi)} 条")
for x in relic_multi:
    print(f"  [{x[0]}] act={x[1]} mpt={x[2]:12s} model_id={str(x[3]):15s} picks={x[4]} ceiling={x[5]}  rooms={x[6]}")

if not non_shop_large and not relic_multi:
    print("\n✓ 所有已知合法场景已无异常告警")
