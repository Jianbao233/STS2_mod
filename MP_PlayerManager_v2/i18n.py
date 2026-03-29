# -*- coding: utf-8 -*-
"""本地化模块：翻译字典 + 自动检测系统语言"""

import locale
from typing import Optional

# ── 支持的语言 ────────────────────────────────────────────────────────────────
LANG_EN = "en"
LANG_ZH = "zh"
LANG_DEFAULT = LANG_ZH

# ── 翻译字典 ──────────────────────────────────────────────────────────────────
# 结构：{ "key": {"en": "English", "zh": "中文"}, ... }
# 值中出现 {0}, {1}, ... 为位置参数

TRANSLATIONS: dict[str, dict[str, str]] = {

    # === App ===
    "app.title":  {"en": "MP_PlayerManager v2", "zh": "MP_PlayerManager v2"},
    "app.exit_hint": {"en": "Press Enter to exit...", "zh": "按 Enter 退出..."},

    # === Navigation ===
    "nav.menu":         {"en": "Menu",                       "zh": "功能菜单"},
    "nav.save_select":  {"en": "Save Select",                "zh": "存档选择"},
    "nav.takeover":     {"en": "Take Over",                  "zh": "夺舍玩家"},
    "nav.add_player":   {"en": "Add Player",                 "zh": "添加玩家"},
    "nav.remove_player":{"en": "Remove Player",              "zh": "移除玩家"},
    "nav.backup":       {"en": "Backup",                     "zh": "备份管理"},
    "nav.settings":     {"en": "Settings",                   "zh": "设置"},

    # === Save Select ===
    "saveselect.title":    {"en": "Select Save",                                           "zh": "选择存档"},
    "saveselect.not_found": {"en": "No saves found.\n\nPlease confirm:\n  1. Slay the Spire 2 is closed\n  2. You have played multiplayer (modded) at least once\n  3. %APPDATA%\\SlayTheSpire2\\steam\\ exists", "zh": "未找到任何存档。\n\n请确认：\n  1. 已退出《杀戮尖塔2》\n  2. 至少进行过一次多人游戏（modded）\n  3. %APPDATA%\\SlayTheSpire2\\steam\\ 目录存在"},
    "saveselect.rescan":      {"en": "Rescan",                 "zh": "重新扫描"},
    "saveselect.active":       {"en": "In Progress {0}",        "zh": " 进行中 {0} "},
    "saveselect.profile_count":{"en": "{0} save(s)",            "zh": "{0} 份存档"},

    # === Takeover ===
    "takeover.title":            {"en": "Take Over Player",                               "zh": "夺舍玩家"},
    "takeover.subtitle":         {"en": "Select a non-host player to transfer game state to a new player. The host (Player 1) is protected and cannot be taken over.", "zh": "选择一名非房主玩家，将游戏状态交接给新玩家。房主（玩家1）受保护，无法被夺舍。"},
    "takeover.select_save":      {"en": "Please select a save in \"Save Select\" first", "zh": "请先在「存档选择」中选择存档"},
    "takeover.host":             {"en": "[Host]",                                          "zh": "[房主]"},
    "takeover.successor":        {"en": "Successor Info",                                  "zh": "接替者信息"},
    "takeover.irreversible":     {"en": "This action is irreversible. Original player data will be overwritten.", "zh": "操作不可逆，夺舍后原玩家数据将被覆盖"},
    "takeover.confirm":          {"en": "Confirm Take Over",                               "zh": "确认夺舍"},
    "takeover.refresh":          {"en": "Refresh Save",                                     "zh": "刷新存档"},
    "takeover.not_selected":     {"en": "Not Selected",                                    "zh": "未选择"},
    "takeover.choose_player":    {"en": "Please select a player to take over",             "zh": "请先选择要夺舍的玩家"},
    "takeover.host_protected":   {"en": "Host Protected",                                  "zh": "房主保护"},
    "takeover.host_protected_msg":{"en": "The host (Player 1) cannot be taken over",        "zh": "房主（玩家1）无法被夺舍"},
    "takeover.choose_steam":      {"en": "Select Steam Player",                             "zh": "请选择 Steam 玩家"},
    "takeover.choose_steam_msg": {"en": "Click the \"Friends\" button to select, or enter Steam64 ID manually.", "zh": "请点击右侧「好友」按钮选择接替者，或手动填写 Steam64 位 ID。"},
    "takeover.id_format":        {"en": "ID Format Error",                                 "zh": "ID 格式错误"},
    "takeover.id_format_msg":    {"en": "Enter a valid Steam64 ID (15-17 digits)",         "zh": "请输入正确的 Steam64 位 ID（15-17位数字）"},
    "takeover.success":          {"en": "Success",                                         "zh": "成功"},
    "takeover.save_failed":      {"en": "Save Failed",                                     "zh": "保存失败"},
    "takeover.save_failed_msg":  {"en": "Save file write failed. Check file permissions.",  "zh": "存档写入失败，请检查文件权限"},
    "takeover.failed":           {"en": "Operation Failed",                               "zh": "操作失败"},

    # === Add Player ===
    "add.title":          {"en": "Add Player",                                     "zh": "添加玩家"},
    "add.copy_mode":      {"en": "Copy Mode (based on existing player)",          "zh": "复制模式（基于现有玩家）"},
    "add.fresh_mode":     {"en": "Template Mode (new character)",                  "zh": "模板模式（全新角色）"},
    "add.new_player":     {"en": "New Player Info",                               "zh": "新玩家信息"},
    "add.source_player":  {"en": "Source Player:",                                "zh": "源玩家："},
    "add.select_char":    {"en": "Select Character:",                            "zh": "选择角色："},
    "add.confirm":        {"en": "Confirm Add",                                   "zh": "确认添加"},
    "add.select_save":    {"en": "Please select a save in \"Save Select\" first", "zh": "请先在「存档选择」中选择存档"},
    "add.choose_steam":   {"en": "Select Steam Player",                          "zh": "请选择 Steam 玩家"},
    "add.choose_steam_msg":{"en": "Click \"Friends\" to select player, or enter Steam64 ID manually.", "zh": "请点击右侧「好友」按钮选择要添加的玩家，或手动填写 Steam64 位 ID。"},
    "add.id_format":      {"en": "ID Format Error",                              "zh": "ID 格式错误"},
    "add.id_conflict":    {"en": "ID Conflict",                                  "zh": "ID 冲突"},
    "add.no_char":        {"en": "Character Not Selected",                       "zh": "未选择角色"},
    "add.no_char_msg":    {"en": "Please select a character first",              "zh": "请先选择要添加的角色"},
    "add.exception":      {"en": "Exception",                                    "zh": "异常"},
    "add.success":        {"en": "Success",                                      "zh": "成功"},
    "add.save_failed":    {"en": "Save Failed",                                  "zh": "保存失败"},
    "add.failed":         {"en": "Operation Failed",                             "zh": "操作失败"},

    # === Remove Player ===
    "remove.title":           {"en": "Remove Player",                               "zh": "移除玩家"},
    "remove.subtitle":        {"en": "Select a player to remove (offline/disconnected). All their data records will be cleaned up.", "zh": "选择要移除的玩家（离线/退出玩家），将清理其在所有数据中的记录。"},
    "remove.confirm_btn":     {"en": "Confirm Removal",                            "zh": "确认移除"},
    "remove.select_save":     {"en": "Please select a save in \"Save Select\" first", "zh": "请先在「存档选择」中选择存档"},
    "remove.not_selected":    {"en": "Not Selected",                               "zh": "未选择"},
    "remove.choose":         {"en": "Please select a player to remove",           "zh": "请先选择要移除的玩家"},
    "remove.irreversible":   {"en": "This action is irreversible. The player will disappear from multiplayer. This cannot be undone.", "zh": "移除后游戏内该玩家将从多人对局中消失，此操作不可撤销。"},
    "remove.confirm_title":   {"en": "Confirm Removal",                            "zh": "确认移除"},
    "remove.confirm_msg":     {"en": "Remove this player?\nThis cannot be undone.", "zh": "确定要移除该玩家吗？\n此操作不可撤销。"},
    "remove.success":         {"en": "Success",                                    "zh": "成功"},
    "remove.save_failed":     {"en": "Save Failed",                               "zh": "保存失败"},
    "remove.failed":          {"en": "Operation Failed",                          "zh": "操作失败"},

    # === Backup ===
    "backup.title":            {"en": "Backup Management",            "zh": "备份管理"},
    "backup.select_save":       {"en": "Please load a save in \"Save Select\" first", "zh": "请先在「存档选择」中加载存档"},
    "backup.create_now":        {"en": "Create Backup Now",           "zh": "立即创建备份"},
    "backup.history":           {"en": "Backup History",               "zh": "备份历史"},
    "backup.created":           {"en": "Backup Created",              "zh": "备份成功"},
    "backup.restore":           {"en": "Restore",                     "zh": "恢复"},
    "backup.restore_confirm":   {"en": "Confirm Restore",             "zh": "确认恢复"},
    "backup.restore_success":   {"en": "Save Restored",               "zh": "恢复成功"},
    "backup.restore_msg":       {"en": "Save file restored",          "zh": "存档已恢复"},
    "backup.restore_failed":    {"en": "Restore Failed",             "zh": "恢复失败"},
    "backup.restore_perm":      {"en": "Check file permissions",      "zh": "请检查文件权限"},

    # === Settings ===
    "settings.title":           {"en": "Settings",                                             "zh": "设置"},
    "settings.font_size":        {"en": "UI Font Size",                                        "zh": "界面字体大小"},
    "settings.font_hint":        {"en": "Drag to switch between 10 font size levels. Page refreshes on release. Settings are for this session only.", "zh": "拖动滑块切换 10 个字号档位；松手后刷新当前页。设置仅在本次运行有效。"},
    "settings.font_min":         {"en": "Base",                                                "zh": "基准"},
    "settings.font_max":         {"en": "Max",                                                 "zh": "最大"},
    "settings.font_current":     {"en": "Level {0} / {1}  Scale {2:.0f}%",                     "zh": "当前：第 {0} / {1} 档　缩放 {2:.0f}%"},
    "settings.mods_dir":         {"en": "Game Mods Directory",                                  "zh": "游戏 Mod 目录"},
    "settings.tool_dir":         {"en": "Tool Directory",                                      "zh": "工具目录"},
    "settings.current_save":     {"en": "Current Save",                                        "zh": "当前存档"},
    "settings.no_save":         {"en": "Not Loaded",                                          "zh": "未加载"},
    "settings.char_count":      {"en": "{0} characters (built-in + Mod)",                     "zh": "{0} 个（内置 + Mod）"},
    "settings.scan_root":        {"en": "Save Scan Root",                                       "zh": "存档扫描根目录"},
    "settings.default_path":     {"en": "(Default Roaming path)",                               "zh": "(默认 Roaming 路径)"},
    "settings.language":         {"en": "Language",                                            "zh": "语言"},
    "settings.lang_en":          {"en": "English",                                             "zh": "English"},
    "settings.lang_zh":          {"en": "中文",                                                "zh": "中文"},

    # === Common Dialog ===
    "common.ok":     {"en": "OK",     "zh": "确定"},
    "common.cancel": {"en": "Cancel", "zh": "取消"},
    "common.confirm":{"en": "Confirm", "zh": "确认"},
    "common.close":  {"en": "Close",  "zh": "关闭"},

    # === Friend Picker ===
    "friend.title":         {"en": "Select Steam Player",                                    "zh": "选择 Steam 玩家"},
    "friend.loading":       {"en": "Loading Steam friends list...",                          "zh": "正在读取 Steam 好友列表…"},
    "friend.hint":          {"en": "Friends from local Steam; 15 rows visible, scroll or use sidebar", "zh": "好友来自本机 Steam；列表仅绘制 15 行，滚轮或右侧条浏览"},
    "friend.search":         {"en": "Search nickname or Steam ID...",                         "zh": "搜索昵称或 Steam ID…"},
    "friend.cancel":        {"en": "Cancel",                                                 "zh": "取消"},
    "friend.select":        {"en": "Select",                                                 "zh": "选择"},
    "friend.no_nickname":   {"en": "(No nickname)",                                          "zh": "（昵称待查）"},
    "friend.online_game":   {"en": "Online: {0}",                                           "zh": "在线：{0}"},
    "friend.online":        {"en": "Online",                                                 "zh": "在线"},
    "friend.offline":       {"en": "Offline",                                                "zh": "离线"},
    "friend.friends_btn":   {"en": "Friends",                                                "zh": "好友"},

    # === Status Bar ===
    "status.scanning":   {"en": "Scanning saves...",                                          "zh": "正在扫描存档..."},
    "status.ready":      {"en": "Ready  ·  {0} saves  ·  {1} characters",                    "zh": "就绪  ·  {0} 份存档  ·  {1} 个角色"},
    "status.no_save":   {"en": "No save loaded",                                          "zh": "未加载存档"},
    "status.info":      {"en": "Save: {0} players · Act {1} · Asc {2} · {3}",              "zh": "存档：{0}名玩家 · 第{1}幕 · 进阶{2} · {3}"},
    "status.no_players":{"en": "No players",                                                 "zh": "无玩家"},
    "status.list_sep":  {"en": ", ",                                                       "zh": "、"},
    "status.init_error": {"en": "Initialization error: {0}",                                 "zh": "初始化出错: {0}"},

    # === Save Card Info ===
    "card.mod":            {"en": "[Mod]",                                               "zh": "[mod]"},
    "card.standard":        {"en": "[Standard]",                                           "zh": "[标准]"},
    "card.players":         {"en": "{0} player(s)",                                       "zh": "{0} 名玩家"},
    "card.act":             {"en": "Act {0}",                                             "zh": "第{0}幕"},
    "card.asc":             {"en": "Asc {0}",                                             "zh": "进阶 {0}"},
    "card.hp":              {"en": "HP {0}/{1}",                                          "zh": "生命 {0}/{1}"},
    "card.gold":            {"en": "Gold {0}",                                            "zh": "金币 {0}"},
    "card.cards":           {"en": "{0} card(s)",                                         "zh": "{0} 张牌"},
    "card.relics":          {"en": "{0} relic(s)",                                        "zh": "{0} 件遗物"},
    "card.potions":         {"en": "{0} potion(s)",                                       "zh": "{0} 瓶药水"},
    "card.relics_list":     {"en": "Relics: {0}",                                        "zh": "遗物：{0}"},
    "card.hp_label":        {"en": "HP {0}",                                              "zh": "HP {0}"},
    "card.mod_tag":         {"en": "[Mod]",                                               "zh": "[Mod]"},
    "card.steam_name_not_found": {"en": "[Steam name not found for this player]",          "zh": "[未检索到该玩家Steam名称]"},
    "card.no_player":       {"en": "?",                                                   "zh": "?"},
    "card.host_sep":        {"en": "  ·  ",                                              "zh": "  ·  "},
    "card.stat_sep":        {"en": "  ·  ",                                              "zh": "  ·  "},

    # === Core Result Messages ===
    "result.takeover.idx_out_of_range": {"en": "Player index {0} out of range (current: {1} players)", "zh": "玩家序号 {0} 超出范围（当前共 {1} 名玩家）"},
    "result.takeover.success":           {"en": "Successfully took over player {0} (ID: {1} → {2})",    "zh": "成功夺舍玩家 {0}（原 ID: {1} → 新 ID: {2}）"},
    "result.add.idx_out_of_range":        {"en": "Source player index {0} out of range",                  "zh": "源玩家序号 {0} 超出范围"},
    "result.add.id_conflict":            {"en": "net_id {0} already exists in this save",                "zh": "net_id {0} 已存在于本存档中"},
    "result.add.copy_success":           {"en": "Added player (copy mode): ID={0}, char={1}",             "zh": "成功添加玩家（复制模式）：ID={0}，角色={1}"},
    "result.add.fresh_success":          {"en": "Added player (template mode): ID={0}, char={1}",        "zh": "成功添加玩家（初始牌组模式）：ID={0}，角色={1}"},
    "result.remove.idx_out_of_range":    {"en": "Player index {0} out of range",                        "zh": "玩家序号 {0} 超出范围"},
    "result.remove.success":             {"en": "Removed player {0} ({1}, ID: {2})",                     "zh": "成功移除玩家 {0}（{1}，ID: {2}）"},

    # === Character Names (built-in) ===
    "char.ironclad":    {"en": "Ironclad",     "zh": "铁甲战士"},
    "char.silent":      {"en": "Silent",         "zh": "静默猎手"},
    "char.defect":      {"en": "Defect",         "zh": "故障机器人"},
    "char.necrobinder": {"en": "Necrobinder",    "zh": "亡灵契约师"},
    "char.regent":      {"en": "Regent",         "zh": "储君"},

    # === Backup UI Details ===
    "backup.info":              {"en": "Path: {0}\nPlayers: {1} · Asc {2} · Act {3}",       "zh": "路径：{0}\n玩家：{1} 名  ·  进阶 {2}  ·  第{3}幕"},
    "backup.item":               {"en": "{0}  ·  {1} player(s)",                               "zh": "{0}  ·  {1} 名玩家"},
    "backup.created_fmt":        {"en": "Backup created:\n{0}",                               "zh": "已创建备份：\n{0}"},
    "backup.restore_confirm_msg":{"en": "Restore save to {0}?",                               "zh": "将存档恢复到 {0}？"},
    "backup.person_count":       {"en": "{0} player(s)",                                       "zh": "{0} 名玩家"},
    "friend.person_count":      {"en": "{0} person(s)",                                      "zh": "{0} 人"},
    "friend.person_count_filtered": {"en": "{0} / {1} person(s)",                            "zh": "{0} / {1} 人"},
    "saveselect.tag_in_progress": {"en": "  In Progress {0}  ",                              "zh": "  进行中 {0}  "},
    "card.info":               {"en": "{0} player(s)  ·  Act {1}  ·  Asc {2}  ·  {3}  ·  {4}", "zh": "{0} 名玩家  ·  第{1}幕  ·  进阶 {2}  ·  {3}  ·  {4}"},
    "char.display.unknown":     {"en": "?",                                                   "zh": "?"},
    "card.profile_mode":       {"en": "{0}  [{1}]",                                           "zh": "{0}  [{1}]"},
    "char.display.steam_not_found": {"en": "[Steam name not found for this player]",           "zh": "[未检索到该玩家Steam名称]"},
    "char.display.format":        {"en": "{0} ({1})",                                        "zh": "{0}（{1}）"},
    "status.id_label":         {"en": "ID: ",                                               "zh": "ID："},
    "status.nickname_label":  {"en": "Nickname: ",                                          "zh": "昵称："},
    "card.status_in_progress": {"en": "In Progress",                                        "zh": "进行中"},
    "card.status_not_started": {"en": "Not Started",                                         "zh": "未开始"},

    # === Backup Browser ===
    "backup.all.title":      {"en": "All Backups",                                              "zh": "全部备份"},
    "backup.all.subtitle":   {"en": "Browse and restore backups across all saves",              "zh": "浏览和恢复所有存档的备份"},
    "backup.current.title":  {"en": "Current Save Backups",                                     "zh": "当前存档备份"},
    "backup.current.subtitle":{"en": "Backups of the currently loaded save",                    "zh": "当前已加载存档的备份"},
    "backup.no_backups":     {"en": "No backups found for this save",                          "zh": "该存档暂无备份"},
    "backup.no_backups_global":{"en": "No backups found across all saves",                      "zh": "全服所有存档均无备份"},
    "backup.backup_count":   {"en": "{0} backup(s)",                                           "zh": "{0} 个备份"},
    "backup.restore_confirm2":{"en": "Restore backup {0}?",                                      "zh": "恢复备份 {0}？"},
    "backup.restore_success2":{"en": "Backup restored to:\n{0}",                                 "zh": "已恢复到：\n{0}"},
    "backup.view_save":      {"en": "Go to Save",                                               "zh": "跳转存档"},
    "backup.loading":        {"en": "Scanning backups...",                                       "zh": "正在扫描备份..."},
    "backup.profile_count":  {"en": "{0} backup(s)",                                           "zh": "{0} 个备份"},
    "backup.rescan":        {"en": "Rescan",                                                    "zh": "重新扫描"},
    "backup.group_header":   {"en": "{0}  ·  {1} backup(s)",                                   "zh": "{0}  ·  {1} 个备份"},
    "backup.delete":         {"en": "Delete",                                                  "zh": "删除"},
    "backup.delete_confirm": {"en": "Confirm Delete",                                          "zh": "确认删除"},
    "backup.delete_confirm2":{"en": "Permanently delete this backup?\n{0}\n{1}",               "zh": "确定永久删除该备份文件？\n{0}\n{1}"},
    "backup.delete_success": {"en": "Backup Deleted",                                          "zh": "备份已删除"},
    "backup.delete_success2":{"en": "Removed:\n{0}",                                         "zh": "已删除：\n{0}"},
    "backup.delete_failed":  {"en": "Delete Failed",                                           "zh": "删除失败"},
}

