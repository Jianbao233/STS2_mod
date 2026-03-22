# -*- coding: utf-8 -*-
"""
analyze_run_ancient_stats.py
=============================
对本地 .run 存档进行先古之民相关流水统计，输出 JSON 报告供人工校对与回归检测。

用法
----
    python analyze_run_ancient_stats.py --run-dir "K:/path/to/run_history"
    python analyze_run_ancient_stats.py --run-dir "dir1" --run-dir "dir2"

输出
----
    Data/ancient_stats_report.json（含以下字段）
    - per_node_type: 按 map_point_type 分组的统计
    - gold_by_ancient: ancient 节点 gold_gained 分位数
    - relic_picks_by_type: 各节点类型 relic_choices was_picked 次数分布
    - summary: 整体摘要

依赖
----
    Python 3.10+，标准库 json / pathlib / statistics / argparse
"""

import json
import statistics
from pathlib import Path
from datetime import datetime
from typing import Any
import argparse

# ---------------------------------------------------------------------------
# 全局配置
# ---------------------------------------------------------------------------
DEFAULT_DB_PATH = Path(__file__).parent.parent / "Data" / "ancient_peoples_rules.json"
DEFAULT_OUTPUT  = Path(__file__).parent.parent / "Data" / "ancient_stats_report.json"

# ---------------------------------------------------------------------------
# 辅助
# ---------------------------------------------------------------------------
def safe_percentile(data: list[float], p: float) -> float | None:
    if len(data) < 2:
        return data[0] if data else None
    sorted_data = sorted(data)
    idx = (len(sorted_data) - 1) * p / 100.0
    lower = int(idx)
    upper = min(lower + 1, len(sorted_data) - 1)
    frac = idx - lower
    return round(sorted_data[lower] + frac * (sorted_data[upper] - sorted_data[lower]), 2)


def describe_distribution(values: list[float]) -> dict[str, Any]:
    if not values:
        return {"count": 0}
    return {
        "count": len(values),
        "min": round(min(values), 2),
        "max": round(max(values), 2),
        "mean": round(statistics.mean(values), 2),
        "median": round(statistics.median(values), 2),
        "p50": safe_percentile(values, 50),
        "p75": safe_percentile(values, 75),
        "p90": safe_percentile(values, 90),
        "p95": safe_percentile(values, 95),
        "p99": safe_percentile(values, 99),
    }


# ---------------------------------------------------------------------------
# 关键修复：正确获取节点类型
# ---------------------------------------------------------------------------
# 实测存档证明：
#   - map_point_type=ancient 的 rooms[].room_type = "event"
#   - map_point_type=monster 的 rooms[].room_type = "monster"
#   - 结论：应优先取 map_point_type；若为 "unknown" 则降级取 rooms[].room_type
# ---------------------------------------------------------------------------
def get_map_point_type(node: dict) -> str:
    """
    获取节点类型。

    优先级：
    1. map_point_type（若非空/非 "unknown"）
    2. rooms[].room_type（第一个非空非 "none" 的值）
    3. 默认 "unknown"

    注意：ancient 节点的 rooms[].room_type 通常为 "event"，
          必须以 map_point_type 为准。
    """
    mpt = node.get("map_point_type", "")
    if mpt and mpt.lower() not in ("unknown", "none", ""):
        return mpt.lower()

    for room in node.get("rooms", []):
        rt = (room.get("room_type") or "").strip().lower()
        if rt and rt not in ("none", ""):
            return rt
    return "unknown"


