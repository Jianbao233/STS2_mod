# -*- coding: utf-8 -*-
"""
STS2 多人存档玩家管理工具 v1.0.0
功能：夺舍玩家 / 添加新玩家 / 移除玩家
"""

import argparse
import base64
import copy
import gzip
import json
import os
import shutil
import sys
from datetime import datetime
from pathlib import Path
from typing import List, Set, Optional, Dict, Any

# ============================================================
# 角色名映射
# ============================================================
CHARACTER_NAMES: Dict[str, str] = {
    "CHARACTER.IRONCLAD": "铁甲战士",
    "CHARACTER.SILENT": "静默猎手",
    "CHARACTER.DEFECT": "故障机器人",
    "CHARACTER.NECROBINDER": "亡灵契约师",
    "CHARACTER.REGENT": "储君",
    "CHARACTER.DEPRIVED": "剥夺者",
    "CHARACTER.WATCHER": "观者",
    "CHARACTER.HERMIT": "隐者",
    # Mod 角色占位符（实际 ID 由 mod 的 player_template.json 提供）
    "MOD.EXAMPLE_CHAR": "[Mod 角色] 未检测到 mod",
}


def get_character_display(char_id: str) -> str:
    if char_id in CHARACTER_NAMES:
        return CHARACTER_NAMES[char_id]
    if char_id and char_id.startswith("CHARACTER."):
        return char_id.replace("CHARACTER.", "")
    return char_id or "未知"




def get_initial_deck(character_id: str) -> List[Dict[str, Any]]:
    """返回指定角色的初始牌组"""
    card_ids = INITIAL_STARTER_DECKS.get(character_id, [])
    return [
        {"id": cid, "floor_added_to_deck": 1}
        for cid in card_ids
    ]


# ============================================================
# 角色最大HP映射
# ============================================================
CHARACTER_MAX_HP: Dict[str, int] = {
    "CHARACTER.IRONCLAD": 80,
    "CHARACTER.SILENT": 70,
    "CHARACTER.DEFECT": 75,
    "CHARACTER.NECROBINDER": 72,
    "CHARACTER.REGENT": 78,
    "CHARACTER.DEPRIVED": 68,
    "CHARACTER.WATCHER": 72,
    "CHARACTER.HERMIT": 74,
    # Mod 角色（初始默认无，需 mod 提供 player_template.json）
    "MOD.EXAMPLE_CHAR": 70,
}


# ============================================================
# 角色初始遗物
# ============================================================
CHARACTER_STARTER_RELICS: Dict[str, str] = {
    "CHARACTER.IRONCLAD": "RELIC.BURNING_BLOOD",
    "CHARACTER.SILENT": "RELIC.WHIP_LASH",
    "CHARACTER.DEFECT": "RELIC.CRACKED_CORE",
    "CHARACTER.NECROBINDER": "RELIC.NECRONOMICON",
    "CHARACTER.REGENT": "RELIC.TOME_OF_honor",
    "CHARACTER.DEPRIVED": "RELIC.NEW_POWER",
    "CHARACTER.WATCHER": "RELIC.PURE_WATER",
    "CHARACTER.HERMIT": "RELIC.BANDAGES",
    # Mod 角色（初始默认无）
    "MOD.EXAMPLE_CHAR": "",
}


# ============================================================
# 初始牌组数据（各角色）
# ============================================================
INITIAL_STARTER_DECKS: Dict[str, List[str]] = {
    "CHARACTER.IRONCLAD": [
        "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
        "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
        "CARD.STRIKE_IRONCLAD",
        "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
        "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
        "CARD.DEFEND_IRONCLAD",
        "CARD.BASH",
    ],
    "CHARACTER.SILENT": [
        "CARD.STRIKE_S", "CARD.STRIKE_S", "CARD.STRIKE_S",
        "CARD.STRIKE_S", "CARD.STRIKE_S",
        "CARD.DEFEND_S", "CARD.DEFEND_S", "CARD.DEFEND_S",
        "CARD.DEFEND_S", "CARD.DEFEND_S",
        "CARD.NEUTRALIZE",
    ],
    "CHARACTER.DEFECT": [
        "CARD.STRIKE_D", "CARD.STRIKE_D", "CARD.STRIKE_D",
        "CARD.STRIKE_D", "CARD.STRIKE_D",
        "CARD.DEFEND_D", "CARD.DEFEND_D", "CARD.DEFEND_D",
        "CARD.DEFEND_D", "CARD.DEFEND_D",
        "CARD.ZAP",
    ],
    "CHARACTER.NECROBINDER": [
        "CARD.STRIKE_N", "CARD.STRIKE_N", "CARD.STRIKE_N",
        "CARD.STRIKE_N", "CARD.STRIKE_N",
        "CARD.DEFEND_N", "CARD.DEFEND_N", "CARD.DEFEND_N",
        "CARD.DEFEND_N", "CARD.DEFEND_N",
        "CARD.SUMMON_SKELETON",
    ],
    "CHARACTER.REGENT": [
        "CARD.STRIKE_R", "CARD.STRIKE_R", "CARD.STRIKE_R",
        "CARD.STRIKE_R", "CARD.STRIKE_R",
        "CARD.DEFEND_R", "CARD.DEFEND_R", "CARD.DEFEND_R",
        "CARD.DEFEND_R", "CARD.DEFEND_R",
        "CARD.EVOLVE",
    ],
    "CHARACTER.DEPRIVED": [
        "CARD.STRIKE_D2", "CARD.STRIKE_D2", "CARD.STRIKE_D2",
        "CARD.STRIKE_D2", "CARD.STRIKE_D2",
        "CARD.DEFEND_D2", "CARD.DEFEND_D2", "CARD.DEFEND_D2",
        "CARD.DEFEND_D2", "CARD.DEFEND_D2",
        "CARD.CLOTHESLINE",
    ],
    "CHARACTER.WATCHER": [
        "CARD.STRIKE_P", "CARD.STRIKE_P", "CARD.STRIKE_P",
        "CARD.STRIKE_P", "CARD.DEFEND_P", "CARD.DEFEND_P",
        "CARD.DEFEND_P", "CARD.DEFEND_P",
        "CARD.VIGILANCE", "CARD.ERUPTION_P",
    ],
    "CHARACTER.HERMIT": [
        "CARD.STRIKE_H", "CARD.STRIKE_H", "CARD.STRIKE_H",
        "CARD.STRIKE_H", "CARD.STRIKE_H",
        "CARD.DEFEND_H", "CARD.DEFEND_H", "CARD.DEFEND_H",
        "CARD.DEFEND_H", "CARD.DEFEND_H",
        "CARD.SHRUG_IT_OFF",
    ],
    # Mod 角色（初始默认无牌组）
    "MOD.EXAMPLE_CHAR": [],
}


