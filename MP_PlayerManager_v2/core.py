# -*- coding: utf-8 -*-
"""
核心业务逻辑层：夺舍/添加/移除玩家
严格遵循 v2 存档格式（schema_version=14）
"""

import copy
import gzip
import base64
from pathlib import Path
from typing import Optional
from dataclasses import dataclass

from .characters import CharacterTemplate
from .i18n import _


# ─── 辅助函数 ────────────────────────────────────────────────────────────────

def _build_player_stat_entry(new_player: dict, map_point_type: str) -> dict:
    """为新玩家构建 player_stats 条目"""
    stat = {
        "player_id": new_player["net_id"],
        "current_gold": new_player.get("gold", 0),
        "current_hp": new_player.get("current_hp", new_player.get("max_hp", 0)),
        "max_hp": new_player.get("max_hp", 0),
        "damage_taken": 0,
        "gold_gained": 0,
        "gold_lost": 0,
        "gold_spent": 0,
        "gold_stolen": 0,
        "hp_healed": new_player.get("max_hp", 0),
        "max_hp_gained": 0,
        "max_hp_lost": 0,
        "cards_gained": [],
        "relic_choices": [],
        "event_choices": [],
    }
    if map_point_type == "ancient":
        stat["ancient_choice"] = []
    return stat


def inject_player_into_map_history(save_data: dict, player: dict) -> None:
    """
    为已有路线的每个节点补齐该 net_id 的 player_stats（与 v1 行为一致）。
    若缺少记录，游戏在 LoadIntoLatestMapCoord 等流程中可能崩溃或无法读档。
    """
    new_id = player.get("net_id")
    if new_id is None:
        return
    for floor in save_data.get("map_point_history", []):
        for entry in floor:
            stats = entry.setdefault("player_stats", [])
            if any(s.get("player_id") == new_id for s in stats):
                continue
            mpt = entry.get("map_point_type") or ""
            stats.append(_build_player_stat_entry(player, mpt))


def remap_player_id_in_map_history(save_data: dict, old_id: int, new_id: int) -> None:
    """夺舍更换 net_id 时，同步更新 map_point_history 中的 player_id 引用。"""
    if old_id == new_id:
        return
    for floor in save_data.get("map_point_history", []):
        for entry in floor:
            for s in entry.get("player_stats", []):
                if s.get("player_id") == old_id:
                    s["player_id"] = new_id


def _build_deck(template: CharacterTemplate, floor: int = 1) -> list[dict]:
    """从角色模板构建初始牌组"""
    return [
        {"id": card_id, "floor_added_to_deck": floor}
        for card_id in template.starter_deck
    ]


def _build_relics(template: CharacterTemplate, floor: int = 1) -> list[dict]:
    """从角色模板构建初始遗物"""
    if not template.starter_relic:
        return []
    return [
        {"id": template.starter_relic, "floor_added_to_deck": floor}
    ]


def _build_new_player(
    net_id: int,
    character_id: str,
    template: CharacterTemplate,
    gold: int = 100,
    copy_source: Optional[dict] = None,
) -> dict:
    """构建新玩家数据结构（full schema_version=14）"""
    max_hp = template.max_hp

    if copy_source:
        deck = copy.deepcopy(copy_source.get("deck", []))
        relics = copy.deepcopy(copy_source.get("relics", []))
        rng = copy.deepcopy(copy_source.get("rng", {}))
        odds = copy.deepcopy(copy_source.get("odds", {}))
        relic_grab_bag = copy.deepcopy(copy_source.get("relic_grab_bag", {}))
    else:
        deck = _build_deck(template)
        relics = _build_relics(template)
        rng = {"counters": {"rewards": 0, "shops": 0, "transformations": 0}, "seed": 0}
        odds = {"card_rarity_odds_value": 0.0, "potion_reward_odds_value": 0.5}
        relic_grab_bag = {
            "relic_id_lists": {
                "common": [],
                "uncommon": [],
                "rare": [],
                "shop": [],
            }
        }

    return {
        "net_id": net_id,
        "character_id": character_id,
        "current_hp": max_hp,
        "max_hp": max_hp,
        "gold": gold,
        "max_energy": 3,
        "max_potion_slot_count": 3,
        "base_orb_slot_count": 0,
        "deck": deck,
        "relics": relics,
        "potions": [],
        "rng": rng,
        "odds": odds,
        "relic_grab_bag": relic_grab_bag,
        "discovered_cards": [],
        "discovered_relics": [],
        "discovered_enemies": [],
        "discovered_epochs": [],
        "unlock_state": {
            "number_of_runs": 0,
            "unlocked_epochs": [],
            "encounters_seen": [],
        },
        "extra_fields": {},
    }


# ─── 核心操作 ────────────────────────────────────────────────────────────────


@dataclass
class OperationResult:
    success: bool
    message: str
    details: Optional[dict] = None


