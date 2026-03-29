# -*- coding: utf-8 -*-
"""
存档读写层：扫描/加载/备份/保存 current_run_mp.save
"""

import gzip
import json
import os
import shutil
from datetime import datetime
from pathlib import Path
from typing import Optional
from dataclasses import dataclass


APP_BASE = Path(os.environ.get("APPDATA", r"C:\Users\Administrator\AppData\Roaming"))
# 若存档不在默认 Roaming 路径，可设置环境变量 SLAY_THE_SPIRE2_APPDATA 指向
# 包含 steam 子目录的根（即与默认下 SlayTheSpire2 同级的那份目录）
_STS2_OVERRIDE = os.environ.get("SLAY_THE_SPIRE2_APPDATA", "").strip()
STS2_ROOT = Path(_STS2_OVERRIDE) if _STS2_OVERRIDE else (APP_BASE / "SlayTheSpire2")
STEAM_DIR = STS2_ROOT / "steam"


def _discover_mp_save_files(steam_folder: Path) -> list[tuple[Path, str]]:
    """
    在单个 SteamID 目录下发现所有多人存档文件。
    返回 [(绝对路径, profile_key), ...]，profile_key 形如 profile1、modded/profile2。
    游戏实际使用 profile1/profile2…，而非仅有 profile。
    """
    # 正常模式（非 mod 模式）存档
    normal_patterns = (
        "profile*/saves/current_run.save",
        "modded/profile*/saves/current_run.save",
    )
    # mod 模式存档
    modded_patterns = (
        "profile*/saves/current_run_mp.save",
        "modded/profile*/saves/current_run_mp.save",
    )
    seen: set[Path] = set()
    out: list[tuple[Path, str]] = []
    for pat in normal_patterns + modded_patterns:
        for save_file in steam_folder.glob(pat):
            if not save_file.is_file():
                continue
            try:
                resolved = save_file.resolve()
            except OSError:
                continue
            if resolved in seen:
                continue
            seen.add(resolved)
            try:
                rel = save_file.relative_to(steam_folder)
            except ValueError:
                continue
            parts = rel.parts
            if len(parts) >= 3 and parts[-2] == "saves" and parts[-1] == "current_run_mp.save":
                profile_key = Path(*parts[:-2]).as_posix()
                out.append((save_file, profile_key))
    return out


@dataclass
class SaveProfile:
    """一份存档的基本信息"""
    path: Path
    rel_path: str          # 相对于 %APPDATA% 的路径，用于显示
    profile_key: str       # "modded/profile1" 这样的 key
    player_count: int
    act_index: int
    ascension: int
    players_summary: str   # "静默猎手、铁甲战士"
    save_time: Optional[datetime]
    is_active: bool       # run_time > 0 表示对局进行中
    is_modded: bool        # True = modded/profileN，False = profileN
    is_mp_save: bool       # 固定为 True（只扫描 current_run_mp.save）
    steam_id: str          # 所属 SteamID64


@dataclass
class SaveBackup:
    """一个备份文件"""
    path: Path
    timestamp: str
    player_count: int
    players_summary: str
    save_time: Optional[datetime]


def _load_raw(path: Path) -> Optional[dict]:
    """读取存档 JSON（自动处理 gzip/明文）"""
    if not path.exists():
        return None
    try:
        raw = path.read_bytes()
        if raw[:2] == b'\x1f\x8b':          # gzip magic
            raw = gzip.decompress(raw)
        text = raw.decode("utf-8")
        return json.loads(text)
    except Exception:
        return None


def _load_raw_from_backup(path: Path) -> Optional[dict]:
    """从 .backup.* 文件读取原始存档"""
    return _load_raw(path)


def _detect_players_summary(data: dict) -> str:
    """从存档数据提取玩家摘要"""
    players = data.get("players", [])
    if not players:
        return "无玩家"
    chars = [p.get("character_id", "?") for p in players]
    # 尝试读取 steam_names
    summary = ", ".join(chars)
    return summary


def scan_save_profiles(steam_id: str = "") -> list[SaveProfile]:
    """扫描所有存档 profile，返回按最后修改时间降序排列"""
    results = []
    if not STEAM_DIR.exists():
        return results

    for steam_folder in STEAM_DIR.iterdir():
        if not steam_folder.is_dir():
            continue
        if steam_id and steam_folder.name != steam_id:
            continue

        for save_file, sub_key in _discover_mp_save_files(steam_folder):
            data = _load_raw(save_file)
            if data is None:
                continue

            players = data.get("players", [])
            act_idx = data.get("current_act_index", 0)
            asc = data.get("ascension", 0)
            run_time = data.get("run_time", 0)
            st = data.get("save_time", 0)
            save_dt = datetime.fromtimestamp(st) if st else None

            profile_key = f"{steam_folder.name}/{sub_key}"
            is_modded = sub_key.startswith("modded/")
            results.append(SaveProfile(
                path=save_file,
                rel_path=f"{steam_folder.name}/{sub_key}/saves/current_run_mp.save",
                profile_key=profile_key,
                player_count=len(players),
                act_index=act_idx,
                ascension=asc,
                players_summary=_detect_players_summary(data),
                save_time=save_dt,
                is_active=run_time > 0,
                is_modded=is_modded,
                is_mp_save=True,
                steam_id=steam_folder.name,
            ))

    results.sort(key=lambda x: x.path.stat().st_mtime, reverse=True)
    return results


