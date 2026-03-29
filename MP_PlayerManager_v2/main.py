# -*- coding: utf-8 -*-
"""
MP_PlayerManager v2 — GUI 主程序
CustomTkinter 现代化暗色风格界面
"""

import json
import os
import sys
import traceback
import webbrowser
from dataclasses import dataclass
from pathlib import Path

import customtkinter as ctk
from i18n import _, init as i18n_init, set_lang, current_lang, available_langs

# ── 项目模块（绝对导入，兼容 run.py 直接执行） ──────────────────────────────
import MP_PlayerManager_v2.save_io as save_io
import MP_PlayerManager_v2.characters as characters
import MP_PlayerManager_v2.core as core
import MP_PlayerManager_v2.steam_api as steam_api

# ── CustomTkinter 全局配置 ───────────────────────────────────────────────────
ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")

# ── 常量 ─────────────────────────────────────────────────────────────────────
APP_NAME = "MP_PlayerManager v2"
WINDOW_W, WINDOW_H = 1360, 860
NAV_W = 200
# 备份页内嵌滚动区高度：避免与「当前存档备份」两个 expand=True 互相平分导致「全部备份」过矮
BACKUP_GLOBAL_SCROLL_H = max(440, min(700, int(WINDOW_H * 0.65)))
BACKUP_CURRENT_SCROLL_H = max(160, int(WINDOW_H * 0.22))


# ── 存档分组数据结构 ─────────────────────────────────────────────────────────
@dataclass
class SteamUserGroup:
    """按 Steam 用户分组的存档集合"""
    steam_id: str
    steam_id_short: str      # 截断后的短 ID，始终显示
    persona: str               # 昵称，从 steam_names.json 或 loginusers.vdf 查得，可能为空
    profiles: list             # list[save_io.SaveProfile]
# 字体缩放：10 档，基准为原「特大」1.30（第 1 档），向上扩 6 档共 10 档
# 第 1 档 = 1.30，第 10 档 = 1.30 + 6 × 0.075 = 1.75
FONT_SCALE_MIN   = 1.30    # 第 1 档（原「特大」）
FONT_SCALE_STEP  = 0.075  # 每升一档加 0.075
FONT_SCALE_LEVELS = 10
FONT_SCALE = FONT_SCALE_MIN + (5 - 1) * FONT_SCALE_STEP  # 默认第 5 档（1.60）


def _font_scale_from_level(level: int) -> float:
    """level: 1 … FONT_SCALE_LEVELS"""
    lv = max(1, min(FONT_SCALE_LEVELS, int(level)))
    return FONT_SCALE_MIN + (lv - 1) * FONT_SCALE_STEP


def _font_level_from_scale(scale: float) -> int:
    t = (scale - FONT_SCALE_MIN) / FONT_SCALE_STEP
    lv = int(round(t)) + 1
    return max(1, min(FONT_SCALE_LEVELS, lv))

GAME_MODS_DIR = os.environ.get(
    "STEAM_MODS",
    r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods",
)

# ── Steam 好友选择对话框（本地 Steam 配置；纯 CTk + pack，无 Canvas 嵌套）────
# 参考 CustomTkinter 官方用法：避免在 tk.Canvas 中嵌入 CTk 控件，减轻合成残影与卡顿
# （仓库：github.com/TomSchimansky/CustomTkinter — ScrollableFrame / issue 讨论）


class FriendPickerDialog(ctk.CTkToplevel):
    """
    Steam 好友选择弹窗。
    固定 15 行可复用列表；滚动条与滚轮只改首行索引 _first，仅 configure + pack。
    """

    ITEM_H = 64
    VISIBLE_ROWS = 15
    DEBOUNCE_MS = 180

    def __init__(self, parent, tool_dir: str,
                 on_select=None,
                 font_scale: float = 1.0):
        super().__init__(parent)
        self.title(_("friend.title"))
        self.geometry("680x520")
        self.resizable(False, False)
        self.grab_set()
        self.selected_id: str | None = None
        self._on_select = on_select
        self._font_scale = font_scale
        self._tool_dir = tool_dir
        self._contacts: list = []
        self._all_contacts: list = []
        self._debounce_after_id: str | None = None
        self._row_slots: list = []
        self._first = 0

        self.update_idletasks()
        sw = self.winfo_screenwidth()
        sh = self.winfo_screenheight()
        self.geometry(f"680x520+{(sw-680)//2}+{(sh-520)//2}")

        self._build_ui()
        self._load_contacts_async()

    def _fs(self, size: float) -> ctk.CTkFont:
        return ctk.CTkFont(
            size=int(size * self._font_scale),
            family="Microsoft YaHei",
        )

    def _load_contacts_async(self):
        import threading
        t = threading.Thread(target=self._load_contacts_bg, daemon=True)
        t.start()

    def _load_contacts_bg(self):
        import MP_PlayerManager_v2.steam_api as _sa
        try:
            contacts = _sa.get_all_contacts(self._tool_dir)
        except Exception:
            contacts = []
        for c in contacts:
            c._filter_nick = (c.nickname or "").lower()
            c._filter_id   = c.steam_id.lower()
        contacts.sort(key=lambda c: (c.online is False, c._filter_nick))
        self.after(0, self._on_contacts_loaded, contacts)

    def _on_contacts_loaded(self, contacts):
        self._all_contacts = contacts
        self._contacts = list(contacts)
        if hasattr(self, "_count_lbl") and self._count_lbl.winfo_exists():
            self._count_lbl.configure(text=_("friend.person_count", len(contacts)))
        if hasattr(self, "_loading_lbl") and self._loading_lbl.winfo_exists():
            self._loading_lbl.destroy()
        if hasattr(self, "_hint_lbl") and self._hint_lbl.winfo_exists():
            self._hint_lbl.configure(
                text=_("friend.hint"),
            )
        self._first = 0
        self._ensure_row_slots()
        self.after_idle(self._refresh_rows_and_scrollbar)

    def _build_ui(self):
        list_row = ctk.CTkFrame(self, fg_color="transparent")
        list_row.pack(fill="both", expand=True, padx=16, pady=12)

        title_row = ctk.CTkFrame(list_row, fg_color="transparent")
        title_row.pack(fill="x", pady=(0, 8))

        ctk.CTkLabel(
            title_row, text=_("friend.title"),
            font=self._fs(14),
            text_color=("#F5A623", "#F5A623"),
        ).pack(side="left")

        self._count_lbl = ctk.CTkLabel(
            title_row, text=_("friend.loading"),
            font=self._fs(10),
            text_color=("#6B7280", "#6B7280"),
        )
        self._count_lbl.pack(side="right")

        self._loading_lbl = ctk.CTkLabel(
            list_row,
            text=_("friend.loading"),
            font=self._fs(10),
            text_color=("#9CA3AF", "#9CA3AF"),
        )
        self._loading_lbl.pack(pady=10)

        search_frame = ctk.CTkFrame(list_row, fg_color="transparent")
        search_frame.pack(fill="x", pady=(0, 8))

        self._search_var = ctk.StringVar()
        search_entry = ctk.CTkEntry(
            search_frame,
            textvariable=self._search_var,
            placeholder_text=_("friend.search"),
            height=34, corner_radius=6,
        )
        search_entry.pack(fill="x")
        self._search_var.trace_add("write", self._on_search_changed)

        list_area = ctk.CTkFrame(list_row, fg_color="transparent")
        list_area.pack(fill="both", expand=True, pady=(0, 8))

        self._viewport = ctk.CTkFrame(list_area, fg_color="transparent")
        self._viewport.pack(side="left", fill="both", expand=True)

        self._scrollbar = ctk.CTkScrollbar(
            list_area, command=self._view_command,
            orientation="vertical",
            fg_color="#2A2A2A",
            width=14,
            button_color="#2A3F7A",
            button_hover_color="#3A5FAA",
        )
        self._scrollbar.pack(side="right", fill="y", padx=(4, 0))

        for seq in ("<MouseWheel>", "<Button-4>", "<Button-5>"):
            self._viewport.bind(seq, self._on_scroll)
            list_area.bind(seq, self._on_scroll)
            search_frame.bind(seq, self._on_scroll)

        footer = ctk.CTkFrame(list_row, fg_color="transparent")
        footer.pack(fill="x")

        self._hint_lbl = ctk.CTkLabel(
            footer,
            text=_("friend.loading"),
            font=self._fs(9),
            text_color=("#6B7280", "#6B7280"),
        )
        self._hint_lbl.pack(side="left")

        ctk.CTkButton(
            footer, text=_("friend.cancel"), width=80, height=32, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            font=self._fs(10), command=self.destroy,
        ).pack(side="right")

    def _clamp_first(self) -> None:
        n = len(self._contacts)
        vis = self.VISIBLE_ROWS
        if n <= vis:
            self._first = 0
            return
        mf = n - vis
        self._first = max(0, min(mf, self._first))

    def _sync_scrollbar(self) -> None:
        n = len(self._contacts)
        vis = self.VISIBLE_ROWS
        if n <= vis:
            self._scrollbar.set(0, 1)
            return
        lo = self._first / n
        hi = (self._first + vis) / n
        self._scrollbar.set(lo, hi)

    def _view_command(self, *args):
        n = len(self._contacts)
        vis = self.VISIBLE_ROWS
        if n <= vis:
            self._first = 0
            self._refresh_rows()
            self._sync_scrollbar()
            return
        if args[0] == "moveto":
            f = float(args[1])
            self._first = int(round(f * n))
        elif args[0] == "scroll":
            self._first += int(args[1])
        self._clamp_first()
        self._refresh_rows()
        self._sync_scrollbar()

    def _on_scroll(self, event):
        n = len(self._contacts)
        vis = self.VISIBLE_ROWS
        if n <= vis:
            return "break"
        step = 3
        if getattr(event, "delta", 0):
            d = -int(event.delta / 120) * step
        elif getattr(event, "num", 0) == 4:
            d = -step
        elif getattr(event, "num", 0) == 5:
            d = step
        else:
            d = 0
        self._first += d
        self._clamp_first()
        self._refresh_rows()
        self._sync_scrollbar()
        return "break"

    def _ensure_row_slots(self):
        if self._row_slots:
            return
        for _ in range(self.VISIBLE_ROWS):
            self._row_slots.append(self._create_row_shell())

    def _create_row_shell(self):
        row_h = self.ITEM_H - 4
        item = ctk.CTkFrame(
            self._viewport,
            corner_radius=8,
            fg_color=("#1F3460", "#1F3460"),
            height=row_h,
        )
        item.pack_propagate(False)
        item.bind(
            "<Enter>",
            lambda e, f=item: f.configure(fg_color=("#2A5090", "#2A5090")),
        )
        item.bind(
            "<Leave>",
            lambda e, f=item: f.configure(fg_color=("#1F3460", "#1F3460")),
        )

        dot = ctk.CTkFrame(item, width=8, height=8, corner_radius=4, fg_color="#6B7280")
        dot.pack(side="left", padx=(12, 8), pady=10)

        info = ctk.CTkFrame(item, fg_color="transparent")
        info.pack(side="left", fill="both", expand=True, pady=6)

        nl = ctk.CTkLabel(
            info, text="",
            font=self._fs(11), anchor="w",
        )
        nl.pack(anchor="w")

        sl = ctk.CTkLabel(
            info, text="",
            font=self._fs(8),
            text_color=("#6B7280", "#6B7280"), anchor="w",
        )
        sl.pack(anchor="w")

        btn = ctk.CTkButton(
            item, text=_("friend.select"), width=60, height=28, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            font=self._fs(9),
        )
        btn.pack(side="right", padx=(8, 10), pady=8)

        item._dot = dot
        item._name_lbl = nl
        item._sub_lbl = sl
        item._btn = btn
        item.pack_forget()
        return item

    def _bind_row_data(self, item, contact):
        fid = contact.steam_id
        nickname = contact.nickname or _("friend.no_nickname")
        online = contact.online
        game = contact.game_name

        dot_color = "#27AE60" if online else "#6B7280"
        item._dot.configure(fg_color=dot_color)
        item._name_lbl.configure(text=nickname)
        status_text = _("friend.online_game", game) if (online and game) else (_("friend.online") if online else _("friend.offline"))
        item._sub_lbl.configure(text=_("status.id_label") + fid + "  ·  " + status_text)
        item._btn.configure(command=lambda f=fid, n=nickname: self._select(f, n))

    def _refresh_rows(self):
        if not self._contacts:
            for row in self._row_slots:
                row.pack_forget()
            return

        self._ensure_row_slots()
        self._clamp_first()
        n = len(self._contacts)
        first = self._first

        for slot_i, row in enumerate(self._row_slots):
            idx = first + slot_i
            if idx < n:
                self._bind_row_data(row, self._contacts[idx])
                row.pack(fill="x", pady=(0, 4))
            else:
                row.pack_forget()

    def _refresh_rows_and_scrollbar(self):
        self._refresh_rows()
        self._sync_scrollbar()

    def _on_search_changed(self, *args):
        if self._debounce_after_id is not None:
            self.after_cancel(self._debounce_after_id)
        self._debounce_after_id = self.after(
            self.DEBOUNCE_MS, self._do_filter,
        )

    def _do_filter(self):
        q = self._search_var.get().strip().lower()
        if not q:
            self._contacts = list(self._all_contacts)
        else:
            self._contacts = [
                c for c in self._all_contacts
                if q in c._filter_nick or q in c._filter_id
            ]
        if hasattr(self, "_count_lbl") and self._count_lbl.winfo_exists():
            self._count_lbl.configure(
                text=_("friend.person_count_filtered", len(self._contacts), len(self._all_contacts))
            )
        self._first = 0
        self._refresh_rows_and_scrollbar()

    def _select(self, steam_id: str, nickname: str = ""):
        self.selected_id = steam_id
        if self._on_select:
            self._on_select(steam_id, nickname)
        self.destroy()


