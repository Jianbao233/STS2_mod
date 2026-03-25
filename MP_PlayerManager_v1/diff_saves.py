# -*- coding: utf-8 -*-
"""
对比两次存档的差异，识别所有在游戏过程中变化的字段
"""
import json
from pathlib import Path
from pprint import pprint

BEFORE_PATH = Path(
    r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\backups\modded_p1_auto_before_copy_20260313_175703\current_run_mp.save"
)
AFTER_PATH = Path(
    r"c:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\modded\profile1\saves\current_run_mp.save"
)


def flatten(obj, prefix=""):
    """将对象展平成 key: value 形式"""
    result = {}
    if isinstance(obj, dict):
        for k, v in obj.items():
            key = f"{prefix}.{k}" if prefix else k
            if isinstance(v, (dict, list)):
                result[key] = repr(v)[:80]
            else:
                result[key] = v
    elif isinstance(obj, list):
        result[prefix] = f"list[{len(obj)}]"
    else:
        result[prefix] = obj
    return result


def compare_flat(a, b, path="root"):
    """对比两个扁平字典，找出差异"""
    all_keys = set(a.keys()) | set(b.keys())
    changes = []
    for k in sorted(all_keys):
        if k not in a:
            changes.append(f"  NEW: {k} = {repr(b[k])[:60]}")
        elif k not in b:
            changes.append(f"  REMOVED: {k} was {repr(a[k])[:60]}")
        elif a[k] != b[k]:
            changes.append(f"  CHANGED: {k}")
            changes.append(f"    before: {repr(a[k])[:70]}")
            changes.append(f"    after:  {repr(b[k])[:70]}")
    return changes


def diff_players(before_players, after_players):
    """逐玩家对比"""
    for i in range(max(len(before_players), len(after_players))):
        print(f"\n{'='*60}")
        print(f"  玩家 {i}")
        print('='*60)
        if i >= len(before_players):
            print(f"  NEW PLAYER:")
            pprint(after_players[i])
            continue
        if i >= len(after_players):
            print(f"  REMOVED (was):")
            pprint(before_players[i])
            continue

        bp = before_players[i]
        ap = after_players[i]

        print(f"  基本属性:")
        for key in ['net_id', 'character_id', 'current_hp', 'max_hp', 'gold']:
            if bp.get(key) != ap.get(key):
                print(f"    {key}: {bp.get(key)} -> {ap.get(key)}")

        # deck diff
        bd = {c['id']: c for c in bp.get('deck', [])}
        ad = {c['id']: c for c in ap.get('deck', [])}
        new_cards = set(ad.keys()) - set(bd.keys())
        removed_cards = set(bd.keys()) - set(ad.keys())
        if new_cards:
            for card_id in new_cards:
                info = ad[card_id]
                up = info.get('current_upgrade_level', 0)
                print(f"    新卡牌: {card_id} (upgrade={up})")
        if removed_cards:
            print(f"    移除卡牌: {removed_cards}")

        # relics diff
        br = {r['id']: r for r in bp.get('relics', [])}
        ar = {r['id']: r for r in ap.get('relics', [])}
        new_relics = set(ar.keys()) - set(br.keys())
        removed_relics = set(br.keys()) - set(ar.keys())
        if new_relics:
            print(f"    新遗物: {new_relics}")
        if removed_relics:
            print(f"    移除遗物: {removed_relics}")

        # potions diff
        if bp.get('potions') != ap.get('potions'):
            print(f"    药水变化:")
            print(f"      before: {bp.get('potions')}")
            print(f"      after:  {ap.get('potions')}")

        # rng counters diff
        brc = bp.get('rng', {}).get('counters', {})
        arc = ap.get('rng', {}).get('counters', {})
        for k2 in set(brc.keys()) | set(arc.keys()):
            if brc.get(k2) != arc.get(k2):
                print(f"    rng.{k2}: {brc.get(k2)} -> {arc.get(k2)}")

        # odds diff
        bo = bp.get('odds', {})
        ao = ap.get('odds', {})
        for k2 in set(bo.keys()) | set(ao.keys()):
            if bo.get(k2) != ao.get(k2):
                print(f"    odds.{k2}: {bo.get(k2)} -> {ao.get(k2)}")

        # relic_grab_bag diff (只看长度)
        brb = bp.get('relic_grab_bag', {}).get('relic_id_lists', {})
        arb = ap.get('relic_grab_bag', {}).get('relic_id_lists', {})
        for rarity in ['common', 'uncommon', 'rare', 'shop']:
            if len(brb.get(rarity, [])) != len(arb.get(rarity, [])):
                print(f"    relic_grab_bag.{rarity}: {len(brb.get(rarity, []))} -> {len(arb.get(rarity, []))} (已拾取)")

        # unlock_state diff
        bus = bp.get('unlock_state', {})
        aus = ap.get('unlock_state', {})
        if bus.get('number_of_runs') != aus.get('number_of_runs'):
            print(f"    unlock_state.number_of_runs: {bus.get('number_of_runs')} -> {aus.get('number_of_runs')}")
        new_epochs = set(aus.get('unlocked_epochs', [])) - set(bus.get('unlocked_epochs', []))
        if new_epochs:
            print(f"    新解锁 epochs: {new_epochs}")
        new_encounters = set(aus.get('encounters_seen', [])) - set(bus.get('encounters_seen', []))
        if new_encounters:
            print(f"    新遭遇敌人: {new_encounters}")

        # discovered_* diff
        for key in ['discovered_cards', 'discovered_relics', 'discovered_enemies', 'discovered_epochs']:
            bv = bp.get(key, [])
            av = ap.get(key, [])
            new_items = set(av) - set(bv)
            if new_items:
                print(f"    {key} 新增: {new_items}")