# ============================================================
# Mod 角色模板自动发现
# ============================================================
# 游戏 mods 目录（用于自动发现 mod 提供的主角）
DEFAULT_MODS_DIR = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods")


def load_mod_character_templates(mods_dir: Optional[Path] = None) -> Dict[str, Dict[str, Any]]:
    """
    扫描 mods 目录，从各 mod 文件夹的 player_template.json 加载角色模板。

    player_template.json 格式（放在 mod 文件夹根目录）：

        {
            "character_id": "MOD.MY_CHAR",
            "name": "我的角色",
            "max_hp": 75,
            "starter_relic": "RELIC.MY_CHAR_RELIC",
            "starter_deck": [
                "CARD.MY_STRIKE",
                "CARD.MY_STRIKE",
                "CARD.MY_STRIKE",
                "CARD.MY_DEFEND",
                "CARD.MY_DEFEND",
                "CARD.MY_DEFEND",
                "CARD.MY_SPECIAL"
            ]
        }

    所有字段均可选，不提供则使用默认值（无初始遗物、无初始牌组）。
    """
    templates: Dict[str, Dict[str, Any]] = {}

    # 修复：不传参时使用默认 mods 目录
    effective_mods_dir = mods_dir if mods_dir is not None else DEFAULT_MODS_DIR
    if not effective_mods_dir:
        print(f"  [提示] 未指定 mods 目录，跳过 mod 角色扫描")
        return templates
    if not effective_mods_dir.exists():
        print(f"  [提示] mods 目录不存在，跳过 mod 角色扫描: {effective_mods_dir}")
        print(f"  提示: 使用 --mods-dir 参数指定 mods 目录")
        return templates

    for mod_dir in effective_mods_dir.iterdir():
        if not mod_dir.is_dir():
            continue
        template_file = mod_dir / "player_template.json"
        if not template_file.exists():
            continue
        try:
            with open(template_file, "r", encoding="utf-8") as f:
                raw = json.load(f)
            if not isinstance(raw, dict):
                continue
            cid = raw.get("character_id")
            if not cid:
                print(f"  [警告] {template_file} 缺少 character_id 字段，跳过")
                continue

            # 注册到全局角色名
            name = raw.get("name") or cid
            if cid not in CHARACTER_NAMES:
                CHARACTER_NAMES[cid] = name

            # 注册初始遗物（无则留空字符串）
            if cid not in CHARACTER_STARTER_RELICS:
                CHARACTER_STARTER_RELICS[cid] = raw.get("starter_relic") or ""

            # 注册初始牌组（无则留空）
            deck_ids = raw.get("starter_deck") or []
            INITIAL_STARTER_DECKS[cid] = deck_ids

            # 注册最大HP
            if cid not in CHARACTER_MAX_HP:
                CHARACTER_MAX_HP[cid] = raw.get("max_hp") or 70

            templates[cid] = {
                "from_mod": mod_dir.name,
                "name": name,
                "max_hp": raw.get("max_hp") or 70,
                "starter_relic": raw.get("starter_relic") or "",
                "starter_deck": deck_ids,
            }
            print(f"  已加载 mod 角色模板: {cid}（来自 {mod_dir.name}）")
        except Exception as e:
            print(f"  [错误] 读取 {template_file} 失败: {e}")

    return templates


def get_max_hp(character_id: str) -> int:
    return CHARACTER_MAX_HP.get(character_id, 70)


# ============================================================
# Steam 名称映射
# ============================================================
STEAM_NAMES_FILE = "steam_names.json"


def load_steam_names(save_dir: Path) -> Dict[str, str]:
    """
    尝试从存档目录加载 steam_names.json。
    格式: {"76561198679823594": "煎包"}
    """
    mapping_file = save_dir / STEAM_NAMES_FILE
    if mapping_file.exists():
        try:
            with open(mapping_file, "r", encoding="utf-8") as f:
                raw = json.load(f)
            if isinstance(raw, dict):
                return {str(k): v for k, v in raw.items()}
        except Exception:
            pass
    return {}


def get_player_display(net_id: int, steam_names: Dict[str, str]) -> str:
    key = str(net_id)
    if key in steam_names and steam_names[key]:
        return f"{steam_names[key]} ({key})"
    return str(net_id)