class SteamIDInput(ctk.CTkFrame):
    """
    Steam64 位 ID 输入框 + 右侧好友选择按钮。
    支持：输入 ID 后自动显示昵称 | 好友选择弹窗 | ID 可留空（外部自行校验）
    顶部可显示目标玩家的 Steam 昵称（外部调用 set_steam_name）。
    """

    def __init__(self, parent, tool_dir: str,
                 width: int = 300,
                 allow_empty: bool = False,
                 font_scale: float = 1.0,
                 **kwargs):
        super().__init__(parent, fg_color="transparent", **kwargs)
        self.tool_dir = tool_dir
        self._allow_empty = allow_empty
        self._font_scale = font_scale
        self._id_var = ctk.StringVar()
        self._nickname_var = ctk.StringVar()
        self._id_var.trace_add("write", lambda *_: self._on_id_change())

        # 顶部 Steam 昵称（默认隐藏）
        self._name_lbl = ctk.CTkLabel(
            self, text="",
            font=self._fs(11, weight="bold"),
            text_color=("#F5A623", "#F5A623"),
            anchor="w",
        )
        self._name_lbl.pack(anchor="w", pady=(0, 2))

        # 输入行（横向排列）
        input_row = ctk.CTkFrame(self, fg_color="transparent")
        input_row.pack(anchor="w", pady=(0, 2))

        self.entry = ctk.CTkEntry(
            input_row, textvariable=self._id_var,
            width=width, height=36,
            font=self._fs(11, family="Consolas"),
            corner_radius=6,
        )
        self.entry.pack(side="left", fill="x", expand=True)

        self.friend_btn = ctk.CTkButton(
            input_row, text=_("friend.friends_btn"),
            width=70, height=36,
            font=self._fs(10),
            corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            command=self._pick_friend,
        )
        self.friend_btn.pack(side="left", padx=(8, 0))

        # 昵称副标签
        self._nick_lbl = ctk.CTkLabel(
            self, text="",
            font=self._fs(9),
            text_color=("#27AE60", "#27AE60"),
            anchor="w",
        )
        self._nick_lbl.pack(anchor="w")

    def get(self) -> str:
        return self._id_var.get().strip()

    def set(self, value: str):
        self._id_var.set(str(value))

    def set_steam_name(self, name: str):
        """在顶部显示目标玩家的 Steam 昵称"""
        if name:
            self._name_lbl.configure(text=name)
        else:
            self._name_lbl.configure(text="")

    def set_nickname(self, nickname: str):
        """外部/好友弹窗选中后调用，显示昵称提示"""
        if nickname:
            self._nickname_var.set(_("status.nickname_label") + nickname)
            self._nick_lbl.configure(text=_("status.nickname_label") + nickname)

    def _on_id_change(self):
        sid = self._id_var.get().strip()
        if not sid:
            self._nick_lbl.configure(text="")

    def _pick_friend(self):
        def on_select(sid: str, nickname: str):
            self.set(sid)
            self.set_nickname(nickname)
        dlg = FriendPickerDialog(
            self.winfo_toplevel(), self.tool_dir,
            on_select=on_select,
            font_scale=self._font_scale,
        )
        self.wait_window(dlg)

    def _fs(self, size: float, weight: str = "normal",
             family: str = "Microsoft YaHei") -> ctk.CTkFont:
        return ctk.CTkFont(size=int(size * self._font_scale),
                           weight=weight, family=family)


# ── 主应用 ───────────────────────────────────────────────────────────────────


