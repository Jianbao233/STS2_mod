# -*- coding: utf-8 -*-
"""完整验证：精确模拟 C# AncientRuleLoader（支持 mpt+model_id+room_type 三键）"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

# === 精确对齐 C# AncientRuleLoader.MaxLegitRelicPicks(mapPointType, modelId, roomType) ===
JSON = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\RunHistoryAnalyzer\Data\ancient_peoples_rules.json")
c_map_mid, c_map_rt, c_mpt = {}, {}, {}

if JSON.exists():
    with JSON.open(encoding="utf-8") as f:
        data = json.load(f)
    for ov in data.get("node_type_overrides", []):
        m = (ov.get("match", {}).get("map_point_type") or "").lower()
        mid = ov.get("match", {}).get("model_id")
        rt  = ov.get("match", {}).get("room_type")
        c   = ov.get("relic_pick_ceiling", -1)
        if c < 0: continue
        if mid: c_map_mid[(m, mid.upper())] = c
        elif rt: c_map_rt[(m, rt.lower())] = c
        else:    c_mpt[m] = c

DEFS = {"ancient":5,"monster":1,"elite":2,"treasure":1,"event":4,"boss":1,"rest":1,"shop":999,"unknown":2}

def max_legit(mpt, mid=None, rt=None):
    """对齐 C# MaxLegitRelicPicks(mapPointType, modelId, roomType) 三键查表"""
    m = mpt.lower()
    mu = (mid or "").upper() or None
    ml = (rt or "").lower() or None
    if mu and (m, mu) in c_map_mid: return c_map_mid[(m, mu)]
    if ml and (m, ml) in c_map_rt:  return c_map_rt[(m, ml)]
    if m in c_mpt: return c_mpt[m]
    return DEFS.get(m, 2)

# === MapNodeShopUtil ===
def is_shop(node):
    mt = (node.get("map_point_type") or "").strip().lower()
    if mt == "shop": return True
    for r in node.get("rooms", []):
        if (r.get("room_type") or "").strip().lower() == "shop": return True
    return False

def has_tx(node):
    return any(ps.get("bought_relics") or ps.get("bought_colorless") or ps.get("bought_potions")
               for ps in node.get("player_stats", []))

def is_spoils(stat, g):
    if g <= 0 or g > 1600: return False
    for q in stat.get("completed_quests", []):
        if q and "SPOILS_MAP" in q.upper(): return True
    for c in stat.get("cards_removed", []):
        if "SPOILS_MAP" in (c.get("id") or "").upper(): return True
    return False

def skip_gold(node):
    if is_shop(node): return True
    mt = (node.get("map_point_type") or "").strip().lower()
    if mt == "ancient": return True
    for r in node.get("rooms", []):
        if (r.get("room_type") or "").strip().lower() == "ancient": return True
    return has_tx(node)

# === 扫描 ===
print("=" * 70)
print("最终验证结果（3键查表：mpt + model_id + room_type）")
print("加载规则:", {k: v for k, v in [("mid", c_map_mid), ("rt", c_map_rt), ("mpt", c_mpt)]})
print("=" * 70)

total, nsl, rmp = 0, [], []

for rp in sorted(HISTORY_DIR.glob("*.run")):
    try:
        with rp.open(encoding="utf-8") as f:
            d = json.load(f)
    except:
        continue
    total += 1

    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            mpt_str = node.get("map_point_type") or ""
            rooms   = node.get("rooms", [])
            mid     = rooms[0].get("model_id") if rooms else None
            rt      = rooms[0].get("room_type") if rooms else None
            ceiling = max_legit(mpt_str, mid, rt)

            is_sl = is_shop(node)
            has_st = has_tx(node)

            # NonShopLargeGold
            if not skip_gold(node):
                for ps in node.get("player_stats", []):
                    g = ps.get("gold_gained", 0)
                    if g >= 250 and not is_spoils(ps, g):
                        nsl.append((rp.stem, ai, mpt_str, g, [(r.get("room_type"), r.get("model_id")) for r in rooms[:2]]))

            # RelicMultiPick
            if not (is_sl or has_st):
                for ps in node.get("player_stats", []):
                    picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                    if picks > ceiling:
                        rmp.append((rp.stem, ai, mpt_str, mid, rt, picks, ceiling,
                                   [(r.get("room_type"), r.get("model_id")) for r in rooms[:2]]))

print(f"\n扫描存档: {total} 个")

print(f"\nNonShopLargeGold 异常: {len(nsl)} 条")
for x in nsl:
    print(f"  [{x[0]}] act={x[1]}  mpt={x[2]!r:12s}  gold={x[3]:4d}  rooms={x[4]}")

print(f"\nRelicMultiPick 异常: {len(rmp)} 条")
for x in rmp:
    print(f"  [{x[0]}] act={x[1]}  mpt={x[2]!r:12s}  mid={str(x[3])!r:15s}  rt={str(x[4])!r:8s}  picks={x[5]}  ceiling={x[6]}  rooms={x[7]}")

if not nsl and not rmp:
    print("\n所有已知合法场景已无异常告警，可关闭游戏后重新构建 DLL 测试。")
else:
    print("\n仍有异常，请检查上述条目。")