# ============================================================
# 存档路径检测
# ============================================================
def find_save_paths() -> List[tuple]:
    """自动搜索 current_run_mp.save，返回 [(path, meta), ...]"""
    appdata = os.environ.get("APPDATA", os.path.expanduser("~"))
    sts2_root = Path(appdata) / "SlayTheSpire2" / "steam"
    if not sts2_root.exists():
        return []

    results = []
    for steam_dir in sts2_root.iterdir():
        if not steam_dir.is_dir():
            continue
        steam_id = steam_dir.name
        for profile_path in steam_dir.glob("profile*/saves/current_run_mp.save"):
            profile_name = profile_path.parent.parent.name
            results.append((profile_path, {
                "is_modded": False,
                "steam_id": steam_id,
                "profile_name": profile_name,
            }))
        for profile_path in steam_dir.glob("modded/profile*/saves/current_run_mp.save"):
            profile_name = profile_path.parent.parent.name
            results.append((profile_path, {
                "is_modded": True,
                "steam_id": steam_id,
                "profile_name": profile_name,
            }))
    return results


def get_save_summary(data: dict) -> dict:
    """从存档提取摘要"""
    ascension = data.get("ascension", 0)
    current_act = data.get("current_act_index", 0)
    players = data.get("players", [])

    player_infos = []
    for p in players:
        char_id = p.get("character_id", "")
        net_id = p.get("net_id", "?")
        gold = p.get("gold", 0)
        current_hp = p.get("current_hp", 0)
        max_hp = p.get("max_hp", 0)
        player_infos.append({
            "role": get_character_display(char_id),
            "net_id": net_id,
            "gold": gold,
            "hp": f"{current_hp}/{max_hp}",
            "deck_count": len(p.get("deck", [])),
            "relic_count": len(p.get("relics", [])),
        })
    return {
        "ascension": ascension,
        "act": current_act + 1,
        "players": player_infos,
    }


# ============================================================
# 存档读写
# ============================================================
def load_save(path: Path) -> dict:
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_save(path: Path, data: dict) -> None:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def backup_save(path: Path) -> Path:
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = path.with_suffix(f".save.backup.{ts}")
    shutil.copy2(path, backup_path)
    return backup_path


# ============================================================
# 核心修改逻辑
# ============================================================
def inject_player_into_map_history(data: dict, player: dict) -> None:
    """
    将新玩家注入到 map_point_history 的所有已有记录中。
    这样游戏在 LoadIntoLatestMapCoord 时能查到该玩家的 stats，不会崩溃。
    """
    new_id = player.get("net_id")
    if not new_id:
        return

    for floor in data.get("map_point_history", []):
        for entry in floor:
            stats = entry.get("player_stats", [])
            # 已存在则跳过
            if any(s.get("player_id") == new_id for s in stats):
                continue

            # 构建该玩家的初始记录
            new_stat: Dict[str, Any] = {
                "player_id": new_id,
                "current_gold": player.get("gold", 0),
                "current_hp": player.get("current_hp", player.get("max_hp", 0)),
                "max_hp": player.get("max_hp", 0),
                "damage_taken": 0,
                "gold_gained": 0,
                "gold_lost": 0,
                "gold_spent": 0,
                "gold_stolen": 0,
                "hp_healed": player.get("max_hp", 0),
                "max_hp_gained": 0,
                "max_hp_lost": 0,
                "cards_gained": [],
                "relic_choices": [],
                "event_choices": [],
            }

            # ancient 类型节点有 ancient_choice
            if entry.get("map_point_type") == "ancient":
                new_stat["ancient_choice"] = []

            stats.append(new_stat)


def remove_players_from_save(data: dict, remove_net_ids: Set) -> tuple:
    """移除玩家及相关引用"""
    original_count = len(data.get("players", []))

    # 1. players 列表
    data["players"] = [
        p for p in data.get("players", [])
        if p.get("net_id") not in remove_net_ids
    ]

    # 2. map_point_history 中的 player_stats
    for floor in data.get("map_point_history", []):
        for entry in floor:
            if "player_stats" in entry:
                entry["player_stats"] = [
                    s for s in entry["player_stats"]
                    if s.get("player_id") not in remove_net_ids
                ]

    # 3. map_drawings (Base64 + Gzip)
    map_drawings_ok = False
    if data.get("map_drawings"):
        try:
            raw = base64.b64decode(data["map_drawings"])
            decompressed = gzip.decompress(raw)
            md = json.loads(decompressed.decode("utf-8"))
            if "drawings" in md:
                md["drawings"] = [
                    d for d in md["drawings"]
                    if d.get("playerId") not in remove_net_ids
                ]
                new_json = json.dumps(md, ensure_ascii=False).encode("utf-8")
                data["map_drawings"] = base64.b64encode(
                    gzip.compress(new_json)
                ).decode("ascii")
                map_drawings_ok = True
        except Exception:
            pass

    removed_count = original_count - len(data.get("players", []))
    return data, map_drawings_ok, removed_count


def possess_player(data: dict, old_net_id: int, new_steam_id: str,
                   new_steam_name: Optional[str]) -> bool:
    """
    夺舍：将 old_net_id 玩家的身份替换为新的 Steam ID。
    返回是否成功。
    """
    for p in data.get("players", []):
        if p.get("net_id") == old_net_id:
            # 替换 net_id（数值形式的 Steam ID）
            try:
                p["net_id"] = int(new_steam_id)
            except ValueError:
                print(f"  错误: 无效的 Steam ID: {new_steam_id}")
                return False

            # 更新 Steam ID 字段（字符串形式）
            p["steam_id"] = new_steam_id

            # 更新 display_data
            if "display_data" not in p:
                p["display_data"] = {}
            if new_steam_name:
                p["display_data"]["steam_name"] = new_steam_name
                p["display_data"]["player_name"] = new_steam_name
            else:
                # 清除旧昵称
                p["display_data"].pop("steam_name", None)

            return True
    return False