# ---------------------------------------------------------------------------
# .run 文件解析
# ---------------------------------------------------------------------------
def parse_run_file(path: Path) -> dict[str, Any] | None:
    try:
        with path.open(encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def collect_nodes(history: dict[str, Any]) -> list[dict]:
    result = []
    for act in history.get("map_point_history", []):
        for node in act:
            result.append(node)
    return result


def collect_gold_and_picks(nodes: list[dict]) -> dict[str, list[dict]]:
    """
    按节点类型分组收集 gold_gained 与 relic_choices 统计。
    返回结构：{ map_point_type: [{"gold_gained": int, "relic_picks": int}, ...] }
    """
    grouped: dict[str, list[dict]] = {}
    for node in nodes:
        mpt = get_map_point_type(node)
        grouped.setdefault(mpt, [])
        for stat in node.get("player_stats", []):
            gold = stat.get("gold_gained", 0)
            picks = sum(1 for c in stat.get("relic_choices", []) if c.get("was_picked", False))
            grouped[mpt].append({"gold_gained": gold, "relic_picks": picks})
    return grouped


def analyze_all(run_files: list[Path]) -> dict[str, Any]:
    all_nodes: list[dict] = []
    total_runs = 0
    error_runs = 0

    for rp in run_files:
        data = parse_run_file(rp)
        if data is None:
            error_runs += 1
            continue
        total_runs += 1
        all_nodes.extend(collect_nodes(data))

    grouped = collect_gold_and_picks(all_nodes)

    per_type: dict[str, Any] = {}
    for mpt, items in grouped.items():
        golds = [float(x["gold_gained"]) for x in items]
        picks = [float(x["relic_picks"])  for x in items]
        per_type[mpt] = {
            "node_count": len(items),
            "gold_gained": describe_distribution(golds),
            "relic_picks": describe_distribution(picks),
        }

    # relic_picks 次数分布
    relic_pick_dist: dict[str, dict[int, int]] = {}
    for mpt, items in grouped.items():
        dist: dict[int, int] = {}
        for x in items:
            p = int(x["relic_picks"])
            dist[p] = dist.get(p, 0) + 1
        relic_pick_dist[mpt] = dict(sorted(dist.items()))

    return {
        "total_runs_analyzed": total_runs,
        "failed_runs": error_runs,
        "total_nodes_analyzed": len(all_nodes),
        "per_node_type": per_type,
        "relic_picks_distribution": relic_pick_dist,
    }


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(
        description="Analyze .run files for Ancient-related statistics")
    parser.add_argument(
        "--run-dir", "-d", action="append", default=[],
        help="Add a directory to scan for .run files (can be repeated)")
    parser.add_argument(
        "--output", "-o", type=Path, default=DEFAULT_OUTPUT,
        help="Output JSON path")
    parser.add_argument(
        "--db", type=Path, default=DEFAULT_DB_PATH,
        help="Path to ancient_peoples_rules.json")
    args = parser.parse_args()

    # 收集 .run 文件
    run_files: list[Path] = []
    for d in args.run_dir:
        dp = Path(d)
        if dp.is_file() and dp.suffix == ".run":
            run_files.append(dp)
        elif dp.is_dir():
            run_files.extend(dp.glob("*.run"))

    if not args.run_dir:
        default_history = Path(__file__).parent.parent / "history"
        if default_history.is_dir():
            run_files.extend(default_history.glob("*.run"))

    run_files = sorted(set(run_files))

    if not run_files:
        output = {
            "generated_at": datetime.now().strftime("%Y-%m-%d"),
            "generated_by": "tools/analyze_run_ancient_stats.py",
            "total_runs_analyzed": 0,
            "message": "No .run files found. Use --run-dir to specify directories.",
        }
    else:
        print(f"[analyze] Found {len(run_files)} .run file(s). Analyzing...")
        stats = analyze_all(run_files)
        output = {
            "generated_at": datetime.now().strftime("%Y-%m-%d"),
            "generated_by": "tools/analyze_run_ancient_stats.py",
            "run_files_scanned": [str(r) for r in run_files],
        }
        output.update(stats)

    # 加载数据库摘要
    if args.db.is_file():
        try:
            with args.db.open(encoding="utf-8") as f:
                db = json.load(f)
            output["database_info"] = {
                "schema_version": db.get("schema_version"),
                "game_version_range": db.get("game_version_range"),
                "generated_at": db.get("generated_at"),
                "relic_count": len(db.get("relic_effects", [])),
                "event_count": len(db.get("event_effects", [])),
                "npc_count": len(db.get("ancient_npcs", [])),
            }
        except Exception as e:
            print(f"[analyze] Warning: could not load database: {e}")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)

    print(f"[analyze] Done -> {args.output}")
    runs_found = output.get("total_runs_analyzed", 0)
    nodes_found = output.get("total_nodes_analyzed", 0)
    print(f"  runs : {runs_found}")
    print(f"  nodes: {nodes_found}")
    if runs_found > 0:
        print()
        print("  节点类型               节点数  gold p75     gold max   relic_picks max")
        print("  " + "-" * 80)
        for mpt, info in sorted(output.get("per_node_type", {}).items(),
                                  key=lambda x: -x[1].get("node_count", 0)):
            g = info.get("gold_gained", {})
            r_dist = output.get("relic_picks_distribution", {}).get(mpt, {})
            max_pick = max(r_dist.keys()) if r_dist else 0
            print(f"  {mpt:20s} {info['node_count']:6d}  "
                  f"p75={str(g.get('p75','?')):>6}  max={str(g.get('max','?')):>6}  "
                  f"relic_picks max={max_pick}")


if __name__ == "__main__":
    main()