@dataclass
class BackupEntry:
    """一个备份文件条目，可定位到其所属的 Steam 用户 + profile"""
    path: Path
    timestamp: str           # "YYYYMMDD_HHMMSS"
    player_count: int
    players_summary: str
    save_time: Optional[datetime]
    steam_id: str
    profile_key: str         # "7656.../profile1" 格式
    save_path: Path          # 对应的 current_run_mp.save 路径


def scan_backups(save_path: Path) -> list[SaveBackup]:
    """扫描某个存档的所有 .backup.* 文件"""
    backups = []
    parent = save_path.parent
    stem = save_path.stem          # "current_run_mp"
    for f in parent.iterdir():
        if not f.name.startswith(stem + ".save.backup."):
            continue
        ts = f.name[len(stem) + len(".save.backup."):]
        data = _load_raw(f)
        if data:
            players = data.get("players", [])
            backups.append(SaveBackup(
                path=f,
                timestamp=ts,
                player_count=len(players),
                players_summary=_detect_players_summary(data),
                save_time=datetime.fromtimestamp(data.get("save_time", 0)) or None,
            ))
    backups.sort(key=lambda x: x.timestamp, reverse=True)
    return backups


def load_save(path: Path) -> Optional[dict]:
    return _load_raw(path)


def save_backup(path: Path) -> Path:
    """为存档创建带时间戳的备份，返回备份路径"""
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = path.with_name(path.name + f".backup.{ts}")
    shutil.copy2(path, backup_path)
    return backup_path


def write_save(path: Path, data: dict, make_backup: bool = True) -> bool:
    """
    将数据写回存档（自动备份）。

    游戏原始存档格式为「明文 JSON」（无 gzip 压缩）。
    为与游戏兼容，写入时不压缩。
    """
    if make_backup:
        save_backup(path)

    text = json.dumps(data, ensure_ascii=False, indent=2)
    # 游戏原始存档使用 CRLF 换行（\r\n），与游戏保持一致
    text = text.replace("\n", "\r\n")
    raw = text.encode("utf-8")

    # 先写临时文件，再 rename，防止写坏
    tmp = path.with_suffix(".tmp")
    try:
        tmp.write_bytes(raw)
        tmp.replace(path)
        return True
    except OSError:
        if tmp.exists():
            tmp.unlink()
        return False


def restore_backup(backup_path: Path, original_path: Path) -> bool:
    """从备份恢复到原始路径"""
    try:
        shutil.copy2(backup_path, original_path)
        return True
    except OSError:
        return False


def delete_backup_file(backup_path: Path) -> bool:
    """删除单个备份文件（不触碰主存档）"""
    try:
        backup_path.unlink()
        return True
    except OSError:
        return False


# ── 全局备份扫描 ─────────────────────────────────────────────────────────────


def _discover_all_profile_dirs(steam_folder: Path) -> list[Path]:
    """返回 steam_folder 下所有包含 saves 目录的子目录（profile 根）"""
    seen: set[Path] = set()
    for p in steam_folder.glob("profile*/saves"):
        if p.is_dir():
            seen.add(p.parent)
    for p in steam_folder.glob("modded/profile*/saves"):
        if p.is_dir():
            seen.add(p.parent)
    return list(seen)


def scan_all_backups() -> list[BackupEntry]:
    """
    扫描所有存档目录下所有 .backup.* 文件，返回 flat list。
    每条记录含所属 Steam 用户、profile_key、对应存档路径，可直接恢复。
    """
    results: list[BackupEntry] = []
    if not STEAM_DIR.exists():
        return results

    for steam_folder in STEAM_DIR.iterdir():
        if not steam_folder.is_dir():
            continue
        sid = steam_folder.name

        for profile_root in _discover_all_profile_dirs(steam_folder):
            saves_dir = profile_root / "saves"

            # 找主存档文件（用于恢复）
            main_save = saves_dir / "current_run_mp.save"
            if not main_save.exists():
                main_save = saves_dir / "current_run.save"

            profile_key = f"{sid}/{profile_root.relative_to(steam_folder).as_posix()}"

            # 扫描该目录所有 .backup.* 文件
            if not saves_dir.exists():
                continue
            for f in saves_dir.iterdir():
                if f.name.startswith("current_run_mp.save.backup."):
                    ts = f.name[len("current_run_mp.save.backup."):]
                elif f.name.startswith("current_run.save.backup."):
                    ts = f.name[len("current_run.save.backup."):]
                else:
                    continue

                data = _load_raw(f)
                if not data:
                    continue

                players = data.get("players", [])
                results.append(BackupEntry(
                    path=f,
                    timestamp=ts,
                    player_count=len(players),
                    players_summary=_detect_players_summary(data),
                    save_time=datetime.fromtimestamp(data.get("save_time", 0)) or None,
                    steam_id=sid,
                    profile_key=profile_key,
                    save_path=main_save,
                ))

    results.sort(key=lambda x: x.timestamp, reverse=True)
    return results
