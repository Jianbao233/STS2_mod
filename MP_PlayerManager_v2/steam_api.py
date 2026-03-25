# -*- coding: utf-8 -*-
"""
Steam 数据访问层
- 自动检测 Steam 安装路径（注册表 HKLM/HKCU）
- 从注册表 ActiveUser 解析当前登录用户的 SteamID64
- 读取 loginusers.vdf → 本机已登录账户昵称（兜底）
- Steam WebAPI 查询好友列表（需要用户配置个人 API Key）
- Steam WebAPI 批量查询玩家昵称/在线状态（5 分钟缓存）
"""

from __future__ import annotations

import gzip
import json
import re
import time
import urllib.request
import urllib.error
from dataclasses import dataclass as _dc
from pathlib import Path
from typing import Optional

# ── 类型 ───────────────────────────────────────────────────────────────────────

SteamID64 = str  # e.g. "76561198000000000"


@_dc
class SteamPlayerInfo:
    steam_id: SteamID64
    nickname: str
    avatar_url: str
    online: bool
    game_name: str
    profile_url: str


@_dc
class SteamAccountInfo:
    steam_id: SteamID64
    persona_name: str
    most_recent: bool
    timestamp: int  # Unix 秒


# ── 全局缓存 ─────────────────────────────────────────────────────────────────

_CACHE: dict[str, tuple[float, list]] = {}
_CACHE_TTL = 300  # 5 分钟


def _cache_get(key: str) -> Optional[list]:
    if key in _CACHE:
        exp, data = _CACHE[key]
        if time.time() < exp:
            return data
        del _CACHE[key]
    return None


def _cache_set(key: str, data: list):
    _CACHE[key] = (time.time() + _CACHE_TTL, data)


# ── Steam 路径检测 ────────────────────────────────────────────────────────────

def get_steam_install_path() -> Optional[Path]:
    """通过注册表查找 Steam 安装根目录（HKLM → HKCU → 常见路径）"""
    import winreg

    for hkey, subkey, name in [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", "InstallPath"),
        (winreg.HKEY_CURRENT_USER,  r"Software\Valve\Steam",  "SteamPath"),
    ]:
        try:
            with winreg.OpenKey(hkey, subkey) as key:
                path, _ = winreg.QueryValueEx(key, name)
                p = Path(path)
                if p.exists():
                    return p
        except OSError:
            pass

    for root in ("J:\\Steam", "C:\\Program Files\\Steam",
                 "C:\\Program Files (x86)\\Steam"):
        p = Path(root)
        if p.exists():
            return p
    return None


def _account_id_to_steam64(account_id: int) -> SteamID64:
    """
    将 Steam Account ID（注册表 ActiveUser）转换为 SteamID64。
    SteamID64 = 76561197960265728 + (account_id >> 1) * 2 + (account_id & 1)
    """
    return str(76561197960265728 + (account_id >> 1) * 2 + (account_id & 1))


def get_current_user_steam_id() -> Optional[SteamID64]:
    """
    从注册表读取当前登录用户的 SteamID64。
    HKCU\\Software\\Valve\\Steam\\ActiveProcess → ActiveUser（Steam Account ID）
    """
    import winreg
    try:
        with winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Valve\Steam\ActiveProcess",
        ) as key:
            val, _ = winreg.QueryValueEx(key, "ActiveUser")
            uid = int(val)
            if uid > 0:
                return _account_id_to_steam64(uid)
    except OSError:
        pass
    return None


def get_current_local_user() -> Optional[SteamAccountInfo]:
    """
    返回当前 Steam 登录账户（优先从 ActiveUser 注册表获取 SteamID64，
    再从 loginusers.vdf 匹配昵称）。
    """
    steam_id = get_current_user_steam_id()
    accounts = get_local_accounts()
    if steam_id:
        for a in accounts:
            if a.steam_id == steam_id:
                return a
    for a in accounts:
        if a.most_recent:
            return a
    return accounts[0] if accounts else None


# ── VDF 解析 ─────────────────────────────────────────────────────────────────

