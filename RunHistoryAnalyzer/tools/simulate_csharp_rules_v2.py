# -*- coding: utf-8 -*-
"""重新对齐 C# 逻辑的模拟脚本"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

# === 加载 JSON 规则 ===
json_path = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\RunHistoryAnalyzer\Data\ancient_peoples_rules.json")
ceilings_map = {}   # (mpt_lower, model_id_upper) -> ceiling
ceilings_mpt = {}  # mpt_lower -> ceiling

if json_path.exists():
    with json_path.open(encoding="utf-8") as f:
        data = json.load(f)
    for ov in data.get("node_type_overrides", []):
        mpt = (ov.get("match", {}).get("map_point_type") or "").lower()
        mid = ov.get("match", {}).get("model_id")
        ceiling = ov.get("relic_pick_ceiling", -1)
        if ceiling < 0:
            continue
        if mid:
            ceilings_map[(mpt, mid.upper())] = ceiling
        else:
            ceilings_mpt[mpt] = ceiling
else:
    print("[!] JSON 不存在")

DEFAULT_CEILINGS = {"monster":1,"elite":2,"treasure":1,"ancient":5,
                     "event":4,"rest":1,"boss":1,"shop":999,"unknown":2}

def max_legit_relic_picks(mpt, model_id=None):
    """对齐 C# AncientRuleLoader.MaxLegitRelicPicks(mapPointType, modelId)"""
    m = mpt.lower()
    mid = (model_id or "").upper() or None
    # 精确匹配 mpt + model_id
    if mid and (m, mid) in ceilings_map:
        return ceilings_map[(m, mid)]
    # 回退：仅按 mpt
    if m in ceilings_mpt:
        return ceilings_mpt[m]
    return DEFAULT_CEILINGS.get(m, 2)

# === 对齐 C# MapNodeShopUtil ===
def is_shop_like(node):
    """对齐 C# MapNodeShopUtil.IsShopLikeMapNode"""
    mpt = (node.get("map_point_type") or "").strip().lower()
    if mpt and mpt not in ("none", ""):
        return mpt == "shop"
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").strip().lower()
        if rt and rt not in ("none", ""):
            return rt == "shop"
    return False

def has_shop_transaction(node):
    """对齐 C# MapNodeShopUtil.HasShopTransaction"""
    for ps in node.get("player_stats", []):
        if (ps.get("bought_relics") or ps.get("bought_colorless") or
            ps.get("bought_potions")):
            return True
    return False

def is_spoils_map_payout(stat, gold):
    if gold <= 0 or gold > 1600: return False
    for q in stat.get("completed_quests", []):
        if q and "SPOILS_MAP" in q.upper(): return True
    for c in stat.get("cards_removed", []):
        if "SPOILS_MAP" in (c.get("id") or "").upper(): return True
    return False

def should_skip_non_shop_gold(node):
    """对齐 C# AncientRuleLoader.ShouldSkipNonShopGold + NonShopLargeGoldRule"""
    if is_shop_like(node): return True
    mpt = (node.get("map_point_type") or "").strip().lower()
    if mpt == "ancient": return True
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").strip().lower()
        if rt == "ancient": return True
    if has_shop_transaction(node): return True
    return False

# === 主扫描 ===
print("=" * 70)
print("修复后 C# 逻辑模拟扫描")
print("=" * 70)

total = 0
nsl = []   # NonShopLargeGold
rmp = []   # RelicMultiPick

for rp in sorted(HISTORY_DIR.glob("*.run")):
    try:
        with rp.open(encoding="utf-8") as f:
            d = json.load(f)
    except:
        continue
    total += 1

    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            # --- NonShopLargeGold ---
            if not should_skip_non_shop_gold(node):
                for ps in node.get("player_stats", []):
                    g = ps.get("gold_gained", 0)
                    if g >= 250 and not is_spoils_map_payout(ps, g):
                        rids = [(r.get("room_type"), r.get("model_id")) for r in node.get("rooms", [])]
                        nsl.append((rp.stem, ai, node.get("map_point_type"), g, rids))

            # --- RelicMultiPick ---
            if is_shop_like(node) or has_shop_transaction(node):
                continue
            mpt = node.get("map_point_type") or ""
            mid = None
            rooms = node.get("rooms")
            if rooms:
                first_room = rooms[0]
                mid = first_room.get("model_id")
            ceiling = max_legit_relic_picks(mpt, mid)
            for ps in node.get("player_stats", []):
                picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                if picks > ceiling:
                    rids = [(r.get("room_type"), r.get("model_id")) for r in rooms]
                    rmp.append((rp.stem, ai, mpt, mid, picks, ceiling, rids))

print(f"\n扫描存档: {total} 个")
print(f"\nNonShopLargeGold 异常: {len(nsl)} 条")
for x in nsl:
    print(f"  [{x[0]}] act={x[1]}  mpt={x[2]!r:12s}  gold={x[3]:4d}  rooms={x[4]}")

print(f"\nRelicMultiPick 异常: {len(rmp)} 条")
for x in rmp:
    print(f"  [{x[0]}] act={x[1]}  mpt={x[2]!r:12s}  model_id={str(x[3]):15s}  picks={x[4]}  ceiling={x[5]}  rooms={x[6]}")

if not nsl and not rmp:
    print("\n✓ 所有已知合法场景已无异常告警")
