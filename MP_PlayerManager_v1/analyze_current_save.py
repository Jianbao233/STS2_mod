# -*- coding: utf-8 -*-
"""
v2 存档结构深度分析脚本
分析当前游戏版本的 current_run_mp.save，输出结构报告
"""

import base64
import gzip
import json
from pathlib import Path
from pprint import pprint

SAVE_PATH = Path(
    r"c:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\modded\profile1\saves\current_run_mp.save"
)


def deep_compare(obj1, obj2, path="root", indent=0):
    """递归对比两个对象，找出差异"""
    prefix = "  " * indent
    changes = []

    if type(obj1) != type(obj2):
        changes.append(f"{prefix}{path}: type differ {type(obj1).__name__} vs {type(obj2).__name__}")
        return changes

    if isinstance(obj1, dict):
        all_keys = set(obj1.keys()) | set(obj2.keys())
        for k in sorted(all_keys):
            if k not in obj1:
                changes.append(f"{prefix}{path}.{k}: NEW key = {repr(obj2[k])[:60]}")
            elif k not in obj2:
                changes.append(f"{prefix}{path}.{k}: REMOVED (was {repr(obj1[k])[:60]})")
            else:
                changes.extend(deep_compare(obj1[k], obj2[k], f"{path}.{k}", indent + 1))
    elif isinstance(obj1, list):
        if len(obj1) != len(obj2):
            changes.append(f"{prefix}{path}: list len {len(obj1)} -> {len(obj2)}")
        else:
            for i in range(min(len(obj1), 5)):
                changes.extend(deep_compare(obj1[i], obj2[i], f"{path}[{i}]", indent + 1))
    else:
        if obj1 != obj2:
            changes.append(f"{prefix}{path}: {repr(obj1)[:40]} -> {repr(obj2)[:40]}")

    return changes


def analyze_player(player: dict, idx: int):
    print(f"\n{'='*60}")
    print(f"  玩家 {idx}  — {player.get('character_id', '?')}")
    print(f"{'='*60}")

    print(f"  基础身份")
    print(f"    net_id:              {player.get('net_id')}")
    print(f"    character_id:        {player.get('character_id')}")
    print(f"    current_hp / max_hp: {player.get('current_hp')} / {player.get('max_hp')}")
    print(f"    gold:                {player.get('gold')}")
    print(f"    max_energy:          {player.get('max_energy')}")
    print(f"    max_potion_slot_count: {player.get('max_potion_slot_count')}")
    print(f"    base_orb_slot_count: {player.get('base_orb_slot_count')}")

    print(f"\n  牌组 deck ({len(player.get('deck', []))} 张)")
    for card in player.get("deck", [])[:5]:
        upgrade = card.get("current_upgrade_level", 0)
        up_str = f" (+{upgrade})" if upgrade else ""
        print(f"    {card['id']}{up_str}")
    if len(player.get("deck", [])) > 5:
        print(f"    ... 共 {len(player['deck'])} 张")

    print(f"\n  遗物 relics ({len(player.get('relics', []))} 件)")
    for r in player.get("relics", []):
        print(f"    {r['id']} (floor {r.get('floor_added_to_deck')})")

    print(f"\n  药水 potions ({len(player.get('potions', []))} 瓶)")
    for pot in player.get("potions", []):
        print(f"    {pot['id']} @ slot {pot.get('slot_index')}")

    print(f"\n  随机数 rng")
    print(f"    seed:    {player.get('rng', {}).get('seed')}")
    counters = player.get('rng', {}).get('counters', {})
    for k, v in counters.items():
        print(f"    {k}: {v}")

    print(f"\n  概率 odds")
    print(f"    card_rarity_odds_value:   {player.get('odds', {}).get('card_rarity_odds_value')}")
    print(f"    potion_reward_odds_value:  {player.get('odds', {}).get('potion_reward_odds_value')}")

    print(f"\n  遗物袋 relic_grab_bag")
    rb = player.get("relic_grab_bag", {}).get("relic_id_lists", {})
    for rarity, items in rb.items():
        print(f"    {rarity}: {len(items)} 件")

    print(f"\n  已发现 discovered_*")
    print(f"    discovered_cards:    {len(player.get('discovered_cards', []))} 张")
    print(f"    discovered_relics:  {len(player.get('discovered_relics', []))} 件")
    print(f"    discovered_enemies: {len(player.get('discovered_enemies', []))} 种")
    print(f"    discovered_epochs:  {len(player.get('discovered_epochs', []))} 个")

    print(f"\n  解锁状态 unlock_state")
    us = player.get("unlock_state", {})
    print(f"    number_of_runs:  {us.get('number_of_runs')}")
    print(f"    unlocked_epochs: {len(us.get('unlocked_epochs', []))} 个")
    print(f"    encounters_seen:  {len(us.get('encounters_seen', []))} 种")

    print(f"\n  extra_fields: {player.get('extra_fields')}")