def _tokenize_vdf(content: str) -> list[str]:
    """扫描文件，提取所有 token（含 { } 裸标记）"""
    tokens: list[str] = []
    i = 0
    n = len(content)

    while i < n:
        if content[i] == '/' and i + 1 < n and content[i + 1] == '/':
            eol = content.find('\n', i)
            i = eol + 1 if eol != -1 else n
            continue
        if content[i] == '/' and i + 1 < n and content[i + 1] == '*':
            end = content.find('*/', i + 2)
            i = end + 2 if end != -1 else n
            continue
        if content[i] in ' \t\r':
            i += 1
            continue
        if content[i] == '\n':
            i += 1
            continue
        if content[i] in '{}':
            tokens.append(content[i])
            i += 1
            continue
        if content[i] == '"':
            i += 1
            chars: list[str] = []
            while i < n:
                c = content[i]
                if c == '\\':
                    i += 1
                    if i < n:
                        nc = content[i]
                        es = {"n": "\n", "t": "\t", "r": "\r",
                              "\\": "\\", '"': '"'}.get(nc, nc)
                        chars.append(es)
                        i += 1
                    continue
                if c == '"':
                    i += 1
                    break
                chars.append(c)
                i += 1
            tokens.append("".join(chars))
            continue
        i += 1
    return tokens


def _build_vdf(tokens: list[str]) -> dict:
    """将 token 序列重建为嵌套 dict"""
    root: dict = {}
    stack: list[dict] = [root]
    pending_key: str | None = None

    i = 0
    while i < len(tokens):
        tok = tokens[i]
        i += 1
        if tok == "{":
            if pending_key is not None:
                new_dict: dict = {}
                stack[-1][pending_key] = new_dict
                stack.append(new_dict)
                pending_key = None
        elif tok == "}":
            if len(stack) > 1:
                stack.pop()
            pending_key = None
        else:
            if pending_key is not None:
                stack[-1][pending_key] = tok
                pending_key = None
            else:
                pending_key = tok
    return root


def _parse_vdf(content: str) -> dict:
    return _build_vdf(_tokenize_vdf(content))


# ── loginusers.vdf ────────────────────────────────────────────────────────────

def get_local_accounts() -> list[SteamAccountInfo]:
    """返回本机所有 Steam 账户，按最近登录时间倒序。"""
    path = get_steam_install_path()
    if not path:
        return []
    vdf = path / "config" / "loginusers.vdf"
    if not vdf.exists():
        return []
    try:
        raw = vdf.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        try:
            raw = vdf.read_text(encoding="utf-16-le")
        except Exception:
            raw = ""
    if not raw:
        return []
    data = _parse_vdf(raw)
    users: dict = data.get("users", {})
    accounts: list[SteamAccountInfo] = []
    for sid, info in users.items():
        if not isinstance(info, dict):
            continue
        ts_raw = str(info.get("Timestamp", "0"))
        try:
            ts = int(float(ts_raw))
        except Exception:
            ts = 0
        mr_raw = str(info.get("mostrecent", "0"))
        mr = mr_raw.strip() in ("1", "true", "True")
        accounts.append(SteamAccountInfo(
            steam_id=str(sid).strip(),
            persona_name=info.get("PersonaName", "") or str(sid),
            most_recent=mr,
            timestamp=ts,
        ))
    accounts.sort(key=lambda a: a.timestamp, reverse=True)
    return accounts


# ── Steam WebAPI ───────────────────────────────────────────────────────────────

_STEAM_API_BASE = "https://steamcommunity.com"


def _web_get(url: str, timeout: int = 12) -> dict:
    """GET JSON，自动处理 gzip"""
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
            if resp.info().get("Content-Encoding") == "gzip":
                raw = gzip.decompress(raw)
            return json.loads(raw.decode())
    except Exception:
        return {}


def is_steam_id64(s: str) -> bool:
    s = s.strip()
    return bool(re.match(r"^7656119\d{10}$", s))


