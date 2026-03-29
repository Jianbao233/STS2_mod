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
    # Note: name field is filled lazily via _get_name() to avoid circular import
    "CHARACTER.IRONCLAD": {
        "i18n_key": "char.ironclad",
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
        "i18n_key": "char.silent",
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
        "i18n_key": "char.defect",
        "max_hp": 75,
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
        "i18n_key": "char.necrobinder",
        "max_hp": 66,
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
        "i18n_key": "char.regent",
        "max_hp": 75,
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
    _i18n_key: str = ""  # i18n 翻译键，内置角色有值

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
    from .i18n import _ as _i18n
    result = {}
    for cid, data in BUILTIN_CHARACTERS.items():
        i18n_key = data.get("i18n_key", "")
        resolved_name = _i18n(i18n_key) if i18n_key else cid
        result[cid] = CharacterTemplate(
            character_id=cid,
            name=resolved_name,
            max_hp=data["max_hp"],
            starter_relic=data["starter_relic"],
            starter_deck=data["starter_deck"],
            is_mod=False,
            _i18n_key=i18n_key,
        )
    result.update(load_mod_templates(game_mods_dir))
    return result
