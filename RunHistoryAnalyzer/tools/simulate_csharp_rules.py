# -*- coding: utf-8 -*-
"""
直接用 C# 解析库模拟 NonShopLargeGoldGainRule + RelicMultiPickRule 的实际行为。
"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

# === 模拟 AncientRuleLoader（直接复制 C# 逻辑）===
def should_skip_non_shop_gold(mpt):
    return mpt == "ancient"

def max_legit_relic_picks(mpt):
    d = {"monster": 1, "elite": 2, "treasure": 1, "ancient": 5,
          "event": 4, "rest": 1, "boss": 1, "shop": 999}
    return d.get(mpt, 2)

# === 模拟 MapNodeShopUtil ===
def is_shop_like(node):
    mpt = (node.get("map_point_type") or "").lower()
    if mpt == "shop": return True
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").lower()
        if rt == "shop": return True
    return False

def has_shop_transaction(node):
    for ps in node.get("player_stats", []):
        if ps.get("bought_relics") or ps.get("bought_colorless") or ps.get("bought_potions"):
            return True
    return False

def is_fake_merchant_combat(node):
    for r in node.get("rooms", []):
        if (r.get("room_type") or "").lower() != "monster":
            continue
        mid = r.get("model_id") or ""
        if "FAKE_MERCHANT" in mid.upper():
            return True
    return False

def is_fake_merchant_event_only(node):
    has_fm = has_fm_ev = has_monster = False
    for r in node.get("rooms", []):
        mid = r.get("model_id") or ""
        if "EVENT.FAKE_MERCHANT" in mid.upper(): has_fm_ev = True
        if (r.get("room_type") or "").lower() == "monster": has_monster = True
    return has_fm_ev and not has_monster

# === 模拟 NonShopLargeGoldGainRule.IsSpoilsMapPayoutNode ===
def is_spoils_map_payout_node(stat, gold_gained):
    if gold_gained <= 0 or gold_gained > 1600:
        return False
    for q in stat.get("completed_quests", []):
        if q and "SPOILS_MAP" in q.upper():
            return True
    for c in stat.get("cards_removed", []):
        cid = c.get("id") or ""
        if "SPOILS_MAP" in cid.upper():
            return True
    return False

# === 模拟 NonShopLargeGoldGainRule.ShouldSkipNodeType ===
def should_skip_non_shop_gold_node(node):
    if is_shop_like(node): return True
    mpt = (node.get("map_point_type") or "").lower()
    if should_skip_non_shop_gold(mpt): return True
    for r in node.get("rooms", []):
        rt = (r.get("room_type") or "").lower()
        if should_skip_non_shop_gold(rt): return True
    if has_shop_transaction(node): return True
    if is_fake_merchant_combat(node): return True
    if is_fake_merchant_event_only(node): return True
    return False

# === 主扫描 ===
def scan_all():
    total_runs = 0
    non_shop_large = []  # (fname, act, mpt, gold, floor_idx, detail)
    relic_multi = []     # (fname, act, mpt, picks, floor_idx, detail)

    for rp in sorted(HISTORY_DIR.glob("*.run")):
        try:
            with rp.open(encoding="utf-8") as f:
                d = json.load(f)
        except:
            continue

        total_runs += 1
        floor_idx = 0

        for ai, act in enumerate(d.get("map_point_history", [])):
            for ni, node in enumerate(act):
                floor_idx += 1

                # === NonShopLargeGold ===
                if not should_skip_non_shop_gold_node(node):
                    for ps in node.get("player_stats", []):
                        g = ps.get("gold_gained", 0)
                        if g >= 250:
                            if not is_spoils_map_payout_node(ps, g):
                                non_shop_large.append((
                                    rp.stem, ai, node.get("map_point_type",""),
                                    g, floor_idx,
                                    f"mpt={node.get('map_point_type')} rooms={[(r.get('room_type'),r.get('model_id')) for r in node.get('rooms',[])[:2]]}"
                                ))

                # === RelicMultiPick ===
                if is_shop_like(node) or has_shop_transaction(node):
                    continue
                mpt = (node.get("map_point_type") or "").lower()
                ceiling = max_legit_relic_picks(mpt)
                for ps in node.get("player_stats", []):
                    picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                    if picks > ceiling:
                        relic_multi.append((
                            rp.stem, ai, node.get("map_point_type",""),
                            picks, floor_idx,
                            f"mpt={mpt} ceiling={ceiling} picks={picks}"
                        ))

    return total_runs, non_shop_large, relic_multi

runs, non_shop, relic_mult = scan_all()
print(f"扫描存档: {runs} 个")
print()
print(f"NonShopLargeGold 异常: {len(non_shop)} 条")
for x in non_shop:
    print(f"  [{x[0]}] act={x[1]} mpt={x[2]:12s} gold={x[3]:4d} (floor {x[4]})")
    print(f"    {x[5]}")
print()
print(f"RelicMultiPick 异常: {len(relic_mult)} 条")
for x in relic_mult:
    print(f"  [{x[0]}] act={x[1]} mpt={x[2]:12s} picks={x[3]} (floor {x[4]})")
    print(f"    {x[5]}")