# ── 当前语言（运行时可切换）───────────────────────────────────────────────────────
_current_lang: str = LANG_DEFAULT


def _detect_system_lang() -> str:
    """自动检测 Windows 系统 UI 语言"""
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                             r"Control Panel\Desktop") as key:
            lang_id, _ = winreg.QueryValueEx(key, "MultiUILanguageId")
        if lang_id == 0x0804:
            return LANG_ZH
        return LANG_EN
    except Exception:
        pass

    try:
        lang, _ = locale.getpreferredencoding(False).split("_")
        if lang.lower().startswith("zh"):
            return LANG_ZH
    except Exception:
        pass

    return LANG_DEFAULT


def init(lang: Optional[str] = None) -> str:
    """初始化语言。lang=None 时自动检测。返回最终语言代码。"""
    global _current_lang
    if lang is None:
        _current_lang = _detect_system_lang()
    else:
        _current_lang = lang if lang in (LANG_EN, LANG_ZH) else LANG_DEFAULT
    return _current_lang


def current_lang() -> str:
    return _current_lang


def set_lang(lang: str) -> str:
    """切换语言，返回最终语言代码"""
    global _current_lang
    _current_lang = lang if lang in (LANG_EN, LANG_ZH) else LANG_DEFAULT
    return _current_lang


def _(key: str, *args) -> str:
    """翻译函数：TRANSLATIONS[key][当前语言]，支持 positional args"""
    if key not in TRANSLATIONS:
        return key

    text = TRANSLATIONS[key].get(
        _current_lang,
        TRANSLATIONS[key].get(LANG_DEFAULT, key),
    )

    if args:
        try:
            return text.format(*args)
        except (IndexError, KeyError):
            return text
    return text


def available_langs() -> list[tuple[str, str]]:
    """返回 [(lang_code, display_name), ...]"""
    return [
        (LANG_EN, TRANSLATIONS["settings.lang_en"][LANG_EN]),
        (LANG_ZH, TRANSLATIONS["settings.lang_zh"][LANG_ZH]),
    ]