def add_player_copy(
    data: dict,
    source_net_id: int,
    new_steam_id: str,
    new_steam_name: Optional[str],
    new_character_id: Optional[str],
) -> bool:
    """
    复制模式添加玩家：
    - 从 source_net_id 复制 deck/relics/gold/rng/odds/grab_bag
    - 以满血状态加入（current_hp = max_hp）
    - 可选更换角色（new_character_id）
    """
    # 找到源玩家
    source = None
    for p in data.get("players", []):
        if p.get("net_id") == source_net_id:
            source = p
            break
    if not source:
        print("  错误: 未找到源玩家")
        return False

    try:
        new_net_id = int(new_steam_id)
    except ValueError:
        print(f"  错误: 无效的 Steam ID: {new_steam_id}")
        return False

    # 深拷贝源玩家数据
    new_player = copy.deepcopy(source)

    # 替换身份
    new_player["net_id"] = new_net_id
    new_player["steam_id"] = new_steam_id

    # 满血
    new_player["current_hp"] = new_player["max_hp"]

    # 清空药水
    new_player["potions"] = []

    # 如果指定了新角色，更换角色
    if new_character_id and new_character_id != source.get("character_id"):
        new_player["character_id"] = new_character_id
        # 更换角色时：遗物清空（需重新获取），discovered_relics 保留已有
        new_player["relics"] = []
        if "discovered_relics" in new_player:
            # discovered_relics 保留，但移除当前遗物
            pass  # keep existing discovered_relics

    # 更新 display_data
    if "display_data" not in new_player:
        new_player["display_data"] = {}
    if new_steam_name:
        new_player["display_data"]["steam_name"] = new_steam_name
        new_player["display_data"]["player_name"] = new_steam_name
    else:
        new_player["display_data"].pop("steam_name", None)

    # 确保 discovered_* 字段存在（兼容某些存档可能缺失的情况）
    if "discovered_cards" not in new_player:
        new_player["discovered_cards"] = []
    if "discovered_relics" not in new_player:
        new_player["discovered_relics"] = []
    if "discovered_potions" not in new_player:
        new_player["discovered_potions"] = []

    # 分配未使用的 net_id（避免与现有冲突）
    existing_ids = {p.get("net_id") for p in data.get("players", [])}
    while new_player["net_id"] in existing_ids:
        new_player["net_id"] += 1

    data["players"].append(new_player)

    # 注入 map_point_history（防止进游戏后崩溃）
    inject_player_into_map_history(data, new_player)

    return True


def add_player_starter(
    data: dict,
    new_steam_id: str,
    new_steam_name: Optional[str],
    character_id: str,
) -> bool:
    """
    初始牌组模式添加玩家：
    - 以选定角色的初始状态加入
    - 基础牌组 + 初始遗物 + 100金币 + 满血
    """
    try:
        new_net_id = int(new_steam_id)
    except ValueError:
        print(f"  错误: 无效的 Steam ID: {new_steam_id}")
        return False

    max_hp = get_max_hp(character_id)

    starter_relic_id = CHARACTER_STARTER_RELICS.get(character_id, "RELIC.BURNING_BLOOD")

    new_player: Dict[str, Any] = {
        "net_id": new_net_id,
        "steam_id": new_steam_id,
        "character_id": character_id,
        "current_hp": max_hp,
        "max_hp": max_hp,
        "max_energy": 3,
        "base_orb_slot_count": 0,
        "max_potion_slot_count": 3,
        "gold": 100,
        "deck": get_initial_deck(character_id),
        "relics": [
            {"id": starter_relic_id, "floor_added_to_deck": 1}
        ],
        "potions": [],
        "odds": {"card_rarity_odds_value": 0.0, "potion_reward_odds_value": 0.0},
        "rng": {"counters": {"rewards": 0, "shops": 0, "transformations": 0}, "seed": 0},
        "extra_fields": {},
        "unlock_state": {
            "encounters_seen": [],
            "number_of_runs": 0,
            "unlocked_epochs": [],
        },
        "discovered_relics": [starter_relic_id],
        "discovered_cards": [],
        "discovered_potions": [],
        "relic_grab_bag": {
            "relic_id_lists": {
                "common": ["RELIC.STRAWBERRY"],
                "uncommon": [],
                "rare": [],
                "shop": [],
            }
        },
    }

    # 更新 display_data
    new_player["display_data"] = {}
    if new_steam_name:
        new_player["display_data"]["steam_name"] = new_steam_name
        new_player["display_data"]["player_name"] = new_steam_name

    # 分配未使用的 net_id
    existing_ids = {p.get("net_id") for p in data.get("players", [])}
    while new_player["net_id"] in existing_ids:
        new_player["net_id"] += 1

    data["players"].append(new_player)

    # 注入 map_point_history（防止进游戏后崩溃）
    inject_player_into_map_history(data, new_player)

    return True


# ============================================================
# UI 辅助
# ============================================================
def print_header():
    print()
    print("=" * 58)
    print("  STS2 多人存档玩家管理工具  v1.0.0")
    print("=" * 58)


