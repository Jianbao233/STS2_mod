# -*- coding: utf-8 -*-
"""
分析 v2 存档结构（current_run_mp.save）
打印 players[] 中第一个玩家的完整字段结构，
以及所有字段的数据类型/值示例。
"""

import json
import sys
from pathlib import Path
from pprint import pprint

SAVE_PATH = Path(
    r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\backups\modded_p1_auto_before_copy_20260313_175703\current_run_mp.save"
)


def analyze_value(key: str, value, indent: int = 0) -> str:
    prefix = "  " * indent
    t = type(value).__name__

    if isinstance(value, dict):
        if not value:
            return f"{prefix}{key}: {{}}  [{t}]"
        lines = [f"{prefix}{key}: {{{t}}}"]
        for k2, v2 in list(value.items())[:10]:
            lines.append(f"{prefix}  {k2}: {repr(v2)[:80]}")
        if len(value) > 10:
            lines.append(f"{prefix}  ... ({len(value)} keys total)")
        return "\n".join(lines)

    elif isinstance(value, list):
        if not value:
            return f"{prefix}{key}: []  [{t}]"
        lines = [f"{prefix}{key}: []  [{t}, {len(value)} items]"]
        for i, item in enumerate(value[:3]):
            if isinstance(item, dict):
                lines.append(f"{prefix}  [{i}]: {json.dumps(item, ensure_ascii=False)[:100]}")
            else:
                lines.append(f"{prefix}  [{i}]: {repr(item)}")
        if len(value) > 3:
            lines.append(f"{prefix}  ... ({len(value)} items total)")
        return "\n".join(lines)

    else:
        return f"{prefix}{key}: {repr(value)[:100]}  [{t}]"


def main():
    print(f"读取存档: {SAVE_PATH}\n")

    with open(SAVE_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    print("=== 顶层字段 ===")
    top_keys = list(data.keys())
    for k in top_keys:
        v = data[k]
        t = type(v).__name__
        if isinstance(v, (dict, list)):
            print(f"  {k}: [{t}]  ({len(v)} items)" if isinstance(v, list) else f"  {k}: [{t}]  ({len(v)} keys)")
        else:
            print(f"  {k}: {repr(v)[:80]}  [{t}]")

    players = data.get("players", [])
    print(f"\n=== players[] ({len(players)} 名玩家) ===")

    for i, player in enumerate(players):
        print(f"\n--- 玩家 {i} ---")
        for key in sorted(player.keys()):
            print(analyze_value(key, player[key], indent=1))


if __name__ == "__main__":
    main()
