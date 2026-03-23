# -*- coding: utf-8 -*-
"""完整精确模拟 C# AncientRuleLoader + RelicMultiPickRule"""
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

# === 加载 JSON 规则 ===
JSON = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\RunHistoryAnalyzer\Data\ancient_peoples_rules.json")
c_map, c_mpt = {}, {}

if JSON.exists():
    with JSON.open(encoding="utf-8") as f:
        data = json.load(f)
    for ov in data.get("node_type_overrides", []):
        m = (ov.get("match", {}).get("map_point_type") or "").lower()
        mid = ov.get("match", {}).get("model_id")
        c = ov.get("relic_pick_ceiling", -1)
        if c < 0: continue
        if mid: c_map[(m, mid.upper())] = c
        else:   c_mpt[m] = c

DEFS = {"ancient":5,"monster":1,"elite":2,"treasure":1,"event":4,"boss":1,"rest":1,"shop":999,"unknown":2}

def max_legit(mpt, mid=None):
    m = mpt.lower()
    k = (m, (mid or "").upper())
    if k in c_map: return c_map[k]
    if m in c_mpt: return c_mpt[m]
    return DEFS.get(m, 2)

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

# === 逐存档完整输出 ===
for fname in ["1773490470", "1773723718", "1773771912"]:
    rp = HISTORY_DIR / (fname + ".run")
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)

    print(f"\n{'='*65}")
    print(f"存档: {fname}  win={d.get('win')}  build={d.get('build_id')}")

    floor = 0
    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            floor += 1
            mpt = node.get("map_point_type") or ""
            mpt_disp = mpt if mpt else "(null→fallback)"
            rooms = node.get("rooms", [])
            mid = rooms[0].get("model_id") if rooms else None
            room_ids = [(r.get("room_type"), r.get("model_id")) for r in rooms[:2]]
            ceiling = max_legit(mpt, mid)

            is_sl = is_shop(node)
            has_st = has_tx(node)

            # gold
            gold_issues = []
            if not skip_gold(node):
                for ps in node.get("player_stats", []):
                    g = ps.get("gold_gained", 0)
                    if g >= 250 and not is_spoils(ps, g):
                        gold_issues.append(g)

            # picks
            pick_issues = []
            if not (is_sl or has_st):
                for ps in node.get("player_stats", []):
                    picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                    if picks > ceiling:
                        pick_issues.append(picks)

            flag = gold_issues or pick_issues
            tag = " *** ANOMALY ***" if flag else ""
            print(f"  {floor:3d}. act={ai} node={ni:2d}  mpt={mpt_disp!r:12s}  mid={str(mid or '')!r:15s}  "
                  f"ceiling={ceiling}  gold={gold_issues}  picks={pick_issues}{tag}")