def print_steam_names_hint(save_dir: Path):
    hint_file = save_dir / STEAM_NAMES_FILE
    print(f"\n[提示] 如需显示 Steam 昵称，在存档目录创建映射文件：")
    print(f"  路径: {hint_file}")
    print(f'  格式: {{"76561198679823594": "煎包", "76561199032167696": "小明"}}')


def select_save_path(found: List[tuple]) -> Optional[Path]:
    """交互式选择存档路径"""
    if not found:
        print("\n未找到 current_run_mp.save，请手动输入存档目录：")
        print("  示例: C:\\Users\\xxx\\AppData\\Roaming\\SlayTheSpire2\\steam\\76561xxx\\modded\\profile1\\saves")
        raw = input("\n> ").strip().strip('"')
        if not raw:
            return None
        p = Path(raw)
        if p.is_file():
            return p
        elif (p / "current_run_mp.save").exists():
            return p / "current_run_mp.save"
        else:
            print("路径无效或文件不存在")
            return None

    if len(found) == 1:
        return found[0][0]

    print("\n找到多份存档，请选择：")
    summaries = []
    for i, (path, meta) in enumerate(found, 1):
        try:
            d = load_save(path)
            sm = get_save_summary(d)
            summaries.append((path, meta, sm))
        except Exception:
            summaries.append((path, meta, None))

    for i, (path, meta, sm) in enumerate(summaries, 1):
        mode_str = "模组" if meta["is_modded"] else "原版"
        steam_short = meta["steam_id"][:8] + "..."
        if sm:
            ps = ", ".join(
                f"{x['role']}({x['net_id']})" for x in sm["players"]
            )
            info = f"进阶{sm['ascension']} 第{sm['act']}幕 | {ps}"
        else:
            info = "(读取失败)"
        print(f"  [{i}] {mode_str} | Steam {steam_short} | {meta['profile_name']} | {info}")
        print(f"      路径: {path}")

    try:
        idx = int(input("\n请输入序号 > "))
        if 1 <= idx <= len(found):
            return found[idx - 1][0]
        print("无效选择")
        return None
    except ValueError:
        print("无效输入")
        return None


def display_players_for_choice(players: List[dict], steam_names: Dict[str, str]):
    """显示玩家列表供选择（带序号标注）"""
    print(f"\n  {'[序号]':<6} {'角色':<8} {'HP':<10} {'金币':<6} {'牌组':<4} {'遗物':<4} Steam ID / 名称")
    print(f"  {'-'*6} {'-'*8} {'-'*10} {'-'*6} {'-'*4} {'-'*4} {'-'*30}")
    for i, p in enumerate(players, 1):
        char_id = p.get("character_id", "")
        net_id = p.get("net_id", "?")
        current_hp = p.get("current_hp", 0)
        max_hp = p.get("max_hp", 0)
        gold = p.get("gold", 0)
        deck_count = len(p.get("deck", []))
        relic_count = len(p.get("relics", []))
        display = get_player_display(net_id, steam_names)
        name = get_character_display(char_id)
        hp_str = f"{current_hp}/{max_hp}"
        print(f"  [{i:>2}]   {name:<8} {hp_str:<10} {gold:<6} {deck_count:<4} {relic_count:<4} {display}")


def display_players(players: List[dict], steam_names: Dict[str, str]):
    """显示玩家列表（纯展示，不需要输入时用）"""
    print(f"\n  {'[序号]':<6} {'角色':<8} {'HP':<10} {'金币':<6} {'牌组':<4} {'遗物':<4} Steam ID / 名称")
    print(f"  {'-'*6} {'-'*8} {'-'*10} {'-'*6} {'-'*4} {'-'*4} {'-'*30}")
    for i, p in enumerate(players, 1):
        char_id = p.get("character_id", "")
        net_id = p.get("net_id", "?")
        current_hp = p.get("current_hp", 0)
        max_hp = p.get("max_hp", 0)
        gold = p.get("gold", 0)
        deck_count = len(p.get("deck", []))
        relic_count = len(p.get("relics", []))
        display = get_player_display(net_id, steam_names)
        name = get_character_display(char_id)
        hp_str = f"{current_hp}/{max_hp}"
        print(f"  [{i:>2}]   {name:<8} {hp_str:<10} {gold:<6} {deck_count:<4} {relic_count:<4} {display}")


def input_steam_id(prompt_msg: str) -> tuple:
    """输入 Steam ID 和昵称，返回 (steam_id, steam_name)"""
    print(prompt_msg)
    steam_id = input("  Steam64位ID > ").strip()
    if not steam_id:
        return "", ""
    steam_name = input("  Steam昵称（留空则不填）> ").strip()
    return steam_id, steam_name


# ============================================================
# 子操作
# ============================================================
def op_possess(data: dict, save_path: Path, steam_names: Dict[str, str]):
    """夺舍操作"""
    players = data.get("players", [])
    if not players:
        print("没有玩家可夺舍")
        return

    display_players_for_choice(players, steam_names)

    print("\n--- 夺舍玩家 ---")
    print("输入要夺舍的玩家序号 > ", end="")
    try:
        idx = int(input().strip())
    except ValueError:
        print("输入无效")
        return
    if idx < 1 or idx > len(players):
        print("序号无效")
        return

    target = players[idx - 1]
    old_net_id = target.get("net_id")
    old_char = get_character_display(target.get("character_id", ""))

    print("\n输入接替者信息：")
    print("  Steam64位ID 格式: 76561198679823594")
    steam_id = input("  > Steam64位ID: ").strip()
    if not steam_id:
        print("已取消")
        return
    steam_name = input("  > Steam昵称（留空跳过）: ").strip()

    # 夺舍预览确认
    print()
    print("=" * 48)
    print("  夺舍确认")
    print("=" * 48)
    print(f"  离线玩家: {old_char} (ID: {old_net_id})")
    print(f"  接替者:   {steam_name or '(无昵称)'} (ID: {steam_id})")
    print("=" * 48)
    confirm = input("\n确认执行夺舍？(y/n) > ").strip().lower()
    if confirm not in ("y", "yes"):
        print("已取消")
        return

    # 备份
    backup_path = backup_save(save_path)
    print(f"\n已备份: {backup_path}")

    # 修改
    if possess_player(data, old_net_id, steam_id, steam_name or None):
        save_save(save_path, data)
        print(f"\n夺舍成功！{old_char} 已由 {steam_name or steam_id} 接管。")
        print("请接替者重新进入游戏房间。")
    else:
        print("\n夺舍失败")


