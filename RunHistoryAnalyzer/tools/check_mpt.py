# -*- coding: utf-8 -*-
import json
from pathlib import Path
HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")
rp = HISTORY_DIR / "1773723718.run"
with rp.open(encoding="utf-8") as f:
    d = json.load(f)
node = d["map_point_history"][0][0]
print("map_point_type:", repr(node.get("map_point_type")))
print("rooms[0].room_type:", repr(node.get("rooms", [{}])[0].get("room_type")))
print("rooms[0].model_id:", repr(node.get("rooms", [{}])[0].get("model_id")))
