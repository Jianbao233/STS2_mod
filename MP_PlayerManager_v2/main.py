# -*- coding: utf-8 -*-
"""
MP_PlayerManager v2 — GUI 主程序
CustomTkinter 现代化暗色风格界面
"""

import json
import os
import sys
import traceback
from pathlib import Path

import customtkinter as ctk

# ── 项目模块（绝对导入，兼容 run.py 直接执行） ──────────────────────────────
import MP_PlayerManager_v2.save_io as save_io
import MP_PlayerManager_v2.characters as characters
import MP_PlayerManager_v2.core as core

# ── CustomTkinter 全局配置 ───────────────────────────────────────────────────
ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")

# ── 常量 ─────────────────────────────────────────────────────────────────────
APP_NAME = "MP_PlayerManager v2"
WINDOW_W, WINDOW_H = 1200, 780
NAV_W = 200
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
        self.title("选择 Steam 玩家")
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
            self._count_lbl.configure(text=f"{len(contacts)} 人")
        if hasattr(self, "_loading_lbl") and self._loading_lbl.winfo_exists():
            self._loading_lbl.destroy()
        if hasattr(self, "_hint_lbl") and self._hint_lbl.winfo_exists():
            self._hint_lbl.configure(
                text="好友来自本机 Steam；列表仅绘制 15 行，滚轮或右侧条浏览",
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
            title_row, text="选择 Steam 玩家",
            font=self._fs(14),
            text_color=("#F5A623", "#F5A623"),
        ).pack(side="left")

        self._count_lbl = ctk.CTkLabel(
            title_row, text="加载中…",
            font=self._fs(10),
            text_color=("#6B7280", "#6B7280"),
        )
        self._count_lbl.pack(side="right")

        self._loading_lbl = ctk.CTkLabel(
            list_row,
            text="正在读取 Steam 好友列表…",
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
            placeholder_text="搜索昵称或 Steam ID…",
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
            text="正在读取 Steam 好友列表…",
            font=self._fs(9),
            text_color=("#6B7280", "#6B7280"),
        )
        self._hint_lbl.pack(side="left")

        ctk.CTkButton(
            footer, text="取消", width=80, height=32, corner_radius=6,
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
            item, text="选择", width=60, height=28, corner_radius=6,
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
        nickname = contact.nickname or "（昵称待查）"
        online = contact.online
        game = contact.game_name

        dot_color = "#27AE60" if online else "#6B7280"
        item._dot.configure(fg_color=dot_color)
        item._name_lbl.configure(text=nickname)
        status_text = f"在线：{game}" if (online and game) else ("在线" if online else "离线")
        item._sub_lbl.configure(text=f"ID: {fid}  ·  {status_text}")
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
                text=f"{len(self._contacts)} / {len(self._all_contacts)} 人"
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
            input_row, text="好友",
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
            self._nickname_var.set(f"昵称：{nickname}")
            self._nick_lbl.configure(text=f"昵称：{nickname}")

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
        self.backups: list[save_io.SaveBackup] = []
        self.all_chars: dict[str, characters.CharacterTemplate] = {}
        self.steam_names: dict[str, str] = {}
        self._page_widgets: dict = {}
        self._current_page: str = ""
        self._font_scale: float = FONT_SCALE

        self._setup_window()
        self._build_layout()
        self._load_initial_data()
        self.show_page("save_select")

    # ── 窗口 & 布局 ────────────────────────────────────────────────────────

    def _setup_window(self):
        self.title(APP_NAME)
        self.geometry(f"{WINDOW_W}x{WINDOW_H}")
        self.minsize(900, 600)

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
            top, text="MP_PlayerManager v2",
            font=self._ctk_font(14, weight="bold"),
            text_color=("#F5A623", "#F5A623"),
        ).pack(side="left", padx=16, pady=8)

        self._status_bar = ctk.CTkLabel(
            top, text="正在扫描存档...",
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
            nav, text="功能菜单",
            font=self._ctk_font(11, weight="bold"),
            text_color=("#6B7280", "#6B7280"),
        )
        nav_title.pack(anchor="w", padx=16, pady=(16, 6))

        self._nav_buttons = {}
        nav_items = [
            ("save_select",  "📂  存档选择"),
            ("takeover",     "👤  夺舍玩家"),
            ("add_player",   "➕  添加玩家"),
            ("remove_player","➖  移除玩家"),
            ("backup",       "💾  备份管理"),
            ("settings",     "⚙  设置"),
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

        # 底部占位
        ctk.CTkLabel(nav, text="").pack(expand=True)

        nav_version = ctk.CTkLabel(
            nav, text="v2.0.0",
            font=self._ctk_font(9),
            text_color=("#4B5563", "#4B5563"),
        )
        nav_version.pack(pady=8)

        # 右侧内容区
        self._content = ctk.CTkFrame(body, fg_color="transparent", corner_radius=0)
        self._content.pack(fill="both", expand=True, side="right")

    # ── 数据加载 ───────────────────────────────────────────────────────────

    def _load_initial_data(self):
        try:
            self.all_chars = characters.get_all_characters(GAME_MODS_DIR)
            self.profiles = save_io.scan_save_profiles()
            self._status_bar.configure(
                text=f"就绪  ·  {len(self.profiles)} 份存档  ·  {len(self.all_chars)} 个角色"
            )
        except Exception as e:
            self._status_bar.configure(text=f"初始化出错: {e}")

    def _reload_save(self):
        if self.save_path:
            self.save_data = save_io.load_save(self.save_path) or {}
            self.backups = save_io.scan_backups(self.save_path)
        else:
            self.save_data = {}

    def _refresh_status(self):
        if not self.save_data:
            self._status_bar.configure(text="未加载存档")
            return
        players = self.save_data.get("players", [])
        act_idx = self.save_data.get("current_act_index", 0) + 1
        asc = self.save_data.get("ascension", 0)
        pnames = "、".join(self._player_heading_name(p) for p in players) or "无玩家"
        self._status_bar.configure(
            text=f"存档：{len(players)}名玩家 · 第{act_idx}幕 · 进阶{asc} · {pnames}"
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
        """角色 ID → 中文/模板名"""
        if not character_id:
            return "?"
        cid = character_id.strip()
        t = self.all_chars.get(cid)
        if t:
            return t.name
        return cid.replace("CHARACTER.", "").replace("_", " ")

    def _steam_name_str(self, net_id: str) -> str:
        """Steam ID → 昵称；查不到时返回指定占位文案"""
        name = self.steam_names.get(str(net_id), "").strip()
        return name if name else "[未检索到该玩家Steam名称]"

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
            return f"{head} {steam}  生命 {hp}  金币 {gold}"
        if steam:
            return f"{head} {steam}  {char_cn}  生命 {hp}  金币 {gold}"
        return f"{head} {char_cn}  生命 {hp}  金币 {gold}"

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
            disp = f"{t.name}（{short}）"
            pairs.append((disp, cid))
        return pairs

    # ── 页面：存档选择 ─────────────────────────────────────────────────────

    def _page_save_select(self):
        p = self._page()
        self._section_title(p, "📂", "选择存档")

        if not self.profiles:
            hint = (
                "未找到任何存档。\n\n"
                "请确认：\n"
                "  1. 已退出《杀戮尖塔2》\n"
                "  2. 至少进行过一次多人游戏（modded）\n"
                "  3. %APPDATA%\\SlayTheSpire2\\steam\\ 目录存在"
            )
            ctk.CTkLabel(
                p, text=hint, font=self._ctk_font(10),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w", justify="left",
            ).pack(anchor="w", pady=20)
            ctk.CTkButton(
                p, text="重新扫描", command=self._load_initial_data,
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

        for profile in self.profiles:
            status_tag = "进行中" if profile.is_active else "未开始"
            time_str = profile.save_time.strftime("%m-%d %H:%M") if profile.save_time else "未知"

            card = ctk.CTkFrame(scroll, corner_radius=8,
                                  fg_color=("#1F3460", "#1F3460"))
            card.pack(fill="x", pady=(0, 8), padx=2)

            ctk.CTkLabel(
                card, text=f"[{profile.rel_path}]",
                font=self._ctk_font(11, weight="bold"),
                text_color=("#F5A623", "#F5A623"),
                anchor="w",
            ).pack(anchor="w", fill="x", padx=12, pady=(10, 4))

            info = (f"{profile.player_count} 名玩家  ·  "
                    f"第{profile.act_index + 1}幕  ·  "
                    f"进阶 {profile.ascension}  ·  "
                    f"{status_tag}  ·  {time_str}")
            ctk.CTkLabel(
                card, text=info,
                font=self._ctk_font(9),
                text_color=("#9CA3AF", "#9CA3AF"),
                anchor="w",
            ).pack(anchor="w", fill="x", padx=12, pady=(0, 10))

            # 点击整张卡片加载 + hover 效果（一次性绑定，无重复）
            def _make_load(prof):
                return lambda _: self._load_profile(prof)

            def _on_enter(e, card_=card):
                card_.configure(fg_color=("#2A5090", "#2A5090"))
            def _on_leave(e, card_=card):
                card_.configure(fg_color=("#1F3460", "#1F3460"))

            for widget in card.winfo_children():
                widget.configure(cursor="hand2")
                widget.unbind("<Enter>")
                widget.unbind("<Leave>")
                widget.unbind("<Button-1>")
                widget.bind("<Enter>", _on_enter)
                widget.bind("<Leave>", _on_leave)
                widget.bind("<Button-1>", _make_load(profile))

            card.unbind("<Button-1>")
            card.bind("<Button-1>", _make_load(profile))

        ctk.CTkButton(
            p, text="重新扫描", command=self._load_initial_data,
            width=120, height=34, corner_radius=6,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
            text_color="#A0A0B0",
        ).pack(anchor="w", pady=(12, 0))

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
            p, "👤", "夺舍玩家",
            "选择一名非房主玩家，将游戏状态交接给新玩家。房主（玩家1）受保护，无法被夺舍。",
        )

        if not self.save_data:
            self._warn(p, "请先在「存档选择」中选择存档")
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
                    card, text=f"[{i + 1}] {pname}  [房主]",
                    font=self._ctk_font(10), anchor="w",
                    text_color=("#F5A623", "#F5A623"),
                ).pack(anchor="w", fill="x", padx=12, pady=(10, 2))
                ctk.CTkLabel(
                    card,
                    text=f"{self._steam_name_str(net_id)}  ·  ID：{net_id}  ·  "
                         f"生命 {hp}/{maxhp}  ·  金币 {gold}  ·  "
                         f"{deck_n} 张牌  ·  {relics_n} 件遗物",
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
                    text=f"生命 {hp}/{maxhp}  ·  金币 {gold}  ·  "
                         f"{deck_n} 张牌  ·  {relics_n} 件遗物",
                    font=self._ctk_font(9),
                    text_color=("#9CA3AF", "#9CA3AF"),
                    anchor="w",
                ).pack(anchor="w", fill="x", pady=(0, 8))

        # 接替者信息
        sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep.pack(fill="x", pady=14)

        ctk.CTkLabel(
            p, text="接替者信息",
            font=self._ctk_font(12, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        self._to_steam_id = SteamIDInput(
            p, self.tool_dir, width=340, font_scale=self._font_scale,
        )
        self._to_steam_id.pack(anchor="w", pady=(0, 8))

        # 换角色
        self._takeover_change_char = ctk.BooleanVar(value=False)
        ctk.CTkCheckBox(
            p, text="同时更换角色（遗物将被清空）",
            variable=self._takeover_change_char,
            command=self._toggle_takeover_char,
            checkbox_width=18, checkbox_height=18,
        ).pack(anchor="w", pady=(0, 8))

        self._takeover_char_panel = ctk.CTkFrame(p, fg_color="transparent")
        self._takeover_char_combo = None

        self._warn(p, "⚠ 操作不可逆，夺舍后原玩家数据将被覆盖")

        btn_row = ctk.CTkFrame(p, fg_color="transparent")
        btn_row.pack(fill="x", pady=(12, 0))
        ctk.CTkButton(
            btn_row, text="确认夺舍", command=self._do_takeover,
            width=140, height=38, corner_radius=8,
            font=self._ctk_font(11, weight="bold"),
            fg_color="#F5A623", hover_color="#E09010",
            text_color="#1A1A2E",
        ).pack(side="left", ipadx=12, ipady=4)
        ctk.CTkButton(
            btn_row, text="刷新存档", command=self._do_refresh,
            width=100, height=38, corner_radius=8,
            fg_color="#2A3F7A", hover_color="#3A5FAA",
        ).pack(side="left", padx=(10, 0), ipadx=12, ipady=4)

    def _toggle_takeover_char(self):
        for w in self._takeover_char_panel.winfo_children():
            w.destroy()
        if not self._takeover_change_char.get():
            self._takeover_char_panel.pack_forget()
            return
        self._takeover_char_panel.pack(anchor="w", pady=4)
        ctk.CTkLabel(
            self._takeover_char_panel, text="新角色：",
            font=self._ctk_font(10),
            text_color=("#9CA3AF", "#9CA3AF"),
        ).pack(side="left", anchor="w", padx=(0, 8))
        pairs = self._char_picker_pairs()
        labels = [a for a, _ in pairs]
        self._takeover_char_label_to_id = {a: b for a, b in pairs}
        self._takeover_char_combo = ctk.CTkComboBox(
            self._takeover_char_panel, values=labels or [""],
            variable=ctk.StringVar(value=labels[0] if labels else ""),
            width=320, height=32, corner_radius=6,
            fg_color="#1F3460", button_color="#2A3F7A",
            dropdown_fg_color="#1F3460",
            dropdown_hover_color="#2A5090",
            text_color=("#D1D5DB", "#D1D5DB"),
            dropdown_text_color=("#D1D5DB", "#D1D5DB"),
        )
        self._takeover_char_combo.pack(side="left")

    def _do_takeover(self):
        idx = self._to_selected_idx.get()
        steam_id = self._to_steam_id.get()
        if idx < 0:
            self._msg_box("warning", "未选择", "请先选择要夺舍的玩家")
            return
        if idx == 0:
            self._msg_box("warning", "房主保护", "房主（玩家1）无法被夺舍")
            return

        if not steam_id:
            self._msg_box(
                "info", "请选择 Steam 玩家",
                "请点击右侧「好友」按钮选择接替者，或手动填写 Steam64 位 ID。",
            )
            return

        if not steam_id.isdigit() or len(steam_id) < 15:
            self._msg_box("warning", "ID 格式错误", "请输入正确的 Steam64 位 ID（15-17位数字）")
            return

        new_char_id = None
        if self._takeover_change_char.get() and self._takeover_char_combo:
            lbl = self._takeover_char_combo.get()
            new_char_id = getattr(self, "_takeover_char_label_to_id", {}).get(lbl)

        result = core.take_over_player(self.save_data, idx, int(steam_id), new_character_id=new_char_id)
        if result.success:
            ok = save_io.write_save(self.save_path, self.save_data)
            if ok:
                self._reload_save()
                self._refresh_status()
                self._msg_box("info", "成功", result.message)
            else:
                self._msg_box("error", "保存失败", "存档写入失败，请检查文件权限")
        else:
            self._msg_box("error", "操作失败", result.message)

    # ── 页面：添加玩家 ─────────────────────────────────────────────────────

    def _page_add_player(self):
        p = self._page()
        self._section_title(p, "➕", "添加玩家")

        if not self.save_data:
            self._warn(p, "请先在「存档选择」中选择存档")
            return

        self._add_mode = ctk.StringVar(value="copy")

        mode_frame = ctk.CTkFrame(p, fg_color="transparent")
        mode_frame.pack(anchor="w", pady=(0, 16))

        for val, lbl in [
            ("copy", "复制模式（基于现有玩家）"),
            ("fresh", "模板模式（全新角色）"),
        ]:
            ctk.CTkRadioButton(
                mode_frame, text=lbl, variable=self._add_mode,
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
            p, text="新玩家信息",
            font=self._ctk_font(12, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", pady=(0, 8))

        self._add_steam_id = SteamIDInput(p, self.tool_dir, width=300, font_scale=self._font_scale)
        self._add_steam_id.pack(anchor="w", pady=(0, 8))

        ctk.CTkButton(
            p, text="确认添加", command=self._do_add_player,
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
            self._add_copy_panel, text="源玩家：",
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
            self._add_fresh_panel, text="选择角色：",
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
                "info", "请选择 Steam 玩家",
                "请点击右侧「好友」按钮选择要添加的玩家，或手动填写 Steam64 位 ID。",
            )
            return
        if not steam_id.isdigit() or len(steam_id) < 15:
            self._msg_box("warning", "ID 格式错误", "请输入正确的 Steam64 位 ID")
            return

        # 检查 ID 是否已存在
        for pl in self.save_data.get("players", []):
            if str(pl.get("net_id")) == steam_id:
                self._msg_box("warning", "ID 冲突", f"该 Steam ID ({steam_id}) 已存在于本存档中")
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
                    self._msg_box("warning", "未选择角色", "请先选择要添加的角色")
                    return
                result = core.add_player_fresh(self.save_data, net_id, template)

            if result.success:
                ok = save_io.write_save(self.save_path, self.save_data)
                if ok:
                    self._reload_save()
                    self._refresh_status()
                    self._msg_box("info", "成功", result.message)
                else:
                    self._msg_box("error", "保存失败", "存档写入失败，请检查文件权限")
            else:
                self._msg_box("error", "操作失败", result.message)
        except Exception as e:
            traceback.print_exc()
            self._msg_box("error", "异常", str(e))

    # ── 页面：移除玩家 ─────────────────────────────────────────────────────

    def _page_remove_player(self):
        p = self._page()
        self._section_title(
            p, "➖", "移除玩家",
            "选择要移除的玩家（离线/退出玩家），将清理其在所有数据中的记录。",
        )

        if not self.save_data:
            self._warn(p, "请先在「存档选择」中选择存档")
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
                text=f"{deck_n} 张牌  ·  {len(relics)} 件遗物  ·  {potions_n} 瓶药水",
                font=self._ctk_font(9),
                text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
            ).pack(anchor="w", pady=(0, 2))
            ctk.CTkLabel(
                text_col,
                text=f"遗物：{', '.join(self._relic_display(r) for r in relics[:3])}"
                     f"{'...' if len(relics) > 3 else ''}",
                font=self._ctk_font(8),
                text_color=("#7F8C8D", "#7F8C8D"), anchor="w",
            ).pack(anchor="w", pady=(0, 8))

        self._warn(p, "⚠ 移除后游戏内该玩家将从多人对局中消失，此操作不可撤销")

        ctk.CTkButton(
            p, text="确认移除", command=self._do_remove_player,
            width=140, height=38, corner_radius=8,
            font=self._ctk_font(11, weight="bold"),
            fg_color="#E74C3C", hover_color="#C0392B",
            text_color="#FFFFFF",
        ).pack(anchor="w", pady=(16, 0), ipadx=12, ipady=4)

    def _do_remove_player(self):
        idx = self._rm_selected.get()
        if idx < 0:
            self._msg_box("warning", "未选择", "请先选择要移除的玩家")
            return

        # CustomTkinter 没有 askyesno，手动实现确认框
        confirm = self._confirm_box(
            "确认移除",
            "确定要移除该玩家吗？\n此操作不可撤销。"
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
                    f"· {k}：{v}" for k, v in (result.details or {}).items()
                    if k.startswith("removed")
                )
                self._msg_box("info", "成功", result.message + ("\n\n已清理：\n" + details if details else ""))
            else:
                self._msg_box("error", "保存失败", "存档写入失败，请检查文件权限")
        else:
            self._msg_box("error", "操作失败", result.message)

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
            btn_row, text="确认", width=100, height=34, corner_radius=6,
            fg_color="#E74C3C", hover_color="#C0392B",
            command=lambda: (result.__setitem__(0, True), dlg.destroy()),
        ).pack(side="left", ipadx=8)
        ctk.CTkButton(
            btn_row, text="取消", width=100, height=34, corner_radius=6,
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
            container, text="确定", width=100, height=34, corner_radius=6,
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
        self._section_title(p, "💾", "备份管理")

        if not self.save_path:
            self._warn(p, "请先在「存档选择」中加载存档")
            return

        # 当前存档信息
        info = (
            f"路径：{self.save_path}\n"
            f"玩家：{len(self.save_data.get('players', []))} 名  ·  "
            f"进阶 {self.save_data.get('ascension', 0)}  ·  "
            f"第{self.save_data.get('current_act_index', 0) + 1}幕"
        )
        info_card = ctk.CTkFrame(p, corner_radius=8, fg_color=("#1F3460", "#1F3460"))
        info_card.pack(fill="x", pady=(0, 12), ipady=8, padx=2)
        ctk.CTkLabel(
            info_card, text=info,
            font=self._ctk_font(10), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=4)

        ctk.CTkButton(
            p, text="立即创建备份", command=self._do_backup_now,
            width=140, height=36, corner_radius=8,
            fg_color="#27AE60", hover_color="#1E8449",
            text_color="#FFFFFF",
        ).pack(anchor="w", ipadx=12, ipady=4)

        ctk.CTkLabel(
            p, text="备份历史",
            font=self._ctk_font(12, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", pady=(16, 6))

        self._backup_list = ctk.CTkScrollableFrame(
            p, fg_color="transparent",
            scrollbar_button_color="#2A3F7A",
            scrollbar_button_hover_color="#3A5FAA",
        )
        self._backup_list.pack(fill="both", expand=True)
        self._refresh_backup_list()

    def _do_backup_now(self):
        if not self.save_path:
            return
        bp = save_io.save_backup(self.save_path)
        self._reload_save()
        self._refresh_backup_list()
        self._msg_box("info", "备份成功", f"已创建备份：\n{bp.name}")

    def _refresh_backup_list(self):
        for w in self._backup_list.winfo_children():
            w.destroy()
        for b in self.backups:
            ts = b.timestamp
            time_str = f"{ts[:4]}-{ts[4:6]}-{ts[6:8]} {ts[9:11]}:{ts[11:13]}"

            card = ctk.CTkFrame(
                self._backup_list, corner_radius=8,
                fg_color=("#1F3460", "#1F3460"),
            )
            card.pack(fill="x", pady=(0, 6), padx=2)

            ctk.CTkLabel(
                card, text=f"{time_str}  ·  {b.player_count} 名玩家",
                font=self._ctk_font(10), anchor="w",
            ).pack(side="left", fill="x", expand=True, padx=10, pady=10)

            ctk.CTkButton(
                card, text="恢复", width=60, height=28, corner_radius=6,
                fg_color="#2A3F7A", hover_color="#3A5FAA",
                font=self._ctk_font(9),
                command=lambda bk=b: self._restore_backup(bk),
            ).pack(side="right", padx=(0, 8), pady=8)

    def _restore_backup(self, backup: save_io.SaveBackup):
        confirm = self._confirm_box(
            "确认恢复",
            f"将存档恢复到 {backup.timestamp}？"
        )
        if not confirm:
            return
        if self.save_path:
            ok = save_io.restore_backup(backup.path, self.save_path)
            if ok:
                self._reload_save()
                self._refresh_status()
                self._msg_box("info", "恢复成功", "存档已恢复")
            else:
                self._msg_box("error", "恢复失败", "请检查文件权限")

    # ── 页面：设置 ─────────────────────────────────────────────────────────

    def _page_settings(self):
        p = self._page()
        self._section_title(p, "⚙", "设置")

        # 字体大小
        font_card = ctk.CTkFrame(p, corner_radius=8, fg_color=("#1F3460", "#1F3460"))
        font_card.pack(anchor="w", pady=(0, 12), fill="x", ipady=10)

        ctk.CTkLabel(
            font_card, text="界面字体大小",
            font=self._ctk_font(11, weight="bold"),
            text_color=("#F5A623", "#F5A623"), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=(8, 6))

        ctk.CTkLabel(
            font_card,
            text="拖动滑块切换 10 个字号档位；松手后刷新当前页。设置仅在本次运行有效。",
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        ).pack(anchor="w", fill="x", padx=12, pady=(0, 8))

        slider_row = ctk.CTkFrame(font_card, fg_color="transparent")
        slider_row.pack(fill="x", padx=12, pady=(0, 6))

        ctk.CTkLabel(
            slider_row, text="基准",
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
            slider_row, text="最大",
            font=self._ctk_font(9),
            text_color=("#6B7280", "#6B7280"),
            width=36,
        ).pack(side="left", padx=(6, 0))

        self._font_scale_label = ctk.CTkLabel(
            font_card,
            text=(
                f"当前：第 {cur_lv} / {FONT_SCALE_LEVELS} 档　"
                f"缩放 {self._font_scale * 100:.0f}%"
            ),
            font=self._ctk_font(9),
            text_color=("#9CA3AF", "#9CA3AF"), anchor="w",
        )
        self._font_scale_label.pack(anchor="w", padx=12, pady=(4, 0))

        sep = ctk.CTkFrame(p, height=1, fg_color=("#2A3F5A", "#2A3F5A"))
        sep.pack(fill="x", pady=12)

        items = [
            ("游戏 Mod 目录", GAME_MODS_DIR),
            ("工具目录", self.tool_dir),
            ("当前存档", str(self.save_path) if self.save_path else "未加载"),
            ("可用角色数量", f"{len(self.all_chars)} 个（内置 + Mod）"),
            ("存档扫描根目录",
             os.environ.get("SLAY_THE_SPIRE2_APPDATA", "(默认 Roaming 路径)")),
        ]
        wrap = max(520, WINDOW_W - 260)
        for k, v in items:
            row = ctk.CTkFrame(p, fg_color="transparent")
            row.pack(fill="x", pady=4)
            ctk.CTkLabel(
                row, text=f"{k}：", width=130, anchor="w",
                font=self._ctk_font(10),
                text_color=("#9CA3AF", "#9CA3AF"),
            ).pack(side="left", anchor="nw")
            ctk.CTkLabel(
                row, text=v, font=self._ctk_font(10, family="Consolas"),
                anchor="w", justify="left", wraplength=wrap,
            ).pack(side="left", anchor="nw", fill="x", expand=True)

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


# ── 入口 ─────────────────────────────────────────────────────────────────────

def main():
    try:
        app = App()
        app.mainloop()
    except Exception:
        traceback.print_exc()
        input("按 Enter 退出...")


if __name__ == "__main__":
    main()