def op_add_player(data: dict, save_path: Path, steam_names: Dict[str, str],
                  mods_dir_arg: Optional[Path] = None):
    """添加新玩家操作"""
    players = data.get("players", [])

    print("\n--- 添加新玩家 ---")
    print("选择加入方式：")
    print("  [1] 复制模式 — 继承某玩家的牌组/遗物/金币（满血加入）")
    print("  [2] 初始牌组 — 以选定角色的初始状态加入（基础牌组+100金币）")
    print("  [0] 取消")
    print("> ", end="")
    choice = input().strip()

    if choice == "0":
        return

    # ---- 新玩家基础信息 ----
    print("\n输入新玩家信息：")
    print("  Steam64位ID 格式: 76561198679823594")
    steam_id = input("  > Steam64位ID: ").strip()
    if not steam_id:
        print("已取消")
        return
    steam_name = input("  > Steam昵称（留空跳过）: ").strip()

    # ---- 复制模式 ----
    if choice == "1":
        if not players:
            print("没有现有玩家可供复制，请使用 [2] 初始牌组模式")
            return

        display_players_for_choice(players, steam_names)
        print("\n选择复制来源玩家（将被复制的内容：牌组/遗物/金币/随机数）")
        print("> ", end="")
        try:
            src_idx = int(input().strip())
        except ValueError:
            print("无效输入")
            return
        if src_idx < 1 or src_idx > len(players):
            print("序号无效")
            return

        src_player = players[src_idx - 1]
        src_char = get_character_display(src_player.get("character_id", ""))
        src_gold = src_player.get("gold", 0)
        src_deck_count = len(src_player.get("deck", []))
        src_relic_count = len(src_player.get("relics", []))

        # 角色更换
        print(f"\n是否更换角色？直接回车保持 [{src_idx}] {src_char}")
        print("  输入新角色编号或名称，如: 1 / 铁甲战士 / IRONCLAD")
        print("  可选角色:")
        char_list = list(CHARACTER_NAMES.items())
        for ci, (cid, cname) in enumerate(char_list, 1):
            marker = " ← 当前" if cid == src_player.get("character_id") else ""
            print(f"    [{ci}] {cid} -> {cname}{marker}")
        new_char_input = input("  > ").strip()
        new_character_id = None
        if new_char_input:
            matched = None
            try:
                ci = int(new_char_input)
                if 1 <= ci <= len(char_list):
                    matched = char_list[ci - 1][0]
            except ValueError:
                for cid, cname in char_list:
                    if new_char_input.lower() in cname.lower() or new_char_input.lower() in cid.lower():
                        matched = cid
                        break
            if matched:
                new_character_id = matched
                print(f"  将更换为: {CHARACTER_NAMES[matched]}")
            else:
                print("  未识别，保持原角色")

        new_char_name = (
            CHARACTER_NAMES.get(new_character_id, src_char)
            if new_character_id
            else src_char
        )

        # 复制模式下初始遗物说明
        src_relic_ids = [r.get("id", "") for r in src_player.get("relics", [])]
        relic_desc = ", ".join(src_relic_ids) if src_relic_ids else "无"

        # 更换角色时的遗物说明
        if new_character_id and new_character_id != src_player.get("character_id"):
            new_starter_relic = CHARACTER_STARTER_RELICS.get(new_character_id, "RELIC.BURNING_BLOOD")
            relic_desc = f"无（需重新获取）← {CHARACTER_NAMES.get(new_character_id)} 初始遗物: {new_starter_relic}"

        # 添加预览确认
        print()
        print("=" * 48)
        print("  添加新玩家确认 — 复制模式")
        print("=" * 48)
        print(f"  新玩家:   {steam_name or '(无昵称)'} (ID: {steam_id})")
        print(f"  角色:     {new_char_name}（复制自 [{src_idx}] {src_char}）")
        print(f"  牌组:     {src_deck_count} 张（不变）")
        print(f"  遗物:     {relic_desc}")
        print(f"  金币:     {src_gold}（不变）")
        print(f"  生命:     满血加入（current_hp = max_hp）")
        print("=" * 48)
        confirm = input("\n确认添加？(y/n) > ").strip().lower()
        if confirm not in ("y", "yes"):
            print("已取消")
            return

        backup_path = backup_save(save_path)
        print(f"\n已备份: {backup_path}")

        if add_player_copy(data, src_player.get("net_id"), steam_id,
                            steam_name or None, new_character_id):
            save_save(save_path, data)
            print(f"\n添加成功！{steam_name or steam_id} 已加入（复制模式）。")
            print("请新玩家重新进入游戏房间。")
        else:
            print("\n添加失败")

    # ---- 初始牌组模式 ----
    elif choice == "2":
        mod_templates = load_mod_character_templates(mods_dir_arg)
        mod_count = len(mod_templates)

        # 内置角色：铁甲战士、静默猎手、故障机器人、亡灵契约师、储君
        base_chars = [
            ("CHARACTER.IRONCLAD", "铁甲战士"),
            ("CHARACTER.SILENT", "静默猎手"),
            ("CHARACTER.DEFECT", "故障机器人"),
            ("CHARACTER.NECROBINDER", "亡灵契约师"),
            ("CHARACTER.REGENT", "储君"),
        ]

        print("\n选择角色（输入序号）：")
        print()
        print("  ── 内置角色 ──")
        for ci, (cid, cname) in enumerate(base_chars, 1):
            marker = " ← 默认" if cid == "CHARACTER.WATCHER" else ""
            print(f"  [{ci}] {cname}  ({cid}){marker}")

        if mod_templates:
            print()
            print(f"  ── Mod 角色（{mod_count} 个）──")
            for ci, (cid, info) in enumerate(sorted(mod_templates.items()), len(base_chars) + 1):
                print(f"  [{ci}] {info['name']}  ({cid})  ← {info['from_mod']}")
            print()
            print("  Mod 作者想注册新角色？")
            print("  → https://github.com/Jianbao233/STS2_mod  (README.md → Mod 角色接口)")
        else:
            print()
            print("  ── Mod 角色 ──")
            print("  （当前无 mod 提供角色模板，未检测到 player_template.json）")
            print()
            print("  ┌─────────────────────────────────────────────┐")
            print("  │  Mod 作者？让你的角色出现在这里！             │")
            print("  └─────────────────────────────────────────────┘")
            print()
            print("  只需两步即可自动注册你的自定义角色：")
            print()
            print("  [1] 在你的 mod 文件夹根目录新建 player_template.json")
            print("      格式示例：")
            print()
            print('      {')
            print('          "character_id": "MOD.MY_CHAR",')
            print('          "name": "我的角色",')
            print('          "max_hp": 75,')
            print('          "starter_relic": "RELIC.MY_CHAR_RELIC",')
            print('          "starter_deck": [')
            print('              "CARD.STRIKE_XXX", "CARD.STRIKE_XXX",')
            print('              "CARD.DEFEND_XXX", "CARD.DEFEND_XXX",')
            print('              "CARD.MY_SPECIAL"')
            print('          ]')
            print('      }')
            print()
            print("  [2] 重启本工具，角色会自动出现在上方列表")
            print()
            print("  完整文档（含字段说明）：")
            print("  → https://github.com/Jianbao233/STS2_mod")
            print("  → README.md → Mod 角色接口（Mod 作者指南）")
            print()
            print("  注意：character_id 必须以 MOD. 开头，如 MOD.MY_CHAR")
        print("  [0] 取消")
        print("> ", end="")
        char_input = input().strip()

        if char_input == "0":
            return

        matched = None

        # 先尝试序号匹配
        try:
            ci = int(char_input)
            if 1 <= ci <= len(base_chars):
                matched = base_chars[ci - 1][0]
            elif mod_templates and len(base_chars) < ci <= len(base_chars) + len(mod_templates):
                sorted_mods = sorted(mod_templates.items())
                matched = sorted_mods[ci - len(base_chars) - 1][0]
        except ValueError:
            pass

        # 再尝试关键字匹配
        if not matched:
            char_input_lower = char_input.lower()
            for cid, cname in base_chars:
                if char_input_lower in cname.lower() or char_input_lower in cid.lower():
                    matched = cid
                    break
            if not matched:
                for cid, info in mod_templates.items():
                    if char_input_lower in info["name"].lower() or char_input_lower in cid.lower():
                        matched = cid
                        break

        if not matched:
            print("未识别角色，默认为观者（WATCHER）")
            matched = "CHARACTER.WATCHER"

        char_name = CHARACTER_NAMES.get(matched, matched)
        max_hp = get_max_hp(matched)
        deck_cards = get_initial_deck(matched)
        starter_relic = CHARACTER_STARTER_RELICS.get(matched, "")

        # 添加预览确认
        print()
        print("=" * 48)
        print("  添加新玩家确认 — 初始牌组模式")
        print("=" * 48)
        print(f"  新玩家:   {steam_name or '(无昵称)'} (ID: {steam_id})")
        print(f"  角色:     {char_name} ({matched})")
        print(f"  初始牌组: {len(deck_cards)} 张")
        relic_display = starter_relic if starter_relic else "（无）"
        print(f"  初始遗物: {relic_display}")
        print(f"  初始金币: 100")
        print(f"  生命值:   {max_hp}/{max_hp}（满血）")
        print("=" * 48)
        confirm = input("\n确认添加？(y/n) > ").strip().lower()
        if confirm not in ("y", "yes"):
            print("已取消")
            return

        backup_path = backup_save(save_path)
        print(f"\n已备份: {backup_path}")

        if add_player_starter(data, steam_id, steam_name or None, matched):
            save_save(save_path, data)
            print(f"\n添加成功！{steam_name or steam_id} 以 {char_name} 初始状态加入。")
            print("请新玩家重新进入游戏房间。")
        else:
            print("\n添加失败")
    else:
        print("无效选择")