def fetch_player_summaries(steam_ids: list[SteamID64],
                            api_key: str = "") -> list[SteamPlayerInfo]:
    """
    批量查询 Steam 玩家摘要（昵称/头像/在线状态/当前游戏）。
    api_key 为空时跳过（只返回空列表）。
    """
    if not steam_ids or not api_key:
        return []
    unique_ids = list(dict.fromkeys(s.strip() for s in steam_ids if s.strip()))
    cache_key = "sum:" + ",".join(sorted(unique_ids))
    cached = _cache_get(cache_key)
    if cached is not None:
        return [SteamPlayerInfo(**p) for p in cached]
    results: list[SteamPlayerInfo] = []
    for chunk_start in range(0, len(unique_ids), 100):
        chunk = unique_ids[chunk_start:chunk_start + 100]
        url = (
            "https://api.steampowered.com/ISteamUser/GetPlayerSummaries_v0002/"
            f"?key={api_key}&steamids={','.join(chunk)}"
        )
        data = _web_get(url)
        for p in data.get("response", {}).get("players", []):
            sid = str(p.get("steamid", ""))
            if not sid:
                continue
            results.append(SteamPlayerInfo(
                steam_id=sid,
                nickname=p.get("personaname", ""),
                avatar_url=p.get("avatarfull", "") or p.get("avatarmedium", ""),
                online=p.get("personastate", 0) == 1,
                game_name=p.get("gameextrainfo", "") or p.get("gameid", "") or "",
                profile_url=p.get("profileurl", ""),
            ))
    if results:
        _cache_set(cache_key, [vars(r) for r in results])
    return results


def fetch_friends_of(steam_id: SteamID64,
                     api_key: str = "") -> list[SteamPlayerInfo]:
    """
    查询指定用户的好友列表（需要 Steam WebAPI Key）。
    先拉好友 ID，再批量查昵称。
    """
    if not api_key:
        return []
    cache_key = f"fof:{steam_id}"
    cached = _cache_get(cache_key)
    if cached is not None:
        return [SteamPlayerInfo(**p) for p in cached]
    url = (
        "https://api.steampowered.com/ISteamUser/GetFriendList_v0001/"
        f"?key={api_key}&steamid={steam_id}&relationship=friend"
    )
    data = _web_get(url)
    friend_ids = [
        str(f["steamid"])
        for f in data.get("friendslist", {}).get("friends", [])
        if f.get("steamid")
    ]
    if not friend_ids:
        return []
    summaries = fetch_player_summaries(friend_ids, api_key)
    if summaries:
        _CACHE[cache_key] = (time.time() + _CACHE_TTL,
                              [vars(r) for r in summaries])
    return summaries


def resolve_vanity_url(vanity: str, api_key: str = "") -> Optional[SteamID64]:
    """通过 Steam 短 URL 解析 SteamID64"""
    if not api_key:
        return None
    url = (f"{_STEAM_API_BASE}/actions/ResolveVanityURL/"
           f"?vanityurl={vanity.strip()}&format=json&key={api_key}")
    data = _web_get(url, timeout=8)
    if data.get("response", {}).get("success") == 1:
        return data["response"].get("steamid")
    return None


def parse_steam_id_from_url(text: str) -> Optional[SteamID64]:
    """从 URL 或纯数字字符串提取 SteamID64"""
    s = text.strip()
    if not s:
        return None
    if s.isdigit() and 15 <= len(s) <= 17:
        return s
    m = re.search(r"steamcommunity\.com/profiles/(\d+)", s)
    if m:
        return m.group(1)
    return None


# ── API Key 管理 ──────────────────────────────────────────────────────────────

def get_api_key_path(tool_dir: str) -> Path:
    return Path(tool_dir) / ".steam_api_key"


def load_api_key(tool_dir: str) -> str:
    """从工具目录加载 Steam API Key（不存在则返回空字符串）"""
    p = get_api_key_path(tool_dir)
    if p.exists():
        try:
            return p.read_text(encoding="utf-8").strip()
        except Exception:
            pass
    return ""


def save_api_key(tool_dir: str, api_key: str) -> bool:
    """保存 Steam API Key 到工具目录"""
    p = get_api_key_path(tool_dir)
    try:
        p.write_text(api_key.strip(), encoding="utf-8")
        return True
    except Exception:
        return False


# ── 一站式联系人获取 ─────────────────────────────────────────────────────────

def _steam64_to_account_id(steam_id: SteamID64) -> int:
    """
    SteamID64 → Steam Account ID
    steam_id = 76561197960265728 + account_id * 2 + (account_id & 1)
    """
    s = int(steam_id)
    base = 76561197960265728
    acc = (s - base) >> 1  # (s - base) // 2
    return acc