def take_over_player(
    save_data: dict,
    source_player_idx: int,
    new_net_id: int,
) -> OperationResult:
    """
    夺舍：替换 source_player_idx 位置的玩家 net_id，保留所有游戏数据（deck/relics/gold/rng/odds），只换身份
    """
    players = save_data.get("players", [])
    if not (0 <= source_player_idx < len(players)):
        return OperationResult(False, _("result.takeover.idx_out_of_range", source_player_idx, len(players)))

    source = players[source_player_idx]
    original_id = source.get("net_id")

    # 替换身份
    source["net_id"] = new_net_id
    remap_player_id_in_map_history(save_data, original_id, new_net_id)

    # 清理发现列表（新玩家视角）
    source["discovered_cards"] = []
    source["discovered_enemies"] = []
    source["discovered_epochs"] = []
    source["unlock_state"] = {
        "number_of_runs": 0,
        "unlocked_epochs": [],
        "encounters_seen": [],
    }

    # 清理 extra_fields
    source["extra_fields"] = {}

    return OperationResult(
        True,
        _("result.takeover.success", source_player_idx + 1, original_id, new_net_id),
        {
            "original_id": original_id,
            "new_id": new_net_id,
            "character_id": source.get("character_id"),
        },
    )


def add_player_copy(
    save_data: dict,
    source_player_idx: int,
    new_net_id: int,
) -> OperationResult:
    """
    添加玩家（复制模式）：深拷贝源玩家，满血、清空药水，并注入 map 历史
    """
    players = save_data.get("players", [])
    if not (0 <= source_player_idx < len(players)):
        return OperationResult(False, _("result.add.idx_out_of_range", source_player_idx))

    if any(p.get("net_id") == new_net_id for p in players):
        return OperationResult(False, _("result.add.id_conflict", new_net_id))

    source = players[source_player_idx]
    new_player = copy.deepcopy(source)

    new_player["net_id"] = new_net_id
    new_player["current_hp"] = new_player.get("max_hp", new_player.get("current_hp", 0))
    new_player["potions"] = []

    char_id = new_player.get("character_id", "?")

    players.append(new_player)
    save_data["players"] = players
    inject_player_into_map_history(save_data, new_player)

    return OperationResult(
        True,
        _("result.add.copy_success", str(new_net_id), char_id),
        {"player_index": len(players) - 1, "character_id": char_id},
    )


def add_player_fresh(
    save_data: dict,
    new_net_id: int,
    template: CharacterTemplate,
    gold: int = 100,
) -> OperationResult:
    """
    添加玩家（初始牌组模式）：以全新初始状态加入
    """
    new_player = _build_new_player(
        net_id=new_net_id,
        character_id=template.character_id,
        template=template,
        gold=gold,
        copy_source=None,
    )
    save_data["players"].append(new_player)
    inject_player_into_map_history(save_data, new_player)

    return OperationResult(
        True,
        _("result.add.fresh_success", str(new_net_id), template.name),
        {"player_index": len(save_data["players"]) - 1, "character_id": template.character_id},
    )


def remove_player(
    save_data: dict,
    player_idx: int,
) -> OperationResult:
    """
    移除玩家：清理该玩家在所有关联字段中的数据
    """
    players = save_data.get("players", [])
    if not (0 <= player_idx < len(players)):
        return OperationResult(False, _("result.remove.idx_out_of_range", player_idx))

    removed = players.pop(player_idx)
    removed_id = removed.get("net_id")
    removed_char = removed.get("character_id", "?")

    # 1. 清理 relic_grab_bag（各 rarity 列表）
    for player in save_data.get("players", []):
        bag = player.get("relic_grab_bag", {})
        id_lists = bag.get("relic_id_lists", {})
        for rarity in ["common", "uncommon", "rare", "shop"]:
            if rarity in id_lists:
                id_lists[rarity] = []

    # 2. 清理 shared_relic_grab_bag（移除该玩家可能留下的记录）
    shared = save_data.get("shared_relic_grab_bag", {})
    id_lists = shared.get("relic_id_lists", {})
    for rarity in ["common", "uncommon", "rare", "shop", "event", "ancient"]:
        if rarity in id_lists:
            id_lists[rarity] = []

    # 3. 清理 map_point_history 中的 player_stats
    mph = save_data.get("map_point_history", [])
    for floor in mph:
        for node in floor:
            node_stats = node.get("player_stats", [])
            node["player_stats"] = [s for s in node_stats if s.get("player_id") != removed_id]

    # 4. 清理 map_drawings（gzip+base64，移除该玩家绘制的涂鸦）
    map_drawings = save_data.get("map_drawings", "")
    if map_drawings:
        try:
            raw = base64.b64decode(map_drawings)
            if raw[:2] == b'\x1f\x8b':
                raw = gzip.decompress(raw)
            drawing_data = raw.decode("utf-8")
            import json as _json
            dd = _json.loads(drawing_data)
            # 移除该玩家的所有涂鸦
            if isinstance(dd, dict) and "drawings" in dd:
                dd["drawings"] = [
                    d for d in dd.get("drawings", [])
                    if d.get("player_id") != removed_id
                ]
                map_drawings = base64.b64encode(
                    gzip.compress(_json.dumps(dd, ensure_ascii=False).encode("utf-8"))
                ).decode("ascii")
                save_data["map_drawings"] = map_drawings
        except Exception:
            pass  # 解析失败不阻塞，保留原数据

    return OperationResult(
        True,
        _("result.remove.success", player_idx + 1, removed_char, removed_id),
        {
            "removed_id": removed_id,
            "character_id": removed_char,
            "removed_deck_size": len(removed.get("deck", [])),
            "removed_relics": [r.get("id") for r in removed.get("relics", [])],
        },
    )
