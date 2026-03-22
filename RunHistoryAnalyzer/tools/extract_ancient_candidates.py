# -*- coding: utf-8 -*-
"""
extract_ancient_candidates.py
=============================
扫描 SL2 源码 Localization JSON，提取与先古之民（Ancient）相关的遗物、事件与节点行为，
输出为 `Data/ancient_peoples_rules.json`（兼容 AncientRuleLoader 的 schema）。

用法
----
    python extract_ancient_candidates.py [--lang {zhs|eng}] [--output PATH]

依赖
----
    Python 3.10+，标准库 json / pathlib / re
"""

import json
import re
import sys
from pathlib import Path
from datetime import datetime
from typing import Any

# ---------------------------------------------------------------------------
# 全局配置
# ---------------------------------------------------------------------------
SL2_ROOT = Path(r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\extracted")
DEFAULT_LANG = "zhs"          # 中文为主，英文为辅
OUTPUT_PATH = Path(__file__).parent.parent / "Data" / "ancient_peoples_rules.json"


# ---------------------------------------------------------------------------
# 辅助
# ---------------------------------------------------------------------------
def load_json(lang: str, filename: str) -> dict[str, Any]:
    path = SL2_ROOT / "localization" / lang / filename
    if not path.exists():
        raise FileNotFoundError(f"找不到 {path}")
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def first_sentence(text: str) -> str:
    """取第一行或第一个句号前的字符串（用于 description 摘要）。"""
    if not text:
        return ""
    s = text.strip()
    for sep in ("\n", "。"):
        if sep in s:
            s = s.split(sep)[0]
    return s.strip()


def any_match(patterns: list[str], text: str) -> bool:
    for p in patterns:
        if p in text:
            return True
    return False


def regex_match(patterns: list[re.Pattern], text: str) -> bool:
    for p in patterns:
        if p.search(text):
            return True
    return False


# ---------------------------------------------------------------------------
# 关键词正则（直接写实际字符，不用 \\uf... 转义）
# ---------------------------------------------------------------------------
# 注意：re.compile 的原始字符串中写入实际 Unicode 字符

# 中文关键词（直接含在字符串中）
ZH_GOLD_PATTERNS = [
    # 匹配 "{Gold}" 后接金币，或 拾起时 之后出现金
    re.compile(r"\{Gold\}.*?金币"),           # {Gold}xxx金币
    re.compile(r"\{Gold\+?\}.*?gold", re.I),  # {Gold+}xxxgold
    re.compile(r"金币.*?\{Gold\}"),            # 金币xxx{Gold}
    re.compile(r"gold.*?\{Gold\}", re.I),     # goldxxx{Gold}
    re.compile(r"gold.*?\{Gold\}", re.I),
    re.compile(r"Gold\}"),                    # 任何含 {Gold} 或 {Gold+} 的字段名
    re.compile(r"Gold\+\}"),
]

ZH_CARDS_PATTERNS = [
    re.compile(r"\{Cards\}.*?牌"),
    re.compile(r"随机.*?牌"),
    re.compile(r"将.*?张.*?加入.*?牌组"),
    re.compile(r"add.*?card.*?to.*?deck", re.I),
]

ZH_RELICS_PATTERNS = [
    re.compile(r"\{Relics\}"),
    re.compile(r"蜡制遗物"),
    re.compile(r"件.*?遗物"),
    re.compile(r"wax.*?relic", re.I),
    re.compile(r"relic.*?on.*?pickup", re.I),
]

ZH_FOREIGN_PATTERNS = [
    re.compile(r"其他角色"),
    re.compile(r"来自.*?的牌"),
    re.compile(r"另一角色"),
    re.compile(r"another character", re.I),
    re.compile(r"foreign.*?card", re.I),
    re.compile(r"other.*?character.*?card", re.I),
]

ZH_SACRIFICE_PATTERNS = [
    re.compile(r"献祭"),
    re.compile(r"牺牲"),
    re.compile(r"sacrifice", re.I),
    re.compile(r"Sacrifices\}"),
]

ZH_PICKUP_PATTERNS = [
    re.compile(r"拾起时"),
    re.compile(r"upon pickup", re.I),
    re.compile(r"on pickup", re.I),
]

ZH_ANCIENT_PATTERNS = [
    re.compile(r"古老"),
    re.compile(r"先古"),
    re.compile(r"Ancient"),
    re.compile(r"ancient", re.I),
    re.compile(r"Archaic"),
]

ZH_STARTER_PATTERNS = [
    re.compile(r"初始.*?变化"),
    re.compile(r"变化为先古版本"),
    re.compile(r"starter.*ancient", re.I),
    re.compile(r"initial.*to.*ancient", re.I),
]


# ---------------------------------------------------------------------------
# 从 relics.json 提取
# ---------------------------------------------------------------------------
def scan_relics(data_zh: dict[str, Any], data_en: dict[str, Any]) -> list[dict]:
    """
    遍历所有 relic 条目，筛出与先古/金币/异色卡/多遗物/献祭相关的遗物。
    返回列表中每项对应 schema_version=1 的 relic_effects 元素。
    """
    results: list[dict] = []
    seen_ids: set[str] = set()

    for key in data_zh:
        if "." not in key:
            continue
        relic_id, field = key.split(".", 1)
        if relic_id in seen_ids:
            continue

        zh_desc      = data_zh.get(f"{relic_id}.description", "")
        zh_event_desc= data_zh.get(f"{relic_id}.eventDescription", "")
        zh_title     = data_zh.get(f"{relic_id}.title", "")
        en_desc      = data_en.get(f"{relic_id}.description", "")
        en_title     = data_en.get(f"{relic_id}.title", "")
        en_event_desc= data_en.get(f"{relic_id}.eventDescription", "")

        combined = " ".join(filter(None, [zh_desc, zh_event_desc, en_desc, en_event_desc]))

        if not (zh_desc or en_desc):
            continue

        # --- 分类判断 ---
        is_pickup      = regex_match(ZH_PICKUP_PATTERNS, zh_desc + " " + en_desc)
        has_gold       = regex_match(ZH_GOLD_PATTERNS, combined)
        has_cards      = regex_match(ZH_CARDS_PATTERNS, combined)
        has_relics_p   = regex_match(ZH_RELICS_PATTERNS, combined)
        has_foreign    = regex_match(ZH_FOREIGN_PATTERNS, combined)
        has_sacrifice  = regex_match(ZH_SACRIFICE_PATTERNS, combined)
        is_ancient_rel = regex_match(ZH_ANCIENT_PATTERNS, zh_title + " " + en_title)
        is_starter_chg = regex_match(ZH_STARTER_PATTERNS, combined)

        if not any([has_gold, has_cards, has_relics_p, has_foreign, has_sacrifice, is_starter_chg, is_ancient_rel]):
            continue

        seen_ids.add(relic_id)

        tags: list[str] = []
        effect_detail: dict = {}
        note_zh_parts: list[str] = []
        note_en_parts: list[str] = []

        if is_pickup:
            tags.append("on_pickup")
        if has_gold:
            tags.append("gold_on_pickup")
            effect_detail["gold_variable"] = True
            note_zh_parts.append("拾起时获得可变金币（{Gold}）")
            note_en_parts.append("Grants variable gold on pickup ({Gold})")
        if has_cards:
            tags.append("cards_on_pickup")
            effect_detail["cards_on_pickup"] = True
            note_zh_parts.append("拾起时获得卡牌入组")
            note_en_parts.append("Adds card(s) to deck on pickup")
        if has_relics_p:
            tags.append("multi_relic_on_pickup")
            effect_detail["relics_on_pickup"] = True
            note_zh_parts.append("拾起时额外授予蜡制遗物")
            note_en_parts.append("Grants extra wax relic(s) on pickup")
        if has_foreign:
            tags.append("foreign_character_cards")
            effect_detail["foreign_character_cards"] = True
            note_zh_parts.append("可选其他角色的卡牌入组")
            note_en_parts.append("May add cards from another character")
        if has_sacrifice:
            tags.append("card_sacrifice_reward")
            effect_detail["card_sacrifice_reward"] = True
            note_zh_parts.append("通过献祭卡牌可额外获得遗物")
            note_en_parts.append("Sacrificing cards grants extra relics")
        if is_starter_chg:
            tags.append("starter_to_ancient")
            effect_detail["starter_to_ancient"] = True
            note_zh_parts.append("将初始卡牌变化为先古版本")
            note_en_parts.append("Transforms starter card into Ancient version")
        if is_ancient_rel:
            tags.append("ancient_named")

        entry: dict[str, Any] = {
            "id": relic_id,
            "title_zh": zh_title or relic_id,
            "title_en": en_title or relic_id,
            "description_zh": first_sentence(zh_desc),
            "description_en": first_sentence(en_desc),
            "tags": tags,
            "effects": effect_detail,
            "note_zh": "；".join(note_zh_parts) if note_zh_parts else None,
            "note_en": "; ".join(note_en_parts) if note_en_parts else None,
        }
        results.append(entry)

    return results


# ---------------------------------------------------------------------------
# 从 ancients.json 提取（NPC 行为模式）
# ---------------------------------------------------------------------------
def scan_ancients(data_zh: dict[str, Any], data_en: dict[str, Any]) -> list[dict]:
    """
    遍历 ancients.json，找出所有先古 NPC（以 .ancient 结尾的对话行）。
    返回列表中每项对应 schema_version=1 的 ancient_npcs 元素。
    """
    results: list[dict] = []
    seen_ids: set[str] = set()

    for key in data_zh:
        if "." not in key:
            continue
        anc_id, rest = key.split(".", 1)
        if anc_id in seen_ids:
            continue

        title_zh  = data_zh.get(f"{anc_id}.title", "")
        epithet_zh= data_zh.get(f"{anc_id}.epithet", "")
        title_en  = data_en.get(f"{anc_id}.title", "")
        epithet_en= data_en.get(f"{anc_id}.epithet", "")

        if not (title_zh or title_en):
            continue

        seen_ids.add(anc_id)

        # 收集所有 .ancient 对话行
        ancient_talks: list[str] = []
        for k, v in data_zh.items():
            if k.startswith(f"{anc_id}.talk.") and k.endswith(".ancient") and isinstance(v, str):
                ancient_talks.append(first_sentence(v))

        tags: list[str] = []
        reward_hints: list[str] = []

        combined_text = " ".join(ancient_talks)
        if regex_match(ZH_GOLD_PATTERNS, combined_text):
            tags.append("offers_gold")
            reward_hints.append("可能赠予金币")
        if regex_match(ZH_RELICS_PATTERNS, combined_text):
            tags.append("offers_relic")
            reward_hints.append("可能赠予遗物")
        if regex_match(ZH_CARDS_PATTERNS, combined_text):
            tags.append("offers_cards")
            reward_hints.append("可能赠予卡牌")
        if "ANCIENT" in anc_id.upper():
            tags.append("ancient_family")

        entry: dict[str, Any] = {
            "id": anc_id,
            "title_zh": title_zh,
            "title_en": title_en,
            "epithet_zh": epithet_zh,
            "epithet_en": epithet_en,
            "ancient_talk_count": len(ancient_talks),
            "talk_sample_zh": ancient_talks[0] if ancient_talks else None,
            "tags": tags,
            "reward_hints_zh": reward_hints,
            "note_zh": "；".join(reward_hints) if reward_hints else "先古 NPC，具体奖励见遗物层数据",
            "note_en": "; ".join(reward_hints) if reward_hints else "Ancient NPC, see relic-layer data for exact rewards",
        }
        results.append(entry)

    return results


# ---------------------------------------------------------------------------
# 从 events.json 提取（普通事件中可能影响金币/多遗物的行为）
# ---------------------------------------------------------------------------
def scan_events(data_zh: dict[str, Any], data_en: dict[str, Any]) -> list[dict]:
    """
    遍历 events.json，筛出含金币/多遗物/献祭奖励的事件。
    返回列表中每项对应 schema_version=1 的 event_effects 元素。
    """
    results: list[dict] = []
    seen_ids: set[str] = set()

    for key in data_zh:
        if "." not in key:
            continue
        event_id, field = key.split(".", 1)
        if event_id in seen_ids:
            continue

        title_zh = data_zh.get(f"{event_id}.title", "")
        en_title = data_en.get(f"{event_id}.title", "")

        # 收集该事件所有文本
        all_zh: list[str] = []
        for k, v in data_zh.items():
            if k.startswith(f"{event_id}."):
                if isinstance(v, str):
                    all_zh.append(v)

        combined_zh = " ".join(all_zh)

        if not (title_zh or combined_zh):
            continue

        has_gold    = regex_match(ZH_GOLD_PATTERNS, combined_zh)
        has_cards   = regex_match(ZH_CARDS_PATTERNS, combined_zh)
        has_relics  = regex_match(ZH_RELICS_PATTERNS, combined_zh)
        has_foreign = regex_match(ZH_FOREIGN_PATTERNS, combined_zh)
        has_sac     = regex_match(ZH_SACRIFICE_PATTERNS, combined_zh)

        if not any([has_gold, has_cards, has_relics, has_foreign, has_sac]):
            continue

        seen_ids.add(event_id)

        tags: list[str] = []
        effects: dict = {}

        if has_gold:
            tags.append("offers_gold")
            effects["gold_variable"] = True
        if has_cards:
            tags.append("offers_cards")
            effects["cards_reward"] = True
        if has_relics:
            tags.append("offers_relic")
            effects["relic_reward"] = True
        if has_foreign:
            tags.append("foreign_character_cards")
            effects["foreign_character_cards"] = True
        if has_sac:
            tags.append("card_sacrifice")
            effects["card_sacrifice"] = True

        note_zh_parts = []
        if has_gold: note_zh_parts.append("可变金币")
        if has_cards: note_zh_parts.append("卡牌奖励")
        if has_relics: note_zh_parts.append("遗物奖励")
        if has_foreign: note_zh_parts.append("异色卡")
        if has_sac: note_zh_parts.append("献祭奖励")

        entry: dict[str, Any] = {
            "id": event_id,
            "title_zh": title_zh or event_id,
            "title_en": en_title or event_id,
            "tags": tags,
            "effects": effects,
            "note_zh": "、".join(note_zh_parts),
            "note_en": ", ".join(p.lower() for p in note_zh_parts),
        }
        results.append(entry)

    return results


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------
def main():
    import argparse
    parser = argparse.ArgumentParser(description="Extract ancient-related candidates from SL2 localization")
    parser.add_argument("--lang", default=DEFAULT_LANG, choices=["zhs", "eng"])
    parser.add_argument("--output", type=Path, default=OUTPUT_PATH)
    args = parser.parse_args()

    lang = args.lang
    lang_en = "eng"

    print(f"[extract] Loading localization files (lang={lang})...")
    relics_zh  = load_json(lang, "relics.json")
    relics_en  = load_json(lang_en, "relics.json")
    ancients_zh= load_json(lang, "ancients.json")
    ancients_en= load_json(lang_en, "ancients.json")
    events_zh  = load_json(lang, "events.json")
    events_en  = load_json(lang_en, "events.json")

    print("[extract] Scanning relics.json...")
    relic_effects = scan_relics(relics_zh, relics_en)
    print(f"       -> {len(relic_effects)} relics with relevant effects")

    print("[extract] Scanning ancients.json...")
    ancient_npcs = scan_ancients(ancients_zh, ancients_en)
    print(f"       -> {len(ancient_npcs)} ancient NPCs")

    print("[extract] Scanning events.json...")
    event_effects = scan_events(events_zh, events_en)
    print(f"       -> {len(event_effects)} events with relevant effects")

    # 节点覆盖：基于现有代码策略
    node_type_overrides = [
        {
            "match": {"map_point_type": "ancient"},
            "non_shop_gold_rule": "skip",
            "relic_pick_ceiling": 10,
            "note_zh": "先古祭坛：多段遗物/变量 Gold 共存于同一节点；整段跳过非商店大额金币检测",
            "note_en": "Ancient altars host variable gold + multi-relic flows; skip NonShopLargeGold check entirely"
        },
        {
            "match": {"map_point_type": "monster"},
            "relic_pick_ceiling": 1,
            "note_zh": "战斗节点 relic_choices 最多 1 次；PAELS_WING 献祭卡包换遗物时可达上限",
            "note_en": "Monster nodes: max 1 relic pick; PAELS_WING card-sacrifice-to-relic is the only known legit case"
        },
        {
            "match": {"map_point_type": "treasure"},
            "relic_pick_ceiling": 1,
            "note_zh": "藏宝图节点 relic_choices 通常 1 次",
            "note_en": "Treasure nodes: max 1 relic pick"
        }
    ]

    output: dict[str, Any] = {
        "schema_version": 1,
        "game_version_range": ">=0.99",
        "generated_at": datetime.now().strftime("%Y-%m-%d"),
        "generated_by": "tools/extract_ancient_candidates.py",
        "source_lang": lang,
        "relic_effects": relic_effects,
        "ancient_npcs": ancient_npcs,
        "event_effects": event_effects,
        "node_type_overrides": node_type_overrides,
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open(encoding="utf-8", newline="\n") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)

    print(f"\n[extract] Done -> {args.output}")
    print(f"  relic_effects : {len(relic_effects)}")
    print(f"  ancient_npcs  : {len(ancient_npcs)}")
    print(f"  event_effects : {len(event_effects)}")
    print(f"  node_overrides: {len(node_type_overrides)}")


if __name__ == "__main__":
    main()