def _extract_vdf_braced_block(text: str, open_brace_idx: int) -> Optional[str]:
    """
    从 open_brace_idx（必须为 '{'）起截取完整 { ... }，字符串内的括号不计入深度。
    """
    if open_brace_idx < 0 or open_brace_idx >= len(text) or text[open_brace_idx] != "{":
        return None
    depth = 0
    i = open_brace_idx
    in_string = False
    escape = False
    n = len(text)
    while i < n:
        c = text[i]
        if in_string:
            if escape:
                escape = False
            elif c == "\\":
                escape = True
            elif c == '"':
                in_string = False
        else:
            if c == '"':
                in_string = True
            elif c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0:
                    return text[open_brace_idx : i + 1]
        i += 1
    return None


def _friend_key_to_account_id_or_none(key: str) -> Optional[str]:
    """
    返回可作为 _account_id_to_steam64 输入的「账号 ID 字符串」，或 None。
    - 新版 Steam：键为 17 位 SteamID64 → 直接返回该字符串供上层当 steam_id 用
    - 旧版：键为 8～11 位 account id
    """
    k = str(key).strip()
    if not k.isdigit():
        return None
    if len(k) == 17 and k.startswith("7656119"):
        return k  # 已是 SteamID64，get_all_contacts 里不再二次转换
    if 8 <= len(k) <= 11:
        return k
    return None


def _parse_friends_vdf_block(block_inner: str) -> list[SteamAccountInfo]:
    """解析 friends 子树 { "id" { "name" "..." } ... }，支持 SteamID64 与 account id 两种键。"""
    friends: list[SteamAccountInfo] = []
    seen: set[str] = set()
    try:
        wrapped = '"_friends_root" ' + block_inner
        root = _parse_vdf(wrapped)
        inner = root.get("_friends_root", {})
    except Exception:
        return []
    if not isinstance(inner, dict):
        return []
    for key, val in inner.items():
        if not isinstance(val, dict):
            continue
        raw_name = val.get("name")
        if raw_name is None:
            continue
        name = str(raw_name).strip()
        if not name or len(name) > 100:
            continue
        kid = _friend_key_to_account_id_or_none(key)
        if not kid:
            continue
        # 统一用 SteamID64 做去重键
        if len(kid) == 17:
            dedup = kid
        else:
            dedup = _account_id_to_steam64(int(kid))
        if dedup in seen:
            continue
        seen.add(dedup)
        friends.append(SteamAccountInfo(
            steam_id=kid,
            persona_name=name,
            most_recent=False,
            timestamp=0,
        ))
    return friends


def _friends_regex_fallback(snippet: str) -> list[SteamAccountInfo]:
    """结构化解析失败时的兜底：分别匹配 account id 键与 SteamID64 键。"""
    import re as _re

    friends: list[SteamAccountInfo] = []
    seen: set[str] = set()

    def _add(sid_raw: str, name: str) -> None:
        name = name.strip()
        if not name or len(name) > 100:
            return
        if len(sid_raw) == 17 and sid_raw.startswith("7656119"):
            dedup = sid_raw
        elif sid_raw.isdigit() and 8 <= len(sid_raw) <= 11:
            dedup = _account_id_to_steam64(int(sid_raw))
        else:
            return
        if dedup in seen:
            return
        seen.add(dedup)
        friends.append(SteamAccountInfo(
            steam_id=sid_raw,
            persona_name=name,
            most_recent=False,
            timestamp=0,
        ))

    # 旧版：8～11 位 account id
    for m in _re.finditer(
        r'"(\d{8,11})"\s*[\r\n]+\s*\{[^}]*?"name"\s+"([^"]+)"',
        snippet,
        _re.DOTALL,
    ):
        _add(m.group(1), m.group(2))

    # 新版：SteamID64 为键
    for m in _re.finditer(
        r'"(7656119\d{10})"\s*[\r\n]+\s*\{[^}]*?"name"\s+"([^"]+)"',
        snippet,
        _re.DOTALL,
    ):
        _add(m.group(1), m.group(2))

    return friends


