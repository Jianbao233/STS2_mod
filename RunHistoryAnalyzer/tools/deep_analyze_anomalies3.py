# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")


def analyze_run(fname, act_idx, node_idx):
    rp = HISTORY_DIR / (fname + ".run")
    if not rp.exists():
        print(f"[{fname}] 文件不存在")
        return
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)

    acts = d.get("map_point_history", [])
    players = d.get("players", [])

    print(f"\n{'='*70}")
    print(f"存档: {fname}  |  win={d.get('win')}  |  build={d.get('build_id')}")
    print(f"玩家数: {len(players)}")
    for pi, p in enumerate(players):
        print(f"  players[{pi}]: id={p.get('id')}  character={p.get('character')}")

    node = acts[act_idx][node_idx]
    rooms = node.get("rooms", [])
    print(f"\n节点 act={act_idx} node={node_idx}  mpt={node.get('map_point_type')}")
    print(f"rooms: {[(r.get('room_type'), r.get('model_id')) for r in rooms]}")

    all_quests = d.get("completed_quests", [])
    print(f"completed_quests: {all_quests}")

    for pi, ps in enumerate(node.get("player_stats", [])):
        pid = ps.get("player_id", "?")
        gold = ps.get("gold_gained", 0)
        picks = sum(1 for c in ps.get("relic_choices", []) if c.get("was_picked"))
        all_choices = ps.get("relic_choices", [])
        was_picked_choices = [c for c in all_choices if c.get("was_picked")]
        print(f"\n  player_stats[{pi}] player_id={pid}:")
        print(f"    gold_gained={gold}  current_gold={ps.get('current_gold')}  gold_spent={ps.get('gold_spent')}")
        print(f"    relic_choices 总数={len(all_choices)}  was_picked=true的={len(was_picked_choices)}")
        for ci, c in enumerate(all_choices):
            print(f"      [{ci}] was_picked={c.get('was_picked')}  choice={c.get('choice')}")
        print(f"    cards_gained: {[c.get('id') for c in ps.get('cards_gained', [])[:8]]}")
        print(f"    cards_removed: {[c.get('id') for c in ps.get('cards_removed', [])[:5]]}")

    # 玩家最终遗物
    for pi, p in enumerate(players):
        relics = p.get("relics", [])
        paels = [r.get("id") for r in relics if "PAELS" in (r.get("id") or "")]
        sea_glass = [r.get("id") for r in relics if "SEA_GLASS" in (r.get("id") or "")]
        print(f"\n  players[{pi}] 最终遗物: {len(relics)} 件")
        print(f"    含PAELS: {paels}")
        print(f"    含SEA_GLASS: {sea_glass}")
        print(f"    前10件: {[r.get('id') for r in relics[:10]]}")


# 逐个分析
cases = [
    # (fname, act_idx, node_idx, 说明)
    ("1773723718", 0, 0, "RelicMultiPick monster picks=10 — 存档 mpt=ancient"),
    ("1773490470", 0, 0, "RelicMultiPick boss picks=2 — boss 节点 2 个 relic_choices was_picked"),
    ("1773771912", 0, 1, "NonShopLargeGold monster gold=1000 — SLIMES_WEAK 异常"),
    ("1773059516", 1, 7, "treasure gold=645/650 — SPOILS_MAP 结算"),
]

for fname, act_idx, node_idx, note in cases:
    print(f"\n### {note}")
    analyze_run(fname, act_idx, node_idx)