def op_remove_players(data: dict, save_path: Path, steam_names: Dict[str, str]):
    """移除玩家操作"""
    players = data.get("players", [])
    if not players:
        print("没有玩家可移除")
        return

    display_players_for_choice(players, steam_names)

    print("\n--- 移除玩家 ---")
    print("输入要移除的玩家序号，逗号分隔，如: 1,3")
    print("  输入 all = 移除全部 | 留空 = 取消")
    print("> ", end="")
    raw = input().strip()

    if not raw:
        print("已取消")
        return

    if raw.lower() == "all":
        remove_net_ids = {p.get("net_id") for p in players if p.get("net_id")}
        print(f"\n将移除全部 {len(remove_net_ids)} 名玩家！")
    else:
        try:
            indices = [int(x.strip()) for x in raw.split(",") if x.strip()]
        except ValueError:
            print("输入格式错误")
            return

        idx_to_net_id = {i: p.get("net_id") for i, p in enumerate(players, 1) if p.get("net_id")}
        remove_net_ids = set()
        for idx in indices:
            if idx in idx_to_net_id:
                remove_net_ids.add(idx_to_net_id[idx])
            else:
                print(f"  忽略无效序号: {idx}")

    if not remove_net_ids:
        print("没有有效的移除目标")
        return

    # 确认
    print("\n以下玩家将被移除：")
    for p in players:
        if p.get("net_id") in remove_net_ids:
            char_id = p.get("character_id", "")
            display = get_player_display(p.get("net_id", ""), steam_names)
            print(f"  × {get_character_display(char_id)} | {display}")

    remaining = len(players) - len(remove_net_ids)
    confirm = input(f"\n确认移除 {len(remove_net_ids)} 名玩家？剩余 {remaining} 名 (y/n) > ").strip().lower()
    if confirm not in ("y", "yes", "是"):
        print("已取消")
        return

    backup_path = backup_save(save_path)
    print(f"\n已备份: {backup_path}")

    data, map_drawings_ok, removed_count = remove_players_from_save(data, remove_net_ids)
    if not map_drawings_ok and data.get("map_drawings"):
        print("  提示: map_drawings 未成功处理（通常不影响游戏）")

    save_save(save_path, data)
    print(f"\n完成！已移除 {removed_count} 名玩家。")
    print("可启动游戏继续游玩。")