def diff_map_point_history(bmph, amph):
    """对比 map_point_history"""
    print(f"\n{'='*60}")
    print("  map_point_history 变化")
    print('='*60)
    for fi in range(max(len(bmph), len(amph))):
        bf = bmph[fi] if fi < len(bmph) else []
        af = amph[fi] if fi < len(amph) else []
        print(f"\n  第{fi+1}层:")
        for ni in range(max(len(bf), len(af))):
            be = bf[ni] if ni < len(bf) else {}
            ae = af[ni] if ni < len(af) else {}
            pt = ae.get('map_point_type', be.get('map_point_type', '?'))
            rooms_a = ae.get('rooms', [{}])[0] if ae.get('rooms') else {}
            rooms_b = be.get('rooms', [{}])[0] if be.get('rooms') else {}
            model_a = rooms_a.get('model_id', '?')
            model_b = rooms_b.get('model_id', '?')

            if ni >= len(bf):
                print(f"    [新节点] {pt} {model_a}")
            elif ni >= len(af):
                print(f"    [移除节点] {pt} {model_b}")
            elif model_a != model_b:
                print(f"    {pt}: {model_b} -> {model_a}")
                # player stats 对比
                stats_b = {s['player_id']: s for s in be.get('player_stats', [])}
                stats_a = {s['player_id']: s for s in ae.get('player_stats', [])}
                for pid in set(list(stats_b.keys()) + list(stats_a.keys())):
                    sb = stats_b.get(pid, {})
                    sa = stats_a.get(pid, {})
                    diff_fields = []
                    for f in ['current_hp', 'current_gold', 'damage_taken', 'hp_healed',
                               'gold_gained', 'gold_spent', 'gold_lost', 'max_hp']:
                        if sb.get(f) != sa.get(f):
                            diff_fields.append(f"{f}={sb.get(f)}->{sa.get(f)}")
                    if diff_fields:
                        print(f"      player_id={pid}: {', '.join(diff_fields)}")


def main():
    with open(BEFORE_PATH, "r", encoding="utf-8") as f:
        before = json.load(f)
    with open(AFTER_PATH, "r", encoding="utf-8") as f:
        after = json.load(f)

    print("=== 顶层字段变化 ===")
    top_changed = []
    for k in sorted(set(list(before.keys()) + list(after.keys()))):
        bv = before.get(k)
        av = after.get(k)
        if bv is None:
            top_changed.append(f"  NEW: {k}")
        elif av is None:
            top_changed.append(f"  REMOVED: {k}")
        elif bv != av:
            if isinstance(bv, (int, str, bool, type(None))):
                top_changed.append(f"  {k}: {repr(bv)[:50]} -> {repr(av)[:50]}")
            elif isinstance(bv, list):
                top_changed.append(f"  {k}: list[{len(bv)}] -> list[{len(av)}]")
            elif isinstance(bv, dict):
                keys_changed = [kk for kk in set(list(bv.keys()) + list(av.keys()))
                               if bv.get(kk) != av.get(kk)]
                top_changed.append(f"  {k}: dict keys changed: {keys_changed}")
    for c in top_changed:
        print(c)

    print()
    diff_players(before.get('players', []), after.get('players', []))

    print()
    diff_map_point_history(
        before.get('map_point_history', []),
        after.get('map_point_history', [])
    )

    # 顶层 rng
    print(f"\n{'='*60}")
    print("  顶层 rng counters 变化")
    print('='*60)
    brc = before.get('rng', {}).get('counters', {})
    arc = after.get('rng', {}).get('counters', {})
    for k in sorted(set(brc.keys()) | set(arc.keys())):
        if brc.get(k) != arc.get(k):
            print(f"  {k}: {brc.get(k)} -> {arc.get(k)}")

    # pre_finished_room
    print(f"\n{'='*60}")
    print("  pre_finished_room 变化")
    print('='*60)
    bf = before.get('pre_finished_room')
    af = after.get('pre_finished_room')
    if bf != af:
        print(f"  before: {bf}")
        print(f"  after:  {af}")

    # visited_map_coords
    print(f"\n{'='*60}")
    print("  visited_map_coords 变化")
    print('='*60)
    bv = before.get('visited_map_coords', [])
    av = after.get('visited_map_coords', [])
    new_coords = av[len(bv):]
    if new_coords:
        print(f"  新增坐标: {new_coords}")

    # acts saved_map
    print(f"\n{'='*60}")
    print("  acts.saved_map 变化（ACT.UNDERDOCKS）")
    print('='*60)
    bmap = before.get('acts', [{}])[0].get('saved_map', {})
    amap = after.get('acts', [{}])[0].get('saved_map', {})
    bpts = {p['coord'] for p in bmap.get('points', []) if p.get('can_modify')}
    apts = {p['coord'] for p in amap.get('points', []) if p.get('can_modify')}
    # 找出被访问过的点（没有 can_modify）
    bvisited = set()
    for p in bmap.get('points', []):
        if not p.get('can_modify', True):
            bvisited.add(p['coord'])
    avisited = set()
    for p in amap.get('points', []):
        if not p.get('can_modify', True):
            avisited.add(p['coord'])
    new_visited = avisited - bvisited
    if new_visited:
        print(f"  新访问节点: {sorted(new_visited, key=lambda c: (c['row'], c['col']))}")


if __name__ == "__main__":
    main()
