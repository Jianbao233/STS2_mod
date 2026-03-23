# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

CEILINGS_MAP = {
    ("unknown", "EVENT.TRIAL"): 4,
    ("ancient", None): 5,
    ("monster", None): 1,
    ("treasure", None): 1,
}
DEFAULT_MAP = {"ancient":5,"monster":1,"elite":2,"treasure":1,"event":4,"boss":1,"rest":1,"shop":999,"unknown":2}

for fname in ["1773490470", "1773723718"]:
    rp = HISTORY_DIR / (fname + ".run")
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)
    print(f"\n=== {fname} ===")
    for ai, act in enumerate(d.get("map_point_history", [])):
        for ni, node in enumerate(act):
            mpt_v = (node.get("map_point_type") or "").strip().lower()
            is_sl = mpt_v == "shop"
            if not is_sl:
                for r in node.get("rooms", []):
                    if (r.get("room_type") or "").strip().lower() == "shop":
                        is_sl = True
                        break
            has_st = any(
                (ps.get("bought_relics") or ps.get("bought_colorless") or ps.get("bought_potions"))
                for ps in node.get("player_stats", [])
            )
            if is_sl or has_st:
                continue
            rooms = node.get("rooms", [])
            mid = rooms[0].get("model_id") if rooms else None
            mpt_str = node.get("map_point_type") or ""
            mpt_lower = mpt_str.lower()

            for pi, ps in enumerate(node.get("player_stats", [])):
                picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
                if picks == 0:
                    continue

                # 精确查表
                mid_key = (mpt_lower, (mid or "").upper() or None)
                ceiling = CEILINGS_MAP.get(mid_key, DEFAULT_MAP.get(mpt_lower, 2))

                if picks > ceiling:
                    room_info = [(r.get("room_type"), r.get("model_id")) for r in rooms[:2]]
                    print(f"  act={ai} node={ni}  mpt={mpt_str!r:12s}  mid={str(mid)!r:20s}  picks={picks}  ceiling={ceiling}  rooms={room_info}")
