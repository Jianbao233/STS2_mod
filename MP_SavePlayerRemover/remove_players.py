#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
STS2 多人存档移除玩家工具
在游戏外修改 current_run_mp.save，移除指定玩家并清理相关引用。
"""

import argparse
import base64
import gzip
import json
import os
import shutil
from datetime import datetime
from pathlib import Path
from typing import List, Set

# ============ 角色名映射 (character_id -> 中文) ============
CHARACTER_NAMES = {
    "CHARACTER.NECROBINDER": "亡灵契约师",
    "CHARACTER.IRONCLAD": "战士",
    "CHARACTER.DEFECT": "机器人",
    "CHARACTER.HERMIT": "隐者",
    "CHARACTER.SILENT": "猎人",
    "CHARACTER.WATCHER": "观者",
    "CHARACTER.DEPRIVED": "剥夺者",
    "CHARACTER.REGENT": "储君",
}


def get_character_display(char_id: str) -> str:
    """从 character_id 获取显示名，未知则返回原始 ID 的简称"""
    if char_id in CHARACTER_NAMES:
        return CHARACTER_NAMES[char_id]
    # CHARACTER.XXX -> XXX
    if char_id and char_id.startswith("CHARACTER."):
        return char_id.replace("CHARACTER.", "")
    return char_id or "未知"


def find_save_paths() -> List[tuple]:
    """
    自动搜索 current_run_mp.save 路径。
    返回 [(path, meta), ...]，meta 含: is_modded, steam_id, profile_name
    区分：原版模式(profile*/saves/) vs 模组模式(modded/profile*/saves/)
    支持多 Steam 账号(每个 steam_id 为不同账号)
    """
    appdata = os.environ.get("APPDATA", os.path.expanduser("~"))
    sts2_root = Path(appdata) / "SlayTheSpire2" / "steam"
    if not sts2_root.exists():
        return []

    results = []
    for steam_dir in sts2_root.iterdir():
        if not steam_dir.is_dir():
            continue
        steam_id = steam_dir.name
        # 原版模式: steam/{SteamId}/profile1/saves/
        for profile_path in steam_dir.glob("profile*/saves/current_run_mp.save"):
            profile_name = profile_path.parent.parent.name
            results.append((profile_path, {
                "is_modded": False,
                "steam_id": steam_id,
                "profile_name": profile_name,
            }))
        # 模组模式: steam/{SteamId}/modded/profile1/saves/
        for profile_path in steam_dir.glob("modded/profile*/saves/current_run_mp.save"):
            profile_name = profile_path.parent.parent.name
            results.append((profile_path, {
                "is_modded": True,
                "steam_id": steam_id,
                "profile_name": profile_name,
            }))
    return results


def get_save_summary(data: dict) -> dict:
    """
    从存档数据提取摘要，用于选择时展示。
    含：难度(ascension)、幕数、层数、玩家(角色+Steam64位ID)
    注：存档内无 Steam 昵称，仅能显示 64 位 ID
    """
    ascension = data.get("ascension", 0)
    current_act = data.get("current_act_index", 0)
    mph = data.get("map_point_history", [])
    floor_count = len(mph)
    players = data.get("players", [])
    player_infos = []
    for p in players:
        char_id = p.get("character_id", "")
        net_id = p.get("net_id")
        name = get_character_display(char_id)
        player_infos.append({"role": name, "net_id": net_id})
    return {
        "ascension": ascension,
        "act": current_act + 1,
        "floor_count": floor_count,
        "players": player_infos,
    }


def load_save(path: Path) -> dict:
    """读取存档 JSON，UTF-8"""
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_save(path: Path, data: dict) -> None:
    """写回存档 JSON，UTF-8"""
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def backup_save(path: Path) -> Path:
    """备份存档为 current_run_mp.save.backup.{timestamp}"""
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = path.with_suffix(f".save.backup.{ts}")
    shutil.copy2(path, backup_path)
    return backup_path


def remove_players_from_save(data: dict, keep_net_ids: Set) -> tuple:
    """
    移除存档中不在 keep_net_ids 的玩家及相关引用。
    返回 (修改后的 data, map_drawings 是否成功处理)
    """
    # 1. players
    original_count = len(data.get("players", []))
    data["players"] = [p for p in data.get("players", []) if p.get("net_id") in keep_net_ids]

    # 2. map_point_history
    for floor in data.get("map_point_history", []):
        for entry in floor:
            if "player_stats" in entry:
                entry["player_stats"] = [
                    s for s in entry["player_stats"]
                    if s.get("player_id") in keep_net_ids
                ]

    # 3. map_drawings (Base64+Gzip 编码)
    map_drawings_ok = False
    if "map_drawings" in data and data["map_drawings"]:
        try:
            raw = base64.b64decode(data["map_drawings"])
            decompressed = gzip.decompress(raw)
            md = json.loads(decompressed.decode("utf-8"))
            if "drawings" in md:
                md["drawings"] = [
                    d for d in md["drawings"]
                    if d.get("playerId") in keep_net_ids
                ]
                new_json = json.dumps(md, ensure_ascii=False).encode("utf-8")
                data["map_drawings"] = base64.b64encode(gzip.compress(new_json)).decode("ascii")
                map_drawings_ok = True
        except Exception:
            pass

    return data, map_drawings_ok


def main() -> None:
    parser = argparse.ArgumentParser(description="STS2 多人存档 - 移除玩家工具")
    parser.add_argument("--path", "-p", help="指定存档路径 (文件或所在目录)")
    parser.add_argument("--list-only", "-l", action="store_true", help="仅列出玩家，不修改")
    args = parser.parse_args()

    print("=" * 50)
    print("  STS2 多人存档 - 移除玩家工具")
    print("=" * 50)

    # 解析路径参数
    save_path = None
    if args.path:
        p = Path(args.path.strip().strip('"'))
        if p.is_file():
            save_path = p
        elif (p / "current_run_mp.save").exists():
            save_path = p / "current_run_mp.save"
        if save_path:
            print(f"\n使用指定路径: {save_path}")

    # 搜索存档（未指定路径时）
    if not save_path:
        found = find_save_paths()
        if not found:
            print("\n未找到 current_run_mp.save，请手动输入路径：")
            print("  示例: C:\\Users\\xxx\\AppData\\Roaming\\SlayTheSpire2\\steam\\76561xxx\\modded\\profile1\\saves")
            raw = input("> ").strip().strip('"')
            if not raw:
                print("已取消")
                return
            p = Path(raw)
            if p.is_file():
                save_path = p
            elif (p / "current_run_mp.save").exists():
                save_path = p / "current_run_mp.save"
            else:
                print("路径无效或文件不存在")
                return
        elif len(found) == 1:
            save_path = found[0][0]
            meta = found[0][1]
            mode_str = "模组模式" if meta["is_modded"] else "原版模式"
            print(f"\n找到存档: {save_path}")
            print(f"  ({mode_str} | Steam {meta['steam_id']} | {meta['profile_name']})")
        else:
            print("\n找到多份存档（含多个 Steam 账号或原版/模组不同配置），请选择：")
            summaries = []
            for i, (path, meta) in enumerate(found):
                try:
                    d = load_save(path)
                    sm = get_save_summary(d)
                    summaries.append((path, meta, sm))
                except Exception:
                    summaries.append((path, meta, None))
            for i, (path, meta, sm) in enumerate(summaries, 1):
                mode_str = "模组" if meta["is_modded"] else "原版"
                steam_short = meta["steam_id"][:8] + "..." if len(meta["steam_id"]) > 12 else meta["steam_id"]
                if sm:
                    ps = ", ".join(f"{x['role']}({x['net_id']})" for x in sm["players"])
                    info = f"难度A{sm['ascension']} 第{sm['act']}幕/{sm['floor_count']}层 | 玩家: {ps}"
                else:
                    info = "(读取失败)"
                print(f"  [{i}] {mode_str} | Steam {steam_short} | {meta['profile_name']} | {info}")
                print(f"      路径: {path}")
            try:
                idx = int(input("\n请输入序号 > "))
                if 1 <= idx <= len(found):
                    save_path = found[idx - 1][0]
                else:
                    print("无效选择")
                    return
            except ValueError:
                print("无效输入")
                return

    # 读取
    try:
        data = load_save(save_path)
    except json.JSONDecodeError as e:
        print(f"\n存档 JSON 解析失败: {e}")
        print("建议从备份恢复 (current_run_mp.save.backup.*)")
        return
    except Exception as e:
        print(f"\n读取失败: {e}")
        return

    players = data.get("players", [])
    if not players:
        print("\n存档中没有玩家数据，可能不是有效的多人存档")
        return

    # 显示所选存档摘要（难度、层数、玩家）
    sm = get_save_summary(data)
    print("\n--- 所选存档信息 ---")
    print(f"  难度: 进阶 {sm['ascension']} | 第 {sm['act']} 幕 | 已探索 {sm['floor_count']} 层")
    print(f"  路径: {save_path}")

    # 显示玩家列表
    print("\n当前玩家（Steam 64位 ID，存档内无昵称）：")
    for i, p in enumerate(players, 1):
        net_id = p.get("net_id", "?")
        char_id = p.get("character_id", "")
        name = get_character_display(char_id)
        print(f"  [{i}] {name} (Steam ID: {net_id})")

    if args.list_only:
        print("\n(--list-only 仅列出，未修改)")
        return

    # 输入要保留的序号
    print("\n输入要【保留】的玩家序号，逗号分隔，如: 1,3")
    print("  输入 all 或 留空 = 保留全部（不修改）")
    raw = input("> ").strip()

    if not raw or raw.lower() == "all":
        print("\n未进行修改，已退出")
        return

    try:
        indices = [int(x.strip()) for x in raw.split(",") if x.strip()]
    except ValueError:
        print("输入格式错误，请输入数字，如 1,3")
        return

    # 序号 -> net_id
    idx_to_net_id = {i: p.get("net_id") for i, p in enumerate(players, 1) if p.get("net_id") is not None}
    keep_net_ids = set()
    for idx in indices:
        if idx in idx_to_net_id:
            keep_net_ids.add(idx_to_net_id[idx])
        else:
            print(f"  忽略无效序号: {idx}")

    if not keep_net_ids:
        print("\n没有有效的保留玩家，已取消")
        return

    remove_count = len(players) - len(keep_net_ids)
    if remove_count <= 0:
        print("\n未移除任何玩家，已退出")
        return

    # 确认
    print(f"\n将移除 {remove_count} 名玩家，保留 {len(keep_net_ids)} 名。")
    confirm = input("确认修改？(y/n): ").strip().lower()
    if confirm not in ("y", "yes", "是"):
        print("已取消")
        return

    # 备份
    try:
        backup_path = backup_save(save_path)
        print(f"\n已备份至: {backup_path}")
    except Exception as e:
        print(f"\n备份失败: {e}，已取消修改")
        return

    # 修改
    data, map_drawings_ok = remove_players_from_save(data, keep_net_ids)

    if not map_drawings_ok and data.get("map_drawings"):
        print("  提示: map_drawings 未能解析，已跳过（通常不影响游戏）")

    # 写回
    try:
        save_save(save_path, data)
    except Exception as e:
        print(f"\n写入失败: {e}")
        print("请从备份恢复")
        return

    print(f"\n完成！已移除 {remove_count} 名玩家。")
    print("可启动游戏继续游玩。")


if __name__ == "__main__":
    main()
