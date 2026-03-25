# -*- coding: utf-8 -*-
"""
角色数据层：内置角色 + Mod 角色扫描
"""

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Optional


# ─── 基础角色定义 ───────────────────────────────────────────────────────────

BUILTIN_CHARACTERS = {
    "CHARACTER.IRONCLAD": {
        "name": "铁甲战士",
        "max_hp": 80,
        "starter_relic": "RELIC.BURNING_BLOOD",
        "starter_deck": [
            "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
            "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
            "CARD.STRIKE_IRONCLAD",
            "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
            "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
            "CARD.BASH",
        ],
    },
    "CHARACTER.SILENT": {
        "name": "静默猎手",
        "max_hp": 70,
        "starter_relic": "RELIC.RING_OF_THE_SNAKE",
        "starter_deck": [
            "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT",
            "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT",
            "CARD.STRIKE_SILENT",
            "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT",
            "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT",
            "CARD.NEUTRALIZE",
        ],
    },
    "CHARACTER.DEFECT": {
        "name": "故障机器人",
        "max_hp": 70,
        "starter_relic": "RELIC.CRACKED_CORE",
        "starter_deck": [
            "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT",
            "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT",
            "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT",
            "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT",
            "CARD.ZAP",
            "CARD.DUALCAST",
        ],
    },
    "CHARACTER.NECROBINDER": {
        "name": "亡灵契约师",
        "max_hp": 70,
        "starter_relic": "RELIC.BOUND_PHYLACTERY",
        "starter_deck": [
            "CARD.STRIKE_NECROBINDER", "CARD.STRIKE_NECROBINDER",
            "CARD.STRIKE_NECROBINDER", "CARD.STRIKE_NECROBINDER",
            "CARD.DEFEND_NECROBINDER", "CARD.DEFEND_NECROBINDER",
            "CARD.DEFEND_NECROBINDER", "CARD.DEFEND_NECROBINDER",
            "CARD.UNLEASH",
            "CARD.FLASH_OF_STEEL",
        ],
    },
    "CHARACTER.REGENT": {
        "name": "储君",
        "max_hp": 72,
        "starter_relic": "RELIC.CROWN",
        "starter_deck": [
            "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT",
            "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT",
            "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT",
            "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT",
            "CARD.CHARGE", "CARD.GLOW",
        ],
    },
}


@dataclass
class CharacterTemplate:
    character_id: str
    name: str
    max_hp: int
    starter_relic: Optional[str]
    starter_deck: list[str]
    is_mod: bool = False
    source: str = ""  # 文件来源路径

    def display_name(self) -> str:
        return self.name or self.character_id


def load_mod_templates(game_mods_dir: str) -> dict[str, CharacterTemplate]:
    """扫描游戏 mods 目录，加载所有 player_template.json"""
    templates = {}
    mods_path = Path(game_mods_dir)
    if not mods_path.exists():
        return templates

    for mod_dir in mods_path.iterdir():
        if not mod_dir.is_dir():
            continue
        tmpl_file = mod_dir / "player_template.json"
        if not tmpl_file.exists():
            continue
        try:
            with open(tmpl_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            cid = data.get("character_id", "")
            if not cid:
                continue
            templates[cid] = CharacterTemplate(
                character_id=cid,
                name=data.get("name", cid),
                max_hp=data.get("max_hp", 70),
                starter_relic=data.get("starter_relic"),
                starter_deck=data.get("starter_deck", []),
                is_mod=True,
                source=str(tmpl_file),
            )
        except (json.JSONDecodeError, OSError):
            pass
    return templates


def load_steam_names(appdata_dir: str) -> dict[str, str]:
    """从存档同级目录加载 steam_names.json"""
    steam_names = {}
    saves_dir = Path(appdata_dir)
    if not saves_dir.exists():
        return steam_names
    names_file = saves_dir / "steam_names.json"
    if names_file.exists():
        try:
            with open(names_file, "r", encoding="utf-8") as f:
                steam_names = json.load(f)
        except (json.JSONDecodeError, OSError):
            pass
    return steam_names


def get_all_characters(game_mods_dir: str) -> dict[str, CharacterTemplate]:
    """返回所有可用角色（内置 + Mod）"""
    result = {}
    for cid, data in BUILTIN_CHARACTERS.items():
        result[cid] = CharacterTemplate(
            character_id=cid,
            name=data["name"],
            max_hp=data["max_hp"],
            starter_relic=data["starter_relic"],
            starter_deck=data["starter_deck"],
            is_mod=False,
        )
    result.update(load_mod_templates(game_mods_dir))
    return result