class App(ctk.CTk):
    def __init__(self):
        super().__init__()
        self.tool_dir = str(Path(sys.argv[0]).parent.resolve())
        self.save_path: Path = None
        self.save_data: dict = {}
        self.profiles: list[save_io.SaveProfile] = []
        self.user_groups: list[SteamUserGroup] = []
        self.backups: list[save_io.SaveBackup] = []
        self.all_chars: dict[str, characters.CharacterTemplate] = {}
        self.steam_names: dict[str, str] = {}
        self._page_widgets: dict = {}
        self._current_page: str = ""
        self._font_scale: float = FONT_SCALE
        self.all_backups: list[save_io.BackupEntry] = []

        self._setup_window()
        self._build_layout()
        self._load_initial_data()
        self._i18n_lang = i18n_init()
        self.show_page("save_select")

    # ── 窗口 & 布局 ────────────────────────────────────────────────────────

    def _setup_window(self):
        self.title(APP_NAME)
        self.geometry(f"{WINDOW_W}x{WINDOW_H}")
        self.minsize(1040, 680)

        # 居中
        self.update_idletasks()
        sx = (self.winfo_screenwidth() - WINDOW_W) // 2
        sy = (self.winfo_screenheight() - WINDOW_H) // 2
        self.geometry(f"{WINDOW_W}x{WINDOW_H}+{sx}+{sy}")

    def _build_layout(self):
        # 顶栏
        top = ctk.CTkFrame(self, height=44, corner_radius=0,
                            fg_color=("#1A2A3A", "#1A2A3A"))
        top.pack(fill="x")
        top.pack_propagate(False)

        ctk.CTkLabel(
            top, text=_("app.title"),
            font=self._ctk_font(14, weight="bold"),
            text_color=("#F5A623", "#F5A623"),
        ).pack(side="left", padx=16, pady=8)

        repo_url = "https://github.com/Jianbao233/STS2_mod"
        repo_label = ctk.CTkLabel(
            top, text=repo_url,
            font=self._ctk_font(9),
            text_color=("#3B82F6", "#3B82F6"),
            cursor="hand2",
        )
        repo_label.pack(side="left", padx=(4, 0), pady=8)
        repo_label.bind(
            "<Button-1>",
            lambda _: webbrowser.open(repo_url),
        )

        self._status_bar = ctk.CTkLabel(
            top, text=_("status.scanning"),
            font=self._ctk_font(10),
            text_color=("#A0A0B0", "#A0A0B0"),
        )
        self._status_bar.pack(side="right", padx=16, pady=8)

        # 主区域
        body = ctk.CTkFrame(self, fg_color="transparent")
        body.pack(fill="both", expand=True)

        # 左侧导航
        nav = ctk.CTkFrame(body, width=NAV_W, corner_radius=0,
                           fg_color=("#111827", "#111827"))
        nav.pack(fill="y", side="left")
        nav.pack_propagate(False)

        nav_title = ctk.CTkLabel(
            nav,             text=_("nav.menu"),
            font=self._ctk_font(11, weight="bold"),
            text_color=("#6B7280", "#6B7280"),
        )
        nav_title.pack(anchor="w", padx=16, pady=(16, 6))

        self._nav_buttons = {}
        nav_items = [
            ("save_select",  "📂  " + _("nav.save_select")),
            ("takeover",     "👤  " + _("nav.takeover")),
            ("add_player",   "➕  " + _("nav.add_player")),
            ("remove_player","➖  " + _("nav.remove_player")),
            ("backup",       "💾  " + _("nav.backup")),
            ("settings",     "⚙  " + _("nav.settings")),
        ]
        for key, text in nav_items:
            btn = ctk.CTkButton(
                nav, text=text,
                font=self._ctk_font(11),
                anchor="w", height=42,
                corner_radius=6,
                fg_color="transparent",
                hover_color=("#1F3460", "#1F3460"),
                text_color=("#D1D5DB", "#D1D5DB"),
                command=lambda k=key: self.show_page(k),
            )
            btn.pack(fill="x", padx=8, pady=2)
            self._nav_buttons[key] = btn

        # ── 语言切换（版本号上方）────────────────────────────────────────────
        lang_bar = ctk.CTkFrame(nav, fg_color=("#0D1117", "#0D1117"), height=36)
        lang_bar.pack(fill="x", padx=4, pady=(0, 0))
        lang_bar.pack_propagate(False)

        self._nav_lang_var = ctk.StringVar(value=current_lang())

        def _make_lang_cmd(code):
            def _():
                set_lang(code)
                self.show_page(self._current_page)
            return _

        for code, lbl in available_langs():
            rb = ctk.CTkRadioButton(
                lang_bar, text=lbl,
                variable=self._nav_lang_var, value=code,
                command=_make_lang_cmd(code),
                radiobutton_width=14, radiobutton_height=14,
                font=self._ctk_font(9),
                fg_color=("#0D1117", "#0D1117"),
                bg_color=("#0D1117", "#0D1117"),
                text_color=("#9CA3AF", "#9CA3AF"),
            )
            rb.pack(side="left", padx=(8, 4))

        # 版本号（最底部）
        ctk.CTkLabel(nav, text="v2.1.0",
                     font=self._ctk_font(9),
                     text_color=("#4B5563", "#4B5563"),
        ).pack(pady=(4, 8))

        # 右侧内容区
        self._content = ctk.CTkFrame(body, fg_color="transparent", corner_radius=0)
        self._content.pack(fill="both", expand=True, side="right")

    # ── 数据加载 ───────────────────────────────────────────────────────────

    def _load_initial_data(self):
        try:
            self.all_chars = characters.get_all_characters(GAME_MODS_DIR)
            self.profiles = save_io.scan_save_profiles()
            self.all_backups = save_io.scan_all_backups()
            self._build_user_groups()
            self._status_bar.configure(
                text=_("status.ready", len(self.profiles), len(self.all_chars))
            )
        except Exception as e:
            self._status_bar.configure(text=_("status.init_error", e))

    def _build_user_groups(self):
        """将扁平的 profiles 按 Steam 用户分组"""
        # 取本地 Steam loginusers.vdf，一次加载供所有用户兜底
        _local_accounts: dict[str, str] = {}
        try:
            for acct in steam_api.get_local_accounts():
                if acct.persona_name:
                    _local_accounts[acct.steam_id] = acct.persona_name
        except Exception:
            pass

        groups_map: dict[str, SteamUserGroup] = {}
        for prof in self.profiles:
            sid = prof.steam_id
            if sid not in groups_map:
                # 来源1：steam_names.json
                steam_root = save_io.STEAM_DIR / sid
                steam_names = characters.load_steam_names(str(steam_root))
                persona = steam_names.get(str(sid), "").strip()

                # 来源2（兜底）：loginusers.vdf
                if not persona and sid in _local_accounts:
                    persona = _local_accounts[sid]

                # 截断 ID
                short_id = f"{sid[:5]}...{sid[-4:]}" if len(sid) >= 9 else sid

                groups_map[sid] = SteamUserGroup(
                    steam_id=sid,
                    steam_id_short=short_id,
                    persona=persona,
                    profiles=[],
                )
            groups_map[sid].profiles.append(prof)

        # 每组内按存档时间降序排序
        for g in groups_map.values():
            g.profiles.sort(key=lambda p: p.path.stat().st_mtime, reverse=True)

        # 有昵称的排前面，同名按 steam_id 排序
        self.user_groups = sorted(
            groups_map.values(),
            key=lambda g: (not bool(g.persona), g.steam_id),
        )

    def _lookup_steam_name(self, steam_id: str, steam_names: dict) -> str:
        """从 steam_names 字典中查找 Steam 昵称，找不到则返回截断的 steam_id"""
        name = steam_names.get(str(steam_id), "").strip()
        if name:
            return name
        if len(steam_id) >= 9:
            return f"{steam_id[:5]}...{steam_id[-4:]}"
        return steam_id

    def _reload_save(self):
        if self.save_path:
            self.save_data = save_io.load_save(self.save_path) or {}
            self.backups = save_io.scan_backups(self.save_path)
        else:
            self.save_data = {}

    def _refresh_status(self):
        if not self.save_data:
            self._status_bar.configure(text=_("status.no_save"))
            return
        players = self.save_data.get("players", [])
        act_idx = self.save_data.get("current_act_index", 0) + 1
        asc = self.save_data.get("ascension", 0)
        pnames = (_("status.list_sep")).join(self._player_heading_name(p) for p in players) or _("status.no_players")
        self._status_bar.configure(
            text=_("status.info", len(players), act_idx, asc, pnames)
        )

    # ── 页面切换 ──────────────────────────────────────────────────────────

    def _ctk_font(self, size: float, weight: str = "normal",
                   family: str = "Microsoft YaHei") -> ctk.CTkFont:
        """返回缩放后的字体"""
        return ctk.CTkFont(size=int(size * self._font_scale),
                           weight=weight, family=family)

    def _nav_highlight(self, key: str):
        for k, btn in self._nav_buttons.items():
            if k == key:
                btn.configure(fg_color=("#1F3460", "#1F3460"),
                              text_color=("#F5A623", "#F5A623"))
            else:
                btn.configure(fg_color="transparent",
                              text_color=("#D1D5DB", "#D1D5DB"))

    def show_page(self, key: str):
        self._nav_highlight(key)
        for w in self._content.winfo_children():
            w.destroy()
        self._current_page = key
        getattr(self, f"_page_{key}")()

    def _page(self):
        """各页面：外层 + 可滚动内容区，避免控件挤出窗口"""
        outer = ctk.CTkFrame(self._content, fg_color="transparent")
        outer.pack(fill="both", expand=True, padx=12, pady=12)
        scroll = ctk.CTkScrollableFrame(
            outer, fg_color="transparent",
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
        )
        scroll.pack(fill="both", expand=True)
        return scroll

    def _section_title(self, parent, icon: str, title: str, subtitle: str = ""):
        ctk.CTkLabel(
            parent, text=f"{icon}  {title}",
            font=self._ctk_font(14, weight="bold"),
            anchor="w",
        ).pack(anchor="w", pady=(0, 6))
        if subtitle:
            ctk.CTkLabel(
                parent, text=subtitle,
                font=self._ctk_font(10),
                text_color=("#A0A0B0", "#A0A0B0"), anchor="w",
            ).pack(anchor="w", pady=(0, 16))

    def _warn(self, parent, msg: str):
        w = ctk.CTkFrame(parent, corner_radius=6, fg_color=("#3D1A1A", "#3D1A1A"))
        w.pack(fill="x", pady=10)
        ctk.CTkLabel(
            w, text=msg,
            font=self._ctk_font(10),
            text_color=("#E74C3C", "#E74C3C"),
            anchor="w",
        ).pack(fill="x", padx=12, pady=8)

    def _row(self, parent, label: str, widget, pady: int = 6):
        row = ctk.CTkFrame(parent, fg_color="transparent")
        row.pack(fill="x", pady=pady)
        ctk.CTkLabel(
            row, text=label, width=110, anchor="w",
            font=self._ctk_font(10),
            text_color=("#9CA3AF", "#9CA3AF"),
        ).pack(side="left", anchor="w")
        widget.pack(side="left", fill="x", expand=True)
        return row

    def _localized_character_name(self, character_id: str) -> str:
        """Character ID → localized name"""
        if not character_id:
            return _("char.display.unknown")
        cid = character_id.strip()
        t = self.all_chars.get(cid)
        if t:
            return t.name
        return cid.replace("CHARACTER.", "").replace("_", " ")

    def _steam_name_str(self, net_id: str) -> str:
        """Steam ID → nickname; fallback placeholder if not found"""
        name = self.steam_names.get(str(net_id), "").strip()
        return name if name else _("char.display.steam_not_found")

    def _player_heading_name(self, pl: dict) -> str:
        """状态栏/列表标题：优先 Steam 昵称，否则本地化角色名"""
        net = str(pl.get("net_id", ""))
        if net in self.steam_names:
            return self.steam_names[net]
        return self._localized_character_name(pl.get("character_id", ""))

    def _player_row_title_with_id(self, display_index: int, pl: dict) -> str:
        """
        玩家卡片主标题：[序号] Steam昵称（若有） 角色名 [net_id]。
        Steam 昵称与本地化角色名相同时只显示一段（Mod 角色名常等于玩家昵称，避免「帕瓦五号 帕瓦五号」）。
        """
        net_id = str(pl.get("net_id", ""))
        char_cn = self._localized_character_name(pl.get("character_id", ""))
        steam = self.steam_names.get(net_id, "").strip()
        head = f"[{display_index}]"
        if steam and char_cn and steam == char_cn:
            return f"{head} {steam}  [{net_id}]"
        if steam:
            return f"{head} {steam}  {char_cn}  [{net_id}]"
        return f"{head} {char_cn}  [{net_id}]"

    def _player_row_copy_mode_label(
        self, display_index: int, pl: dict, hp: int, gold: int,
    ) -> str:
        """添加玩家 · 复制模式：源玩家单选行（Steam 名 + 角色名 + 生命/金币）。"""
        net_id = str(pl.get("net_id", ""))
        char_cn = self._localized_character_name(pl.get("character_id", ""))
        steam = self.steam_names.get(net_id, "").strip()
        head = f"[{display_index}]"
        if steam and char_cn and steam == char_cn:
            return f"{head} {steam}  {_("card.hp_label", hp)}  {_("card.gold", gold)}"
        if steam:
            return f"{head} {steam}  {char_cn}  {_("card.hp_label", hp)}  {_("card.gold", gold)}"
        return f"{head} {char_cn}  {_("card.hp_label", hp)}  {_("card.gold", gold)}"

    def _relic_display(self, relic_id: str) -> str:
        """遗物 ID 简短显示（去前缀）"""
        if not relic_id:
            return ""
        return relic_id.replace("RELIC.", "").replace("_", " ")

    def _char_picker_pairs(self) -> list[tuple[str, str]]:
        """下拉框：(显示文案, character_id)，显示为「中文名（短ID）」"""
        pairs: list[tuple[str, str]] = []
        for cid, t in sorted(
            self.all_chars.items(),
            key=lambda x: (x[1].is_mod, x[0]),
        ):
            short = cid.replace("CHARACTER.", "")
            disp = _("char.display.format", t.name, short)
            pairs.append((disp, cid))
        return pairs

    # ── 页面：存档选择 ─────────────────────────────────────────────────────

    def _page_save_select(self):
        p = self._page()
        self._section_title(p, "📂", _("saveselect.title"))

        if not self.user_groups:
            ctk.CTkLabel(
                p, text=_("saveselect.not_found"), font=self._ctk_font(10),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w", justify="left",
            ).pack(anchor="w", pady=20)
            ctk.CTkButton(
                p, text=_("saveselect.rescan"), command=self._load_initial_data,
                width=120, height=34, corner_radius=6,
                fg_color="#2A3F7A", hover_color="#3A5FAA",
            ).pack(anchor="w")
            return

        scroll = ctk.CTkScrollableFrame(
            p, fg_color="transparent",
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
        )
        scroll.pack(fill="both", expand=True, pady=(4, 8))

        # 折叠状态：steam_id -> bool（True=展开）
        self._group_collapsed: dict[str, bool] = {}

        for group in self.user_groups:
            self._group_collapsed[group.steam_id] = False
            self._render_user_group(scroll, group)

        ctk.CTkButton(
            p, text=_("saveselect.rescan"), command=self._load_initial_data,
            width=120, height=34, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            text_color="#A0A0B0",
        ).pack(anchor="w", pady=(12, 0))

    def _render_user_group(self, parent, group):
        """渲染一个 Steam 用户的分组（折叠头 + 存档卡片列表）"""
        active_n = sum(1 for p in group.profiles if p.is_active)

        # ── 分组头 ────────────────────────────────────────────────────────
        header = ctk.CTkFrame(
            parent, corner_radius=8,
            fg_color=("#1A2F50", "#1A2F50"),
            height=52,
        )
        header.pack(fill="x", pady=(0, 4), padx=2)
        header.pack_propagate(False)

        # 展开/折叠箭头
        self._group_arrow: dict = getattr(self, "_group_arrow", {})
        arrow = ctk.CTkLabel(
            header, text="▼", font=self._ctk_font(12),
            text_color=("#F5A623", "#F5A623"),
            width=24,
        )
        arrow.pack(side="left", padx=(12, 4), pady=8)
        self._group_arrow[group.steam_id] = arrow

        # 截断 ID（灰色次要）
        ctk.CTkLabel(
            header, text=group.steam_id_short,
            font=self._ctk_font(12, weight="normal"),
            text_color=("#A0A0B0", "#A0A0B0"),
            anchor="w",
        ).pack(side="left", padx=(0, 8), pady=8)

        # Steam 昵称（金色强调，有则显示，无则空白）
        if group.persona:
            ctk.CTkLabel(
                header, text=group.persona,
                font=self._ctk_font(12, weight="bold"),
                text_color=("#F5A623", "#F5A623"),
                anchor="w",
            ).pack(side="left", fill="x", expand=True, pady=8)
        else:
            ctk.CTkLabel(
                header, text="",
                font=self._ctk_font(12, weight="bold"),
                anchor="w",
            ).pack(side="left", fill="x", expand=True, pady=8)

        # 进行中标签
        if active_n > 0:
            tag = ctk.CTkLabel(
                header, text=_("saveselect.active", active_n),
                font=self._ctk_font(9, weight="bold"),
                text_color=("#1A1A2E", "#1A1A2E"),
                fg_color=("#F5A623", "#F5A623"),
                corner_radius=10,
                width=80, height=22,
            )
            tag.pack(side="right", padx=(8, 10), pady=12)
            tag.pack_propagate(False)

        # 存档数量
        ctk.CTkLabel(
            header, text=_("saveselect.profile_count", len(group.profiles)),
            font=self._ctk_font(9),
            text_color=("#6B7280", "#6B7280"),
        ).pack(side="right", padx=(0, 8), pady=8)

        # ── 存档卡片容器（展开时显示）────────────────────────────────────
        cards_container = ctk.CTkFrame(parent, fg_color="transparent")
        cards_container.pack(fill="x", padx=2)

        def _toggle(e=None):
            collapsed = not self._group_collapsed[group.steam_id]
            self._group_collapsed[group.steam_id] = collapsed
            arrow.configure(text="▶" if collapsed else "▼")
            if collapsed:
                cards_container.pack_forget()
            else:
                cards_container.pack(fill="x", padx=2)

        header.bind("<Button-1>", _toggle)
        arrow.bind("<Button-1>", _toggle)
        for w in header.winfo_children():
            w.configure(cursor="hand2")
            w.unbind("<Button-1>")
            w.bind("<Button-1>", _toggle)

        # ── 渲染存档卡片 ──────────────────────────────────────────────────
        for profile in group.profiles:
            self._render_save_card(cards_container, profile)

    def _render_save_card(self, parent, profile):
        """渲染单个存档卡片"""
        status_tag = _("card.status_in_progress") if profile.is_active else _("card.status_not_started")
        time_str = profile.save_time.strftime("%m-%d %H:%M") if profile.save_time else "?"

        # 进行中卡片：左上角加蓝色竖线边框
        base_color = ("#1A3A6A", "#1A3A6A") if profile.is_active else ("#1F3460", "#1F3460")
        card = ctk.CTkFrame(
            parent, corner_radius=8,
            fg_color=base_color,
            height=72,
        )
        card.pack(fill="x", pady=(0, 4))
        card.pack_propagate(False)

        # 蓝色左边框（进行中标识）- 使用 Canvas 绘制竖线避免 place 限制
        if profile.is_active:
            card._left_bar_canvas = ctk.CTkCanvas(
                card, width=4, height=52,
                bg=card.cget("fg_color")[0] if hasattr(card, 'cget') else "#1A3A6A",
                highlightthickness=0, bd=0,
            )
            card._left_bar_canvas.place(x=6, y=10)
            card._left_bar_canvas.create_rectangle(0, 0, 4, 52, fill="#3B82F6", outline="#3B82F6")

        # 存档路径标签（区分模式）
        mode_label = _("card.mod") if profile.is_modded else _("card.standard")
        ctk.CTkLabel(
            card, text=_("card.profile_mode", mode_label, profile.profile_key),
            font=self._ctk_font(10, weight="bold"),
            text_color=("#F5A623", "#F5A623"),
            anchor="w",
        ).pack(anchor="w", fill="x", padx=(16 if profile.is_active else 12, 12), pady=(10, 2))

        info = _("card.info",
                 profile.player_count,
                 profile.act_index + 1,
                 profile.ascension,
                 status_tag,
                 time_str)
        ctk.CTkLabel(
            card, text=info,
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"),
            anchor="w",
        ).pack(anchor="w", fill="x", padx=(16 if profile.is_active else 12, 12), pady=(0, 8))

        # 点击加载
        def _make_load(prof):
            return lambda _: self._load_profile(prof)

        def _on_enter(e, c=card, active=profile.is_active):
            c.configure(fg_color=("#2A5090", "#2A5090"))

        def _on_leave(e, c=card, active=profile.is_active):
            c.configure(fg_color=("#1A3A6A", "#1A3A6A") if active else ("#1F3460", "#1F3460"))

        card.bind("<Enter>", _on_enter)
        card.bind("<Leave>", _on_leave)
        card.bind("<Button-1>", _make_load(profile))
        for w in card.winfo_children():
            if hasattr(w, "winfo_class"):
                w.configure(cursor="hand2")
                w.unbind("<Button-1>")
                w.bind("<Button-1>", _make_load(profile))

    def _load_profile(self, profile: save_io.SaveProfile):
        self.save_path = profile.path
        self.steam_names = characters.load_steam_names(str(profile.path.parent.parent))
        # 用本机 Steam 好友列表补全昵称（完全离线，无需 API Key）
        try:
            import MP_PlayerManager_v2.steam_api as _sa
            for c in _sa.get_all_contacts(self.tool_dir):
                if c.steam_id and c.nickname:
                    self.steam_names[str(c.steam_id)] = c.nickname
        except Exception:
            pass
        self._reload_save()
        self._refresh_status()
        self.show_page("takeover")

    # ── 页面：夺舍玩家 ─────────────────────────────────────────────────────

    def _page_takeover(self):
        p = self._page()
        self._section_title(
            p, "👤", _("takeover.title"),
            _("takeover.subtitle"),
        )

        if not self.save_data:
            self._warn(p, _("takeover.select_save"))
            return

        players = self.save_data.get("players", [])
        self._to_selected_idx = ctk.IntVar(value=-1)

        # 选中玩家后，在接替者区显示该玩家的 Steam 昵称
        def _on_idx_change(*_):
            idx = self._to_selected_idx.get()
            if idx < 0 or idx >= len(players):
                if hasattr(self, "_to_steam_id"):
                    self._to_steam_id.set_steam_name("")
                return
            pl = players[idx]
            net_id = str(pl.get("net_id", ""))
            if hasattr(self, "_to_steam_id"):
                self._to_steam_id.set_steam_name(self._steam_name_str(net_id))

        self._to_selected_idx.trace_add("write", _on_idx_change)

        scroll = ctk.CTkScrollableFrame(
            p, fg_color="transparent", height=220,
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
        )
        scroll.pack(fill="x", pady=(4, 6))

        for i, pl in enumerate(players):
            is_host = (i == 0)
            net_id = pl.get("net_id", "")
            hp = pl.get("current_hp", 0)
            maxhp = pl.get("max_hp", 0)
            gold = pl.get("gold", 0)
            deck_n = len(pl.get("deck", []))
            relics_n = len(pl.get("relics", []))

            card = ctk.CTkFrame(scroll, corner_radius=8,
                                  fg_color=("#1F3460", "#1F3460"))
            card.pack(fill="x", pady=4, padx=2)

            if is_host:
                pname = self._player_heading_name(pl)
                ctk.CTkLabel(
                    card, text=f"[{i + 1}] {pname}  {_("takeover.host")}",
                    font=self._ctk_font(10), anchor="w",
                    text_color=("#F5A623", "#F5A623"),
                ).pack(anchor="w", fill="x", padx=12, pady=(10, 2))
                ctk.CTkLabel(
                    card,
                    text=f"{self._steam_name_str(net_id)}  {_("status.id_label")}{net_id}  "
                         + _("card.hp", hp, maxhp) + "  " + _("card.gold", gold) + "  "
                         + _("card.cards", deck_n) + "  " + _("card.relics", relics_n),
                    font=self._ctk_font(9),
                    text_color=("#9CA3AF", "#9CA3AF"),
                    anchor="w",
                ).pack(anchor="w", fill="x", padx=12, pady=(0, 10))
            else:
                ctk.CTkRadioButton(
                    card, text="", variable=self._to_selected_idx, value=i,
                    radiobutton_width=18, radiobutton_height=18,
                ).pack(side="left", padx=(10, 6), anchor="n", pady=10)

                text_col = ctk.CTkFrame(card, fg_color="transparent")
                text_col.pack(side="left", fill="both", expand=True)
                ctk.CTkLabel(
                    text_col,
                    text=self._player_row_title_with_id(i + 1, pl),
                    font=self._ctk_font(10, weight="bold"),
                    anchor="w",
                ).pack(anchor="w", fill="x", pady=(8, 2))
                ctk.CTkLabel(
                    text_col,
                    text=_("card.hp", hp, maxhp) + "  ·  " + _("card.gold", gold) + "  ·  "
                         + _("card.cards", deck_n) + "  ·  " + _("card.relics", relics_n),
                    font=self._ctk_font(9),
                    text_color=("#9CA3AF", "#9CA3AF"),
                    anchor="w",
                ).pack(anchor="w", fill="x", pady=(0, 8))

        # 接替者信息
        sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep.pack(fill="x", pady=14)

        ctk.CTkLabel(
            p, text=_("takeover.successor"),
            font=self._ctk_font(12, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        self._to_steam_id = SteamIDInput(
            p, self.tool_dir, width=340, font_scale=self._font_scale,
        )
        self._to_steam_id.pack(anchor="w", pady=(0, 8))

        self._warn(p, "⚠ " + _("takeover.irreversible"))

        btn_row = ctk.CTkFrame(p, fg_color="transparent")
        btn_row.pack(fill="x", pady=(12, 0))
        ctk.CTkButton(
            btn_row, text=_("takeover.confirm"), command=self._do_takeover,
            width=140, height=38, corner_radius=8,
            font=self._ctk_font(11, weight="bold"),
            fg_color="#F5A623", hover_color="#E09010",
            text_color="#1A1A2E",
        ).pack(side="left", ipadx=12, ipady=4)
        ctk.CTkButton(
            btn_row, text=_("takeover.refresh"), command=self._do_refresh,
            width=100, height=38, corner_radius=8,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
        ).pack(side="left", padx=(10, 0), ipadx=12, ipady=4)

    def _do_takeover(self):
        idx = self._to_selected_idx.get()
        steam_id = self._to_steam_id.get()
        if idx < 0:
            self._msg_box("warning", _("takeover.not_selected"), _("takeover.choose_player"))
            return
        if idx == 0:
            self._msg_box("warning", _("takeover.host_protected"), _("takeover.host_protected_msg"))
            return

        if not steam_id:
            self._msg_box(
                "info", _("takeover.choose_steam"),
                _("takeover.choose_steam_msg"),
            )
            return

        if not steam_id.isdigit() or len(steam_id) < 15:
            self._msg_box("warning", _("takeover.id_format"), _("takeover.id_format_msg"))
            return

        result = core.take_over_player(self.save_data, idx, int(steam_id))
        if result.success:
            ok = save_io.write_save(self.save_path, self.save_data)
            if ok:
                self._reload_save()
                self._refresh_status()
                self._msg_box("info", _("takeover.success"), result.message)
            else:
                self._msg_box("error", _("takeover.save_failed"), _("takeover.save_failed_msg"))
        else:
            self._msg_box("error", _("takeover.failed"), result.message)

    # ── 页面：添加玩家 ─────────────────────────────────────────────────────

    def _page_add_player(self):
        p = self._page()
        self._section_title(p, "➕", _("add.title"))

        if not self.save_data:
            self._warn(p, _("add.select_save"))
            return

        self._add_mode = ctk.StringVar(value="copy")

        mode_frame = ctk.CTkFrame(p, fg_color="transparent")
        mode_frame.pack(anchor="w", pady=(0, 16))

        for val, key in [
            ("copy", "add.copy_mode"),
            ("fresh", "add.fresh_mode"),
        ]:
            ctk.CTkRadioButton(
                mode_frame, text=_(key), variable=self._add_mode,
                value=val, command=self._refresh_add_page,
                radiobutton_width=18, radiobutton_height=18,
            ).pack(anchor="w", pady=(0, 6))

        # 复制模式面板
        self._add_copy_panel = ctk.CTkFrame(p, fg_color="transparent")
        self._add_copy_panel.pack(fill="x", pady=(0, 10))

        # 模板模式面板
        self._add_fresh_panel = ctk.CTkFrame(p, fg_color="transparent")
        self._add_fresh_panel.pack(fill="x", pady=(0, 10))

        # 新玩家信息
        sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep.pack(fill="x", pady=12)

        ctk.CTkLabel(
            p, text=_("add.new_player"),
            font=self._ctk_font(12, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        self._add_steam_id = SteamIDInput(p, self.tool_dir, width=300, font_scale=self._font_scale)
        self._add_steam_id.pack(anchor="w", pady=(0, 8))

        ctk.CTkButton(
            p, text=_("add.confirm"), command=self._do_add_player,
            width=140, height=38, corner_radius=8,
            font=self._ctk_font(11, weight="bold"),
            fg_color="#27AE60", hover_color="#1E8449",
            text_color="#FFFFFF",
        ).pack(anchor="w", pady=(16, 0), ipadx=12, ipady=4)

        self._refresh_add_page()

    def _refresh_add_page(self):
        for w in self._add_copy_panel.winfo_children():
            w.destroy()
        for w in self._add_fresh_panel.winfo_children():
            w.destroy()

        if self._add_mode.get() == "copy":
            self._add_copy_panel.pack(fill="x", pady=(0, 10))
            self._add_fresh_panel.pack_forget()
            self._build_copy_panel()
        else:
            self._add_copy_panel.pack_forget()
            self._add_fresh_panel.pack(fill="x", pady=(0, 10))
            self._build_fresh_panel()

    def _build_copy_panel(self):
        players = self.save_data.get("players", [])
        self._add_source_idx = ctk.IntVar(value=0)

        ctk.CTkLabel(
            self._add_copy_panel, text=_("add.source_player"),
            font=self._ctk_font(10),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(anchor="w")

        for i, pl in enumerate(players):
            hp = pl.get("current_hp", 0)
            gold = pl.get("gold", 0)
            ctk.CTkRadioButton(
                self._add_copy_panel,
                text=self._player_row_copy_mode_label(i + 1, pl, hp, gold),
                variable=self._add_source_idx, value=i,
                radiobutton_width=18, radiobutton_height=18,
            ).pack(anchor="w", pady=(0, 6))

    def _build_fresh_panel(self):
        ctk.CTkLabel(
            self._add_fresh_panel, text=_("add.select_char"),
            font=self._ctk_font(10),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        self._add_selected_char = ctk.StringVar()
        chars = list(self.all_chars.values())

        # 外层滚动容器，防止角色过多时超出窗口
        scroll = ctk.CTkScrollableFrame(
            self._add_fresh_panel,
            fg_color="transparent",
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
            height=200,
        )
        scroll.pack(fill="x", expand=True, pady=(0, 4))

        grid = ctk.CTkFrame(scroll, fg_color="transparent")
        grid.pack(fill="x", expand=True)

        cols = 3
        for c in range(cols):
            grid.columnconfigure(c, weight=1, uniform="chars")

        GOLD = ("#F5A623", "#F5A623")
        GOLD_BG = ("#2A1F08", "#2A1F08")
        CARD_BG = ("#1F3460", "#1F3460")

        for i, char in enumerate(chars):
            row_i = i // cols
            col_i = i % cols

            # 金色外框（通过父框架背景色模拟边框）
            outer = ctk.CTkFrame(
                grid, corner_radius=10,
                fg_color=GOLD_BG,
            )
            outer.grid(row=row_i, column=col_i, padx=6, pady=6, sticky="nsew")

            card = ctk.CTkFrame(
                outer, corner_radius=8,
                fg_color=CARD_BG,
            )
            card.pack(fill="both", expand=True, padx=2, pady=2)

            inner = ctk.CTkFrame(card, fg_color="transparent")
            inner.pack(fill="both", expand=True, padx=10, pady=8)

            ctk.CTkRadioButton(
                inner, text="", variable=self._add_selected_char,
                value=char.character_id,
                radiobutton_width=18, radiobutton_height=18,
            ).pack(anchor="w", pady=(0, 4))

            ctk.CTkLabel(
                inner, text=char.name,
                font=self._ctk_font(11, weight="bold"),
                text_color=GOLD, anchor="w",
            ).pack(anchor="w", pady=(0, 2))

            ctk.CTkLabel(
                inner, text=f"HP {char.max_hp}",
                font=self._ctk_font(9),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
            ).pack(anchor="w")

            if char.is_mod:
                ctk.CTkLabel(
                    inner, text="[Mod]",
                    font=self._ctk_font(8),
                    text_color=("#9B59B6", "#9B59B6"), anchor="w",
                ).pack(anchor="w")

        if chars:
            self._add_selected_char.set(chars[0].character_id)

    def _do_add_player(self):
        steam_id = self._add_steam_id.get()
        if not steam_id:
            self._msg_box(
                "info", _("add.choose_steam"),
                _("add.choose_steam_msg"),
            )
            return
        if not steam_id.isdigit() or len(steam_id) < 15:
            self._msg_box("warning", _("add.id_format"), _("add.id_format"))
            return

        # 检查 ID 是否已存在
        for pl in self.save_data.get("players", []):
            if str(pl.get("net_id")) == steam_id:
                self._msg_box("warning", _("add.id_conflict"), f"Steam ID ({steam_id}) {_('add.id_conflict')}")
                return

        net_id = int(steam_id)
        mode = self._add_mode.get()

        try:
            if mode == "copy":
                source_idx = self._add_source_idx.get()
                result = core.add_player_copy(
                    self.save_data, source_idx, net_id,
                )
            else:
                char_id = self._add_selected_char.get()
                template = self.all_chars.get(char_id)
                if not template:
                    self._msg_box("warning", _("add.no_char"), _("add.no_char_msg"))
                    return
                result = core.add_player_fresh(self.save_data, net_id, template)

            if result.success:
                ok = save_io.write_save(self.save_path, self.save_data)
                if ok:
                    self._reload_save()
                    self._refresh_status()
                    self._msg_box("info", _("add.success"), result.message)
                else:
                    self._msg_box("error", _("add.save_failed"), _("add.save_failed"))
            else:
                self._msg_box("error", _("add.failed"), result.message)
        except Exception as e:
            traceback.print_exc()
            self._msg_box("error", _("add.exception"), str(e))

    # ── 页面：移除玩家 ─────────────────────────────────────────────────────

    def _page_remove_player(self):
        p = self._page()
        self._section_title(
            p, "➖", _("remove.title"),
            _("remove.subtitle"),
        )

        if not self.save_data:
            self._warn(p, _("remove.select_save"))
            return

        players = self.save_data.get("players", [])
        self._rm_selected = ctk.IntVar(value=-1)

        scroll = ctk.CTkScrollableFrame(
            p, fg_color="transparent", height=260,
            scrollbar_button_color="#E74C3C",
            scrollbar_button_hover_color="#C0392B",
        )
        scroll.pack(fill="x", pady=4)

        for i, pl in enumerate(players):
            net_id = pl.get("net_id", "")
            deck_n = len(pl.get("deck", []))
            relics = [r.get("id", "") for r in pl.get("relics", [])]
            potions_n = len(pl.get("potions") or [])

            card = ctk.CTkFrame(
                scroll, corner_radius=8,
                fg_color=("#3D1A1A", "#3D1A1A"),
            )
            card.pack(fill="x", pady=4, padx=2)

            ctk.CTkRadioButton(
                card, text="", variable=self._rm_selected, value=i,
                radiobutton_width=18, radiobutton_height=18,
                border_color="#E74C3C",
            ).pack(side="left", padx=(10, 6), anchor="n", pady=10)

            text_col = ctk.CTkFrame(card, fg_color="transparent")
            text_col.pack(side="left", fill="x", expand=True)

            ctk.CTkLabel(
                text_col,
                text=self._player_row_title_with_id(i + 1, pl),
                font=self._ctk_font(10, weight="bold"),
                anchor="w",
            ).pack(anchor="w", pady=(8, 2))
            ctk.CTkLabel(
                text_col,
                text=_("card.cards", deck_n) + "  ·  " + _("card.relics", len(relics)) + "  ·  " + _("card.potions", potions_n),
                font=self._ctk_font(9),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
            ).pack(anchor="w", pady=(0, 2))
            ctk.CTkLabel(
                text_col,
                text=_("card.relics_list", ', '.join(self._relic_display(r) for r in relics[:3]))
                     + ("..." if len(relics) > 3 else ""),
                font=self._ctk_font(8),
                text_color=("#7F8C8D", "#7F8C8D"), anchor="w",
            ).pack(anchor="w", pady=(0, 8))

        self._warn(p, "⚠ " + _("remove.irreversible"))

        ctk.CTkButton(
            p, text=_("remove.confirm_btn"), command=self._do_remove_player,
            width=140, height=38, corner_radius=8,
            font=self._ctk_font(11, weight="bold"),
            fg_color="#E74C3C", hover_color="#C0392B",
            text_color="#FFFFFF",
        ).pack(anchor="w", pady=(16, 0), ipadx=12, ipady=4)

    def _do_remove_player(self):
        idx = self._rm_selected.get()
        if idx < 0:
            self._msg_box("warning", _("remove.not_selected"), _("remove.choose"))
            return

        # CustomTkinter 没有 askyesno，手动实现确认框
        confirm = self._confirm_box(
            _("remove.confirm_title"),
            _("remove.confirm_msg"),
        )
        if not confirm:
            return

        result = core.remove_player(self.save_data, idx)
        if result.success:
            ok = save_io.write_save(self.save_path, self.save_data)
            if ok:
                self._reload_save()
                self._refresh_status()
                details = "\n".join(
                    f"· {k}: {v}" for k, v in (result.details or {}).items()
                    if k.startswith("removed")
                )
                self._msg_box("info", _("remove.success"), result.message + ("\n\nRemoved:\n" + details if details else ""))
            else:
                self._msg_box("error", _("remove.save_failed"), _("remove.save_failed"))
        else:
            self._msg_box("error", _("remove.failed"), result.message)

    def _confirm_box(self, title: str, message: str) -> bool:
        """简易确认对话框"""
        dlg = ctk.CTkToplevel(self)
        dlg.title(title)
        dlg.geometry("360x160")
        dlg.resizable(False, False)
        dlg.grab_set()
        dlg.update_idletasks()
        sx = (dlg.winfo_screenwidth() - 360) // 2
        sy = (dlg.winfo_screenheight() - 160) // 2
        dlg.geometry(f"360x160+{sx}+{sy}")

        result = [False]

        container = ctk.CTkFrame(dlg, fg_color="transparent")
        container.pack(fill="both", expand=True, padx=24, pady=20)

        ctk.CTkLabel(
            container, text=message,
            font=self._ctk_font(11), wraplength=310,
        ).pack(pady=(0, 16))

        btn_row = ctk.CTkFrame(container, fg_color="transparent")
        btn_row.pack()
        ctk.CTkButton(
            btn_row, text=_("common.confirm"), width=100, height=34, corner_radius=6,
            fg_color="#E74C3C", hover_color="#C0392B",
            command=lambda: (result.__setitem__(0, True), dlg.destroy()),
        ).pack(side="left", ipadx=8)
        ctk.CTkButton(
            btn_row, text=_("common.cancel"), width=100, height=34, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            command=dlg.destroy,
        ).pack(side="left", padx=(10, 0))

        dlg.wait_window()
        return result[0]

    def _msg_box(self, kind: str, title: str, message: str):
        """通用消息框"""
        icon_map = {"info": "#27AE60", "warning": "#F5A623", "error": "#E74C3C"}
        color = icon_map.get(kind, "#F5A623")

        dlg = ctk.CTkToplevel(self)
        dlg.title(title)
        dlg.geometry("360x180")
        dlg.resizable(False, False)
        dlg.grab_set()
        dlg.update_idletasks()
        sx = (dlg.winfo_screenwidth() - 360) // 2
        sy = (dlg.winfo_screenheight() - 180) // 2
        dlg.geometry(f"360x180+{sx}+{sy}")

        container = ctk.CTkFrame(dlg, fg_color="transparent")
        container.pack(fill="both", expand=True, padx=24, pady=20)

        ctk.CTkLabel(
            container, text=title,
            font=self._ctk_font(13, weight="bold"),
            text_color=(color, color), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        ctk.CTkLabel(
            container, text=message,
            font=self._ctk_font(10), wraplength=310, anchor="w",
        ).pack(fill="both", expand=True)

        ctk.CTkButton(
            container, text=_("common.ok"), width=100, height=34, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            command=dlg.destroy,
        ).pack(pady=(10, 0))

    def _do_refresh(self):
        self._reload_save()
        self._refresh_status()
        self.show_page(self._current_page)

    # ── 页面：备份管理 ─────────────────────────────────────────────────────

    def _page_backup(self):
        p = self._page()
        self._section_title(p, "💾", _("backup.all.title"), _("backup.all.subtitle"))

        # ── 全局备份浏览（按 Steam 用户分组）───────────────────────────────
        self._backup_group_collapsed: dict[str, bool] = {}

        if not self.all_backups:
            ctk.CTkLabel(
                p, text=_("backup.no_backups_global"),
                font=self._ctk_font(10),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
            ).pack(anchor="w", pady=20)
        else:
            self._render_backup_groups(p)

        # ── 当前存档备份（单独卡片，若有已加载存档）───────────────────────
        if self.save_path:
            sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
            sep.pack(fill="x", pady=16)

            ctk.CTkLabel(
                p, text=_("backup.current.title"),
                font=self._ctk_font(12, weight="bold"),
                text_color=("#F5A623", "#F5A623"), anchor="w",
            ).pack(anchor="w", pady=(0, 4))
            ctk.CTkLabel(
                p, text=_("backup.current.subtitle"),
                font=self._ctk_font(9),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
            ).pack(anchor="w", pady=(0, 10))

            # 当前存档信息
            info = _("backup.info",
                     self.save_path,
                     len(self.save_data.get('players', [])),
                     self.save_data.get('ascension', 0),
                     self.save_data.get('current_act_index', 0) + 1)
            info_card = ctk.CTkFrame(p, corner_radius=8, fg_color=("#1F3460", "#1F3460"))
            info_card.pack(fill="x", pady=(0, 12), ipady=8, padx=2)
            ctk.CTkLabel(
                info_card, text=info,
                font=self._ctk_font(10), anchor="w",
            ).pack(anchor="w", fill="x", padx=12, pady=4)

            ctk.CTkButton(
                p, text=_("backup.create_now"), command=self._do_backup_now,
                width=140, height=36, corner_radius=8,
                fg_color="#27AE60", hover_color="#1E8449",
                text_color="#FFFFFF",
            ).pack(anchor="w", ipadx=12, ipady=4)

            cur_wrap = ctk.CTkFrame(p, fg_color="transparent", height=BACKUP_CURRENT_SCROLL_H)
            cur_wrap.pack(fill="x", expand=False)
            cur_wrap.pack_propagate(False)

            self._current_backup_list = ctk.CTkScrollableFrame(
                cur_wrap, fg_color="transparent",
                scrollbar_button_color="#2A3F7A",
                scrollbar_button_hover_color="#3A5FAA",
            )
            self._current_backup_list.pack(fill="both", expand=True)
            self._refresh_current_backup_list()

        # 重新扫描按钮
        ctk.CTkButton(
            p, text=_("backup.rescan"), command=self._rescan_all_backups,
            width=120, height=34, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            text_color="#A0A0B0",
        ).pack(anchor="w", pady=(16, 0))

    def _render_backup_groups(self, parent):
        """按 Steam 用户分组渲染全局备份列表"""
        # 构建分组
        groups: dict[str, dict[str, list[save_io.BackupEntry]]] = {}
        for be in self.all_backups:
            sid = be.steam_id
            profile_key = be.profile_key
            if sid not in groups:
                groups[sid] = {}
            if profile_key not in groups[sid]:
                groups[sid][profile_key] = []
            groups[sid][profile_key].append(be)

        # 用定高外层 Frame 限高；勿对 CTkScrollableFrame 使用 pack_propagate(False)，否则内容区常无法绘制
        wrap = ctk.CTkFrame(parent, fg_color="transparent", height=BACKUP_GLOBAL_SCROLL_H)
        wrap.pack(fill="x", expand=False, pady=(0, 8))
        wrap.pack_propagate(False)

        scroll = ctk.CTkScrollableFrame(
            wrap, fg_color="transparent",
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
        )
        scroll.pack(fill="both", expand=True)

        for sid in sorted(groups.keys()):
            self._render_backup_user_group(scroll, sid, groups[sid])

    def _render_backup_user_group(self, parent, steam_id: str,
                                   profiles: dict[str, list[save_io.BackupEntry]]):
        """渲染单个 Steam 用户的备份分组"""
        total = sum(len(v) for v in profiles.values())

        # 取该用户第一个条目的 Steam 昵称（来自 steam_names.json）
        persona = ""
        for be_list in profiles.values():
            if be_list:
                from characters import load_steam_names
                names = load_steam_names(str(save_io.STEAM_DIR / steam_id))
                persona = names.get(str(steam_id), "").strip()
                break

        short_id = f"{steam_id[:5]}...{steam_id[-4:]}" if len(steam_id) >= 9 else steam_id

        # 分组头
        header = ctk.CTkFrame(
            parent, corner_radius=8,
            fg_color=("#1A2F50", "#1A2F50"),
            height=52,
        )
        header.pack(fill="x", pady=(0, 4), padx=2)
        header.pack_propagate(False)

        self._backup_group_collapsed[steam_id] = False

        arrow = ctk.CTkLabel(
            header, text="▼", font=self._ctk_font(12),
            text_color=("#F5A623", "#F5A623"),
            width=24,
        )
        arrow.pack(side="left", padx=(12, 4), pady=8)

        ctk.CTkLabel(
            header, text=short_id,
            font=self._ctk_font(12, weight="normal"),
            text_color=("#A0A0B0", "#A0A0B0"), anchor="w",
        ).pack(side="left", padx=(0, 8), pady=8)

        if persona:
            ctk.CTkLabel(
                header, text=persona,
                font=self._ctk_font(12, weight="bold"),
                text_color=("#F5A623", "#F5A623"), anchor="w",
            ).pack(side="left", fill="x", expand=True, pady=8)
        else:
            ctk.CTkLabel(
                header, text="", font=self._ctk_font(12, weight="bold"), anchor="w",
            ).pack(side="left", fill="x", expand=True, pady=8)

        ctk.CTkLabel(
            header, text=_("backup.backup_count", total),
            font=self._ctk_font(9),
            text_color=("#6B7280", "#6B7280"),
        ).pack(side="right", padx=(0, 8), pady=8)

        # 折叠容器
        content = ctk.CTkFrame(parent, fg_color="transparent")
        content.pack(fill="x", padx=2)

        def _toggle(e=None):
            collapsed = not self._backup_group_collapsed[steam_id]
            self._backup_group_collapsed[steam_id] = collapsed
            arrow.configure(text="▶" if collapsed else "▼")
            if collapsed:
                content.pack_forget()
            else:
                content.pack(fill="x", padx=2)

        header.bind("<Button-1>", _toggle)
        arrow.bind("<Button-1>", _toggle)
        for w in header.winfo_children():
            w.configure(cursor="hand2")
            w.unbind("<Button-1>")
            w.bind("<Button-1>", _toggle)

        # 每 profile 子组
        for profile_key in sorted(profiles.keys()):
            backups = profiles[profile_key]
            self._render_backup_profile_content(content, steam_id, profile_key, backups)

    def _render_backup_profile_content(self, parent, steam_id: str,
                                       profile_key: str,
                                       backups: list[save_io.BackupEntry]):
        """渲染单个 profile 的备份列表"""
        # profile_key 格式：steam_id/modded/profile1 或 steam_id/profile1
        profile_parts = profile_key.split("/")
        is_modded = "modded" in profile_parts
        profile_label = profile_parts[-1]

        sub_header = ctk.CTkFrame(
            parent, corner_radius=6,
            fg_color=("#162040", "#162040"),
            height=36,
        )
        sub_header.pack(fill="x", pady=(0, 2), padx=(12, 0))
        sub_header.pack_propagate(False)

        mode_tag = _("card.mod") if is_modded else _("card.standard")
        ctk.CTkLabel(
            sub_header, text=_("card.profile_mode", mode_tag, profile_label),
            font=self._ctk_font(10, weight="bold"),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(side="left", padx=(8, 0), pady=6)

        for be in backups:
            self._render_backup_card(parent, be)

    def _render_backup_card(self, parent, be: save_io.BackupEntry):
        """渲染单个备份卡片"""
        ts = be.timestamp
        time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"
        save_time_str = (be.save_time.strftime("%m-%d %H:%M")
                         if be.save_time else "?")

        card = ctk.CTkFrame(
            parent, corner_radius=6,
            fg_color=("#1F3460", "#1F3460"),
            height=64,
        )
        card.pack(fill="x", pady=(0, 4), padx=(24, 0))
        card.pack_propagate(False)

        def _on_enter(e, c=card):
            c.configure(fg_color=("#2A4090", "#2A4090"))
        def _on_leave(e, c=card):
            c.configure(fg_color=("#1F3460", "#1F3460"))

        card.bind("<Enter>", _on_enter)
        card.bind("<Leave>", _on_leave)

        for w in card.winfo_children():
            w.bind("<Enter>", _on_enter)
            w.bind("<Leave>", _on_leave)

        ctk.CTkLabel(
            card, text=time_str,
            font=self._ctk_font(11, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(side="left", padx=(12, 8), pady=8)

        info_text = _(
            "card.info",
            be.player_count,
            "?",
            0,
            "?",
            save_time_str,
        )
        ctk.CTkLabel(
            card, text=info_text,
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(side="left", fill="both", expand=True, pady=8)

        btn_row = ctk.CTkFrame(card, fg_color="transparent")
        btn_row.pack(side="right", padx=(0, 8), pady=6)

        ctk.CTkButton(
            btn_row, text=_("backup.delete"),
            width=52, height=26, corner_radius=6,
            fg_color="#8B2A2A", hover_color="#A83232",
            font=self._ctk_font(8),
            command=lambda be=be: self._delete_global_backup(be),
        ).pack(side="right", padx=(4, 0))

        ctk.CTkButton(
            btn_row, text=_("backup.view_save"),
            width=80, height=26, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            font=self._ctk_font(8),
            command=lambda be=be: self._go_to_save_from_backup(be),
        ).pack(side="right", padx=(4, 0))

        ctk.CTkButton(
            btn_row, text=_("backup.restore"),
            width=60, height=26, corner_radius=6,
            fg_color="#27AE60", hover_color="#1E8449",
            font=self._ctk_font(8),
            command=lambda be=be: self._restore_global_backup(be),
        ).pack(side="right")

    def _go_to_save_from_backup(self, be: save_io.BackupEntry):
        """从备份卡片跳转到对应存档"""
        # 找到对应的 profile
        for group in self.user_groups:
            if group.steam_id == be.steam_id:
                for prof in group.profiles:
                    if prof.profile_key == be.profile_key:
                        self._load_profile(prof)
                        self.show_page("save_select")
                        return

    def _restore_global_backup(self, be: save_io.BackupEntry):
        """恢复全局备份"""
        ts = be.timestamp
        time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"
        confirm = self._confirm_box(
            _("backup.restore_confirm"),
            _("backup.restore_confirm2", time_str),
        )
        if not confirm:
            return

        ok = save_io.restore_backup(be.path, be.save_path)
        if ok:
            self.all_backups = save_io.scan_all_backups()
            # 若恢复的正是当前加载的存档，则刷新
            if self.save_path and be.save_path.resolve() == self.save_path.resolve():
                self._reload_save()
                self._refresh_status()
            self.show_page(self._current_page)
            self._msg_box("info", _("backup.restore_success"),
                          _("backup.restore_success2", str(be.save_path)))
        else:
            self._msg_box("error", _("backup.restore_failed"), _("backup.restore_perm"))

    def _delete_global_backup(self, be: save_io.BackupEntry):
        ts = be.timestamp
        time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"
        confirm = self._confirm_box(
            _("backup.delete_confirm"),
            _("backup.delete_confirm2", time_str, be.path.name),
        )
        if not confirm:
            return
        ok = save_io.delete_backup_file(be.path)
        if ok:
            self.all_backups = save_io.scan_all_backups()
            if self.save_path and be.save_path.resolve() == self.save_path.resolve():
                self.backups = save_io.scan_backups(self.save_path)
            self.show_page(self._current_page)
            self._msg_box(
                "info", _("backup.delete_success"),
                _("backup.delete_success2", be.path.name),
            )
        else:
            self._msg_box("error", _("backup.delete_failed"), _("backup.restore_perm"))

    def _rescan_all_backups(self):
        self.all_backups = save_io.scan_all_backups()
        self.show_page("backup")

    def _refresh_current_backup_list(self):
        """渲染当前已加载存档的备份列表（原有逻辑）"""
        for w in self._current_backup_list.winfo_children():
            w.destroy()
        for b in self.backups:
            ts = b.timestamp
            time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"

            card = ctk.CTkFrame(
                self._current_backup_list, corner_radius=8,
                fg_color=("#1F3460", "#1F3460"),
            )
            card.pack(fill="x", pady=(0, 6), padx=2)

            ctk.CTkLabel(
                card, text=_("backup.item", time_str, b.player_count),
                font=self._ctk_font(10), anchor="w",
            ).pack(side="left", fill="x", expand=True, padx=10, pady=10)

            btn_row = ctk.CTkFrame(card, fg_color="transparent")
            btn_row.pack(side="right", padx=(0, 8), pady=8)

            ctk.CTkButton(
                btn_row, text=_("backup.delete"), width=52, height=28, corner_radius=6,
                fg_color="#8B2A2A", hover_color="#A83232",
                font=self._ctk_font(9),
                command=lambda bk=b: self._delete_loaded_save_backup(bk),
            ).pack(side="right", padx=(6, 0))

            ctk.CTkButton(
                btn_row, text=_("backup.restore"), width=60, height=28, corner_radius=6,
                fg_color="#2A3F7A", hover_color="#3A5FAA",
                font=self._ctk_font(9),
                command=lambda bk=b: self._restore_backup(bk),
            ).pack(side="right")

    def _do_backup_now(self):
        if not self.save_path:
            return
        bp = save_io.save_backup(self.save_path)
        self._reload_save()
        self.all_backups = save_io.scan_all_backups()
        self._refresh_current_backup_list()
        self._msg_box("info", _("backup.created"), _("backup.created_fmt", bp.name))

    def _restore_backup(self, backup: save_io.SaveBackup):
        confirm = self._confirm_box(
            _("backup.restore_confirm"),
            _("backup.restore_confirm_msg", backup.timestamp),
        )
        if not confirm:
            return
        if self.save_path:
            ok = save_io.restore_backup(backup.path, self.save_path)
            if ok:
                self._reload_save()
                self._refresh_status()
                self._msg_box("info", _("backup.restore_success"), _("backup.restore_msg"))
            else:
                self._msg_box("error", _("backup.restore_failed"), _("backup.restore_perm"))

    def _delete_loaded_save_backup(self, backup: save_io.SaveBackup):
        ts = backup.timestamp
        time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"
        confirm = self._confirm_box(
            _("backup.delete_confirm"),
            _("backup.delete_confirm2", time_str, backup.path.name),
        )
        if not confirm:
            return
        ok = save_io.delete_backup_file(backup.path)
        if ok:
            self.all_backups = save_io.scan_all_backups()
            if self.save_path:
                self.backups = save_io.scan_backups(self.save_path)
            self._refresh_current_backup_list()
            self._msg_box(
                "info", _("backup.delete_success"),
                _("backup.delete_success2", backup.path.name),
            )
        else:
            self._msg_box("error", _("backup.delete_failed"), _("backup.restore_perm"))

    # ── 页面：设置 ─────────────────────────────────────────────────────────

    def _page_settings(self):
        p = self._page()
        self._section_title(p, "⚙", _("settings.title"))

        # 字体大小
        font_card = ctk.CTkFrame(p, corner_radius=8, fg_color=("#1F3460", "#1F3460"))
        font_card.pack(anchor="w", pady=(0, 12), fill="x", ipady=10)

        ctk.CTkLabel(
            font_card, text=_("settings.font_size"),
            font=self._ctk_font(11, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=(8, 6))

        ctk.CTkLabel(
            font_card,
            text=_("settings.font_hint"),
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=(0, 8))

        slider_row = ctk.CTkFrame(font_card, fg_color="transparent")
        slider_row.pack(fill="x", padx=12, pady=(0, 6))

        ctk.CTkLabel(
            slider_row, text=_("settings.font_min"),
            font=self._ctk_font(9),
            text_color=("#6B7280", "#6B7280"),
            width=36,
        ).pack(side="left", padx=(0, 6))

        cur_lv = _font_level_from_scale(self._font_scale)
        self._font_slider = ctk.CTkSlider(
            slider_row,
            from_=1,
            to=FONT_SCALE_LEVELS,
            number_of_steps=FONT_SCALE_LEVELS - 1,
            width=360,
            height=20,
            command=self._on_font_slider_moved,
            fg_color=("#2A2A2A", "#2A2A2A"),
            progress_color=("#F5A623", "#F5A623"),
            button_color=("#3B82F6", "#3B82F6"),
            button_hover_color=("#60A5FA", "#60A5FA"),
        )
        self._font_slider.pack(side="left", fill="x", expand=True)
        self._font_slider.set(float(cur_lv))

        ctk.CTkLabel(
            slider_row, text=_("settings.font_max"),
            font=self._ctk_font(9),
            text_color=("#6B7280", "#6B7280"),
            width=36,
        ).pack(side="left", padx=(6, 0))

        self._font_scale_label = ctk.CTkLabel(
            font_card,
            text=_("settings.font_current", cur_lv, FONT_SCALE_LEVELS, self._font_scale * 100),
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        )
        self._font_scale_label.pack(anchor="w", padx=12, pady=(4, 0))

        sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep.pack(fill="x", pady=12)

        items = [
            (_("settings.mods_dir"), GAME_MODS_DIR),
            (_("settings.tool_dir"), self.tool_dir),
            (_("settings.current_save"), str(self.save_path) if self.save_path else _("settings.no_save")),
            (_("settings.char_count"), str(len(self.all_chars))),
            (_("settings.scan_root"),
             os.environ.get("SLAY_THE_SPIRE2_APPDATA", _("settings.default_path"))),
        ]
        wrap = max(520, WINDOW_W - 260)
        for k, v in items:
            row = ctk.CTkFrame(p, fg_color="transparent")
            row.pack(fill="x", pady=4)
            ctk.CTkLabel(
                row, text=k + ":", width=130, anchor="w",
                font=self._ctk_font(10),
                text_color=("#9CA3AF", "#9CA3AF"),
            ).pack(side="left", anchor="nw")
            ctk.CTkLabel(
                row, text=v, font=self._ctk_font(10, family="Consolas"),
                anchor="w", justify="left", wraplength=wrap,
            ).pack(side="left", anchor="nw", fill="x", expand=True)

        sep2 = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep2.pack(fill="x", pady=12)

        lang_card = ctk.CTkFrame(p, corner_radius=8, fg_color=("#1F3460", "#1F3460"))
        lang_card.pack(anchor="w", pady=(0, 12), fill="x", ipady=10)

        ctk.CTkLabel(
            lang_card, text=_("settings.language"),
            font=self._ctk_font(11, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=(8, 6))

        lang_row = ctk.CTkFrame(lang_card, fg_color="transparent")
        lang_row.pack(fill="x", padx=12, pady=(0, 8))

        self._lang_var = ctk.StringVar(value=current_lang())
        for code, label in available_langs():
            ctk.CTkRadioButton(
                lang_row, text=label, variable=self._lang_var,
                value=code, command=self._on_lang_changed,
                radiobutton_width=18, radiobutton_height=18,
            ).pack(side="left", padx=(0, 16))

    def _on_font_slider_moved(self, value):
        lv = int(round(float(value)))
        lv = max(1, min(FONT_SCALE_LEVELS, lv))
        sc = _font_scale_from_level(lv)
        if abs(sc - self._font_scale) < 0.0001:
            return
        self._set_font_scale(sc)

    def _set_font_scale(self, scale: float):
        self._font_scale = scale
        # 整页重建（含设置页上的缩放标签），避免对已销毁控件 configure
        self.show_page(self._current_page)

    def _on_lang_changed(self):
        lang = set_lang(self._lang_var.get())
        self.show_page(self._current_page)


# ── 入口 ─────────────────────────────────────────────────────────────────────

def main():
    try:
        app = App()
        app.title(_("app.title") + " - v2.1.0")
        app.mainloop()
    except Exception:
        traceback.print_exc()
        input(_("app.exit_hint"))


if __name__ == "__main__":
    main()
