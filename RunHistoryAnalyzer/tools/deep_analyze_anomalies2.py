# -*- coding: utf-8 -*-
import json
from pathlib import Path

HISTORY_DIR = Path(r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history")

cases = [
    ("1773723718", 0, 0),   # RelicMultiPick monster picks=10
    ("1773490470", 0, 0),   # RelicMultiPick boss picks=2
    ("1773771912", 0, 1),   # NonShopLargeGold monster gold=1000
    ("1773059516", 1, None), # treasure gold=645,650
    ("1773484674", 1, None), # treasure gold=763
    ("1773599019", 1, None), # treasure gold=633
]


def analyze_run(fname, act_idx, node_idx):
    rp = HISTORY_DIR / (fname + ".run")
    if not rp.exists():
        print(f"[{fname}] 文件不存在")
        return
    with rp.open(encoding="utf-8") as f:
        d = json.load(f)

    print(f"\n{'='*60}")
    print(f"存档: {fname}  |  win={d.get('win')}  |  build={d.get('build_id')}")

    acts = d.get("map_point_history", [])
    player_relics = d.get("players", [{}])[0].get("relics", [])
    paels = [r.get("id") for r in player_relics if "PAELS" in (r.get("id") or "")]
    all_quests = d.get("completed_quests", [])

    if node_idx is not None:
        if act_idx < len(acts) and node_idx < len(acts[act_idx]):
            _print_node(d, act_idx, node_idx, acts[act_idx][node_idx], paels, all_quests)
        else:
            print(f"  节点 act={act_idx} node={node_idx} 不存在")
    else:
        if act_idx < len(acts):
            for ni, node in enumerate(acts[act_idx]):
                for ps in node.get("player_stats", []):
                    if ps.get("gold_gained", 0) > 200:
                        _print_node(d, act_idx, ni, node, paels, all_quests)


def _print_node(d, act_idx, node_idx, node, paels, all_quests):
    rooms = node.get("rooms", [])
    print(f"  act={act_idx} node={node_idx}  mpt={node.get('map_point_type')}")
    print(f"  rooms: {[(r.get('room_type'), r.get('model_id')) for r in rooms]}")
    print(f"  全局 completed_quests: {all_quests}")
    print(f"  玩家含PAELS遗物: {paels[:10]}")

    for ps_idx, ps in enumerate(node.get("player_stats", [])):
        gold = ps.get("gold_gained", 0)
        if gold > 0:
            picks = [c for c in ps.get("relic_choices", []) if c.get("was_picked")]
            print(f"  player_stats[{ps_idx}]: gold_gained={gold}  current_gold={ps.get('current_gold')}")
            print(f"    relic_choices was_picked: {[c.get('choice') for c in picks]}")
            print(f"    cards_gained: {[c.get('id') for c in ps.get('cards_gained', [])[:5]]}")
            print(f"    cards_removed: {[c.get('id') for c in ps.get('cards_removed', [])[:5]]}")


for fname, act_idx, node_idx in cases:
    analyze_run(fname, act_idx, node_idx)