def get_local_friends_from_config(
    user_account_id: int,
) -> list[SteamAccountInfo]:
    """
    直接解析 localconfig.vdf 的 friends 区段，返回好友列表。
    这是完全离线的，不需要 API Key 或网络。
    user_account_id：Steam Account ID（注册表 ActiveUser 的值，也是 userdata 文件夹名）

    支持：
    - 键为 8～11 位 account id（旧客户端）
    - 键为 17 位 SteamID64（新客户端）
    - 嵌套子对象（完整截取 friends 的 { ... } 再 VDF 解析）
    """
    path = get_steam_install_path()
    if not path:
        return []
    cfg = path / "userdata" / str(user_account_id) / "config" / "localconfig.vdf"
    if not cfg.exists():
        return []

    try:
        text = cfg.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return []

    fi = text.lower().find('"friends"')
    if fi < 0:
        return []
    snippet = text[fi : fi + 5_000_000]
    brace_start = text.find("{", fi)
    if brace_start < 0:
        return _friends_regex_fallback(snippet)

    block = _extract_vdf_braced_block(text, brace_start)
    if not block:
        return _friends_regex_fallback(snippet)

    friends = _parse_friends_vdf_block(block)
    if not friends:
        friends = _friends_regex_fallback(snippet)
    return friends


def get_all_contacts(tool_dir: str) -> list[SteamPlayerInfo]:
    """
    返回当前 Steam 玩家登录账户的好友列表（含昵称）。

    数据来源优先级（完全离线优先）：
    1. localconfig.vdf friends 区段（通过 ActiveUser 注册表直接定位）
    2. 遍历所有 userdata 文件夹（兜底）
    3. Steam WebAPI GetFriendList（需要 API Key）
    4. loginusers.vdf 本机账户（无需网络）
    """
    import winreg as _winreg

    # 1. ActiveUser 就是 userdata 文件夹名，直接用
    try:
        with _winreg.OpenKey(
            _winreg.HKEY_CURRENT_USER,
            r"Software\Valve\Steam\ActiveProcess",
        ) as k:
            val, _ = _winreg.QueryValueEx(k, "ActiveUser")
            active_account_id = int(val)
    except Exception:
        active_account_id = 0

    def _friend_entry_to_player(f: SteamAccountInfo) -> SteamPlayerInfo:
        sid = str(f.steam_id).strip()
        if len(sid) == 17 and sid.startswith("7656119"):
            sid64 = sid
        else:
            sid64 = _account_id_to_steam64(int(sid))
        return SteamPlayerInfo(
            steam_id=sid64,
            nickname=f.persona_name,
            avatar_url="",
            online=False,
            game_name="",
            profile_url=_STEAM_API_BASE + "/profiles/" + sid64,
        )

    if active_account_id > 0:
        friends = get_local_friends_from_config(active_account_id)
        if friends:
            return [_friend_entry_to_player(f) for f in friends]

    # 2. 遍历所有 userdata 文件夹
    steam_path = get_steam_install_path()
    if steam_path:
        userdata_root = steam_path / "userdata"
        if userdata_root.exists():
            for folder in sorted(userdata_root.iterdir()):
                if not folder.is_dir():
                    continue
                try:
                    folder_id = int(folder.name)
                except ValueError:
                    continue
                friends = get_local_friends_from_config(folder_id)
                if friends:
                    return [_friend_entry_to_player(f) for f in friends]

    # 3. WebAPI
    api_key = load_api_key(tool_dir)
    current_sid = get_current_user_steam_id()
    if api_key and current_sid:
        friends = fetch_friends_of(current_sid, api_key)
        if friends:
            return friends

    # 4. loginusers 本机账户
    accounts = get_local_accounts()
    if accounts:
        summaries = fetch_player_summaries(
            [a.steam_id for a in accounts], api_key
        )
        if summaries:
            return summaries
        return [
            SteamPlayerInfo(
                steam_id=a.steam_id,
                nickname=a.persona_name,
                avatar_url="",
                online=False,
                game_name="",
                profile_url=_STEAM_API_BASE + "/profiles/" + a.steam_id,
            )
            for a in accounts
        ]
    return []



# ── 工具 ─────────────────────────────────────────────────────────────────────

def format_steam_id_brief(sid: SteamID64) -> str:
    """截断显示：前5位...末4位"""
    if len(sid) >= 9:
        return f"{sid[:5]}...{sid[-4:]}"
    return sid