# ============================================================
# 主入口
# ============================================================
def main() -> None:
    parser = argparse.ArgumentParser(description="STS2 多人存档玩家管理工具")
    parser.add_argument("--path", "-p", help="指定存档路径")
    parser.add_argument("--list-only", "-l", action="store_true", help="仅列出玩家")
    parser.add_argument("--mods-dir", "-m", help="指定 mods 目录（默认: K:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\mods）")
    args = parser.parse_args()

    # 解析 mods 目录
    mods_dir_arg: Optional[Path] = None
    if args.mods_dir:
        mods_dir_arg = Path(args.mods_dir.strip().strip('"'))
        if not mods_dir_arg.exists():
            print(f"\n[警告] 指定的 mods 目录不存在: {mods_dir_arg}")
            print("  Mod 角色模板将无法加载！")


    print_header()

    # 选择存档路径
    save_path = None
    if args.path:
        p = Path(args.path.strip().strip('"'))
        if p.is_file():
            save_path = p
        elif (p / "current_run_mp.save").exists():
            save_path = p / "current_run_mp.save"
        if save_path:
            print(f"\n使用指定路径: {save_path}")

    if not save_path:
        found = find_save_paths()
        save_path = select_save_path(found)

    if not save_path or not save_path.exists():
        print("未找到存档文件")
        return

    # 读取存档
    try:
        data = load_save(save_path)
    except json.JSONDecodeError as e:
        print(f"\n存档解析失败: {e}")
        print("建议从备份恢复 (current_run_mp.save.backup.*)")
        return
    except Exception as e:
        print(f"\n读取失败: {e}")
        return

    players = data.get("players", [])
    if not players:
        print("\n存档中没有玩家数据，可能不是有效的多人存档")
        return

    save_dir = save_path.parent
    steam_names = load_steam_names(save_dir)

    # 显示存档信息
    sm = get_save_summary(data)
    print(f"\n--- 存档信息 ---")
    print(f"  进阶等级: {sm['ascension']} | 第 {sm['act']} 幕 | {len(players)} 名玩家")
    print(f"  路径: {save_path}")
    print_steam_names_hint(save_dir)
    display_players(players, steam_names)

    if args.list_only:
        print("\n(--list-only 模式，仅列出，未修改)")
        return

    # 主菜单循环
    while True:
        print("\n--- 操作 ---")
        print("  [1] 夺舍玩家 — 接管离线玩家的存档继续游戏")
        print("  [2] 添加新玩家 — 复制模式或初始牌组加入")
        print("  [3] 移除玩家 — 清理离线玩家的存档数据")
        print("  [0] 退出（不修改）")
        print("> ", end="")
        choice = input().strip()

        if choice == "0":
            print("已退出，未修改存档")
            return
        elif choice == "1":
            # 重新加载（因为可能之前操作过）
            try:
                data = load_save(save_path)
            except Exception:
                pass
            steam_names = load_steam_names(save_dir)
            op_possess(data, save_path, steam_names)
        elif choice == "2":
            try:
                data = load_save(save_path)
            except Exception:
                pass
            steam_names = load_steam_names(save_dir)
            op_add_player(data, save_path, steam_names, mods_dir_arg)
        elif choice == "3":
            try:
                data = load_save(save_path)
            except Exception:
                pass
            steam_names = load_steam_names(save_dir)
            op_remove_players(data, save_path, steam_names)
        else:
            print("无效选择，请输入 0~3")


if __name__ == "__main__":
    main()