def analyze_map_point_history(mph: list):
    print(f"\n{'='*60}")
    print(f"  map_point_history ({len(mph)} 层)")
    print(f"{'='*60}")

    for floor_idx, floor in enumerate(mph):
        print(f"\n  第 {floor_idx + 1} 层 ({len(floor)} 个节点):")
        for node_idx, node in enumerate(floor[:3]):
            pt = node.get("map_point_type", "?")
            rooms = node.get("rooms", [])
            stats = node.get("player_stats", [])

            room_info = ""
            if rooms:
                r = rooms[0]
                room_info = f" -> {r.get('room_type', '?')} {r.get('model_id', '')}"

            print(f"    [{node_idx}] {pt}{room_info}")
            for s in stats:
                pid = s.get("player_id", "?")
                hp = s.get("current_hp", 0)
                gold = s.get("current_gold", 0)
                maxhp = s.get("max_hp", 0)
                dmg = s.get("damage_taken", 0)
                print(f"        player_id={pid} hp={hp}/{maxhp} gold={gold} dmg_taken={dmg}")


def decode_map_drawings(b64str: str):
    if not b64str:
        return None
    try:
        raw = base64.b64decode(b64str)
        decompressed = gzip.decompress(raw)
        return json.loads(decompressed.decode("utf-8"))
    except Exception as e:
        return f"解码失败: {e}"


def main():
    print(f"读取存档: {SAVE_PATH}\n")

    with open(SAVE_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    print("=" * 60)
    print("  顶层字段总览")
    print("=" * 60)
    for k in sorted(data.keys()):
        v = data[k]
        t = type(v).__name__
        if isinstance(v, list):
            print(f"  {k}: [{t}]  ({len(v)} items)")
        elif isinstance(v, dict):
            print(f"  {k}: [{t}]  ({len(v)} keys)")
        elif isinstance(v, str) and len(v) > 60:
            print(f"  {k}: [{t}]  {v[:60]}...")
        else:
            print(f"  {k}: {repr(v)}")

    # Players
    players = data.get("players", [])
    for i, p in enumerate(players):
        analyze_player(p, i)

    # map_point_history
    analyze_map_point_history(data.get("map_point_history", []))

    # 顶层 odds
    print(f"\n{'='*60}")
    print("  顶层 odds（区别于 per-player odds）")
    print("=" * 60)
    for k, v in data.get("odds", {}).items():
        print(f"  {k}: {v}")

    # 顶层 rng
    print(f"\n{'='*60}")
    print("  顶层 rng")
    print("=" * 60)
    rng = data.get("rng", {})
    print(f"  seed: {rng.get('seed')}")
    for k, v in rng.get("counters", {}).items():
        print(f"  {k}: {v}")

    # shared_relic_grab_bag
    print(f"\n{'='*60}")
    print("  shared_relic_grab_bag")
    print("=" * 60)
    srb = data.get("shared_relic_grab_bag", {}).get("relic_id_lists", {})
    for rarity, items in srb.items():
        print(f"  {rarity}: {len(items)} 件")
        if rarity in ("event", "ancient"):
            print(f"    示例: {items[:3]}")

    # map_drawings
    print(f"\n{'='*60}")
    print("  map_drawings (Base64+Gzip)")
    print("=" * 60)
    md = decode_map_drawings(data.get("map_drawings", ""))
    if isinstance(md, dict):
        print(f"  绘制数量: {len(md.get('drawings', []))} 条")
        for d in md.get("drawings", [])[:3]:
            pid = d.get("playerId", "?")
            coords = d.get("points", [])[:2]
            print(f"    playerId={pid}, points={coords}")
    else:
        print(f"  {md}")

    # pre_finished_room
    print(f"\n{'='*60}")
    print("  pre_finished_room")
    print("=" * 60)
    pprint(data.get("pre_finished_room"))

    # acts
    print(f"\n{'='*60}")
    print("  acts (3个幕)")
    print("=" * 60)
    for act in data.get("acts", []):
        act_id = act.get("id", "?")
        rooms = act.get("rooms", {})
        elite_ids = rooms.get("elite_encounter_ids", [])
        event_ids = rooms.get("event_ids", [])
        normal_ids = rooms.get("normal_encounter_ids", [])
        saved_map = act.get("saved_map", {})
        map_h = saved_map.get("height", 0)
        map_w = saved_map.get("width", 0)
        print(f"  {act_id}: elite={len(elite_ids)} event={len(event_ids)} normal={len(normal_ids)} map={map_w}x{map_h}")

    # visited_map_coords
    print(f"\n{'='*60}")
    print("  visited_map_coords")
    print("=" * 60)
    for c in data.get("visited_map_coords", []):
        print(f"    row={c['row']} col={c['col']}")

    # extra_fields
    print(f"\n{'='*60}")
    print("  顶层 extra_fields")
    print("=" * 60)
    pprint(data.get("extra_fields"))

    print(f"\n\n{'='*60}")
    print("  schema_version: " + str(data.get("schema_version")))
    print("  platform_type:  " + str(data.get("platform_type")))
    print("  ascension:       " + str(data.get("ascension")))
    print("  current_act_index: " + str(data.get("current_act_index")))
    print(f"{'='*60}")


if __name__ == "__main__":
    main()
