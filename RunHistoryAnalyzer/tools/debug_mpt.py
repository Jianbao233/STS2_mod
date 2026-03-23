# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")
rp = HISTORY_DIR / "1773723718.run"
with rp.open(encoding="utf-8") as f:
    d = json.load(f)

node = d["map_point_history"][0][0]
print("=== 直接读 JSON ===")
print("map_point_type:", repr(node.get("map_point_type")))
print("rooms[0]:", node.get("rooms", [{}])[0])
print()

# 对齐 C# RunHistoryData.cs MapPointHistoryEntry 反序列化
# C# MapPointType 是 string，默认 "" (空字符串)
# 但 JSON 里有 "map_point_type": "ancient"
# 如果 C# 反序列化失败，返回 "" 而不是 "ancient"

# 在 Python 里模拟 C# 反序列化的效果
import re

class MapPointHistoryEntry:
    def __init__(self, d):
        # C# MapPointType = "" if not present
        self.MapPointType = d.get("map_point_type", "")
        self.Rooms = [MapPointRoomHistoryEntry(r) for r in d.get("rooms", [])]
        self.PlayerStats = d.get("player_stats", [])

class MapPointRoomHistoryEntry:
    def __init__(self, d):
        self.ModelId = d.get("model_id")
        self.RoomType = d.get("room_type")

# 模拟 C# 反序列化
node_cs = MapPointHistoryEntry(d["map_point_history"][0][0])
print("=== 模拟 C# 反序列化 ===")
print("MapPointType:", repr(node_cs.MapPointType))
print("rooms[0].RoomType:", repr(node_cs.Rooms[0].RoomType))
print("rooms[0].ModelId:", repr(node_cs.Rooms[0].ModelId))

# C# GetMapPointType 逻辑
def get_map_point_type_csharp(node):
    mpt = node.MapPointType.strip() if node.MapPointType else ""
    if mpt and mpt.lower() not in ("unknown", "none", ""):
        return mpt.lower()
    for r in node.Rooms:
        rt = r.RoomType.strip() if r.RoomType else ""
        if rt and rt.lower() not in ("none", ""):
            return rt.lower()
    return "unknown"

print("C# GetMapPointType():", get_map_point_type_csharp(node_cs))
