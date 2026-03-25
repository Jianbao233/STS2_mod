# -*- coding: utf-8 -*-
"""
深度分析 map_point_history 中 player_stats 的含义
通过对比战斗前后存档，确认 current_hp/current_gold 的语义
"""
import json
from pathlib import Path

AFTER_PATH = Path(
    r"c:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\modded\profile1\saves\current_run_mp.save"
)

with open(AFTER_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

mph = data.get("map_point_history", [])
players = data.get("players", [])

print("当前玩家状态:")
for p in players:
    print(f"  net_id={p['net_id']} char={p['character_id']} "
          f"hp={p['current_hp']}/{p['max_hp']} gold={p['gold']}")

print("\n\nmap_point_history 每层每节点的 player_stats 完整数据:")
for fi, floor in enumerate(mph):
    print(f"\n第{fi+1}层 ({len(floor)} 节点):")
    for ni, node in enumerate(floor):
        pt = node.get("map_point_type", "?")
        rooms = node.get("rooms", [])
        rm = rooms[0] if rooms else {}
        print(f"\n  节点{ni}: type={pt}")
        print(f"  room: {rm.get('room_type')} | {rm.get('model_id')} | turns={rm.get('turns_taken')}")

        for si, stat in enumerate(node.get("player_stats", [])):
            pid = stat.get("player_id")
            # 找到对应玩家
            player = next((p for p in players if p['net_id'] == pid), None)
            char = player['character_id'] if player else '?'
            print(f"\n    stat[{si}]: player_id={pid} ({char})")
            for k, v in stat.items():
                if k != 'player_id':
                    vstr = json.dumps(v, ensure_ascii=False) if isinstance(v, list) else repr(v)
                    print(f"      {k}: {vstr}")

print("\n\n关键分析:")
print("比较 SEAPUNK_WEAK 节点 stats vs 玩家当前状态")
p0 = players[0]
p1 = players[1]
seapunk_stats = mph[0][1].get('player_stats', [])
for s in seapunk_stats:
    pid = s['player_id']
    player = next((p for p in players if p['net_id'] == pid), None)
    if player:
        diff_hp = s.get('current_hp', 0) - player['current_hp']
        diff_gold = s.get('current_gold', 0) - player['gold']
        print(f"  player_id={pid}:")
        print(f"    SEAPUNK node stats: hp={s.get('current_hp')}/{s.get('max_hp')}, gold={s.get('current_gold')}")
        print(f"    当前玩家状态: hp={player['current_hp']}/{player['max_hp']}, gold={player['gold']}")
        print(f"    差异: hp={diff_hp:+d}, gold={diff_gold:+d}")
        print(f"    伤害: damage_taken={s.get('damage_taken')}, hp_healed={s.get('hp_healed')}")
        print(f"    金币: gold_gained={s.get('gold_gained')}, gold_spent={s.get('gold_gained')}")
