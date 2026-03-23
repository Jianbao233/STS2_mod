# PyQt-SiliconUI 完整架构分析文档

> 基于 GitHub: [ChinaIceF/PyQt-SiliconUI](https://github.com/ChinaIceF/PyQt-SiliconUI) v1.14+ 源码分析
> 分析日期：2026-03-23
> 仓库：https://github.com/ChinaIceF/PyQt-SiliconUI
> 安装：`git clone` 后 `python setup.py install`（暂未发布到 PyPI）
> 许可证：GPLv3 | Stars: ~1,241

---

## 1. 项目定位

PyQt-SiliconUI 是一个**基于 PyQt5/PySide6 的 UI 框架**，面向桌面应用开发，目标：灵动、优雅、轻量。

### 1.1 核心特点

- **零依赖 UI 组件**：不依赖 WebView，自绘控件，完全 Python 实现
- **动画优先**：内置指数动画（SiExpAnimation）、计数器动画（SiCounterAnimation）、弹性动画
- **主题色系统**：集中式颜色管理（SiColorGroup / DarkColorGroup / BrightColorGroup）
- **SVG 图标**：内置 Fluent UI 图标包（ic_fluent_xxx），支持颜色替换
- **DPI 自适应**：自动获取 Windows 缩放因子，响应式布局

### 1.2 ⚠️ 重要警告

> **应用模板（templates/）暂不可用于生产环境。** 官方明确说明 templates 重构中，质量不佳。**重构控件**（widgets/ 下已重构部分）推荐使用。

---

## 2. 模块架构总览

```
siui/
├── __init__.py              ← 包入口，加载缩放因子，导出子模块
│
├── core/                   ← 底层核心
│   ├── __init__.py
│   ├── globals.py           ← SiGlobal（全局状态）、Tooltip 系统
│   ├── animation.py         ← 动画系统（SiExpAnimation 等）
│   ├── alignment.py        ← SiQuickAlignmentManager（布局辅助）
│   ├── color.py            ← SiColor（颜色枚举 + 工具函数）
│   ├── effect.py           ← SiQuickEffect（特效）
│   ├── enumrates.py        ← Si（枚举标志集合）
│   ├── token.py            ← FontStyle / GlobalFont / GlobalFontSize
│   └── painter.py          ← createPainter（绘图工具）
│
├── components/             ← UI 组件
│   ├── __init__.py
│   ├── option_card/        ← 选项卡片组件
│   ├── progress_bar/        ← 进度条
│   ├── slider/              ← 滑块
│   ├── titled_widget_group/ ← 带标题的组件组
│   └── widgets/             ← 重构控件（核心）
│       ├── __init__.py
│       ├── abstracts/       ← 抽象基类
│       │   ├── __init__.py
│       │   ├── widget.py    ← SiWidget 基类
│       │   ├── button.py    ← 按钮基类
│       │   ├── label.py    ← 标签基类
│       │   ├── line_edit.py← 输入框基类
│       │   └── navigation_bar.py
│       ├── button.py        ← SiPushButton / SiToggleButton / SiSwitch 等
│       ├── container.py    ← SiCard / SiSegmentedInput 等
│       ├── label.py         ← SiLabel / SiIconLabel / SiSvgLabel / SiPixLabel 等
│       ├── line_edit.py    ← SiLineEdit
│       └── scrollarea.py    ← SiScrollArea
│
├── templates/              ← 应用模板（⚠️ 重构中，不建议用于生产）
│   └── __init__.py
│
└── gui/                   ← GUI 资源
    ├── __init__.py
    ├── color_group/       ← 颜色组
    │   ├── __init__.py
    │   ├── color_group.py  ← SiColorGroup 基类
    │   ├── dark.py         ← DarkColorGroup（深色主题）
    │   └── bright.py       ← BrightColorGroup（亮色主题）
    ├── font.py             ← SiFont
    ├── icons/             ← SVG 图标系统
    │   ├── __init__.py
    │   ├── parser.py       ← GlobalIconPack（图标加载 + 颜色替换）
    │   └── packages/       ← Fluent UI 图标包（.icons 文件）
    └── scale.py           ← DPI 缩放管理
```

---

## 3. 核心概念

### 3.1 SiGlobal — 全局状态中心

```python
from siui.core import SiGlobal

SiGlobal.siui           # SiliconUIGlobal 实例
SiGlobal.siui.windows    # 窗口字典
SiGlobal.siui.colors    # 颜色字典（DarkColorGroup）
SiGlobal.siui.iconpack   # GlobalIconPack 实例
SiGlobal.siui.icons     # SVG 图标数据
SiGlobal.siui.qss       # 动态样式表
SiGlobal.siui.fonts     # 字体字典
```

### 3.2 SiColor — 颜色令牌

```python
from siui.core import SiColor

SiColor.BUTTON_IDLE           # 按钮默认
SiColor.BUTTON_HOVER          # 按钮悬停
SiColor.BUTTON_FLASH          # 按钮按下
SiColor.TEXT_A / TEXT_B      # 文字颜色
SiColor.THEME                 # 主题色
SiColor.THEME_TRANSITION_A   # 过渡色A
SiColor.THEME_TRANSITION_B   # 过渡色B
SiColor.INTERFACE_BG_A        # 界面背景A
SiColor.PROGRESS_BAR_*       # 进度条相关
SiColor.SIDE_MSG_*            # 侧边消息相关

# 颜色工具函数
SiColor.RGB_to_RGBA("#RRGGBB")           # 加透明度
SiColor.toArray("#RRGGBB")               # 转 numpy array
SiColor.toCode([r,g,b,a])                # array 转色号
SiColor.mix("#color1", "#color2", 0.5)  # 混色
SiColor.trans("#color", 0.5)            # 设置透明度
```

### 3.3 SiColorGroup — 颜色组

颜色组是令牌的**值容器**，支持令牌引用和直接赋值：

```python
from siui.gui import DarkColorGroup

colors = DarkColorGroup()
# 取值
colors['TEXT_A']         # → "#RRGGBB"
colors.fromToken(SiColor.TEXT_A)  # → "#RRGGBB"

# 赋值
colors.assign(SiColor.TEXT_A, "#FF5733")

# 引用其他颜色组
ref = SiColorGroup(reference=SiGlobal.siui.colors)
```

**DarkColorGroup 预定义颜色（关键）**：

| 令牌 | 色值 | 用途 |
|------|------|------|
| `TEXT_A` | `#E4DFD5` | 主文字 |
| `TEXT_B` | `#D1CBD4` | 次文字 |
| `INTERFACE_BG_A` | `#1A1821` | 界面背景 |
| `INTERFACE_BG_B` | `#252331` | 面板背景 |
| `BUTTON_PANEL` | `#343148` | 按钮面板 |
| `BUTTON_SHADOW` | `#1A1821` | 按钮阴影 |
| `BUTTON_THEMED_BG_A` | `#3D3757` | 主题按钮渐变A |
| `BUTTON_THEMED_BG_B` | `#504D72` | 主题按钮渐变B |
| `SCROLL_BAR` | `#3D3757` | 滚动条 |
| `PROGRESS_BAR_PROCESSING` | `#6C63FF` | 进度条进行中 |

---

## 4. 控件体系

### 4.1 SiWidget — 所有控件的基类

继承自 `QWidget`，所有重构控件的父类。提供：

- 内置动画支持（move / resize / opacity / color）
- 令牌颜色系统（`getColor(token)` / `colorGroup()`）
- 样式表热重载（`reloadStyleSheet()`）
- 动画组管理（`animationGroup()`）
- 移动限制（`setMoveLimits()`）
- 中心组件显示动画（`showCenterWidgetFadeIn()`）

**生命周期方法**（规范约定）：

```python
class MyWidget(SiWidget):
    def __init__(self):
        super().__init__()
        self.your_var = None
        self._initWidget()   # 1. 声明子控件
        self._initStyle()    # 2. 设置样式/颜色/字体
        self._initLayout()   # 3. 设置布局/几何
        self._initAnimation()# 4. 绑定动画信号

    def _initWidget(self):    pass
    def _initStyle(self):     pass
    def _initLayout(self):    pass
    def _initAnimation(self): pass

    def reloadStyleSheet(self):
        # 重载样式表时调用（窗口show或主题切换时自动触发）
        pass
```

**动画使用示例**：

```python
def _initAnimation(self):
    # 移动动画
    self.animation_move = self.animationGroup().fromToken("move")
    self.animation_move.setFactor(1/4)
    self.animation_move.setBias(1)
    self.animation_move.ticked.connect(lambda pos: self.move(int(pos[0]), int(pos[1])))

# 触发动画
self.animation_move.setTarget([200, 100])
self.animation_move.try_to_start()

# 颜色动画
self.animation_color = self.animationGroup().fromToken("color")
self.animation_color.ticked.connect(lambda rgb: self.setStyleSheet(
    f"background-color: {SiColor.toCode(rgb)}"
))
self.animation_color.setTarget(SiColor.toArray("#FF5733"))
self.animation_color.try_to_start()
```

### 4.2 按钮系列

```python
from siui.components import SiPushButton, SiToggleButton, SiLongPressButton
from siui.components import SiSimpleButton, SiSwitch, SiCheckBox, SiRadioButton

# 普通按钮
btn = SiPushButton()
btn.setText("Click Me")
btn.clicked.connect(handler)

# 主题渐变按钮
btn.setUseTransition(True)

# 长按按钮
long_btn = SiLongPressButton()
long_btn.longPressed.connect(handler)

# 切换按钮（双态）
toggle = SiToggleButton()
toggle.toggled.connect(lambda on: print("on" if on else "off"))

# 开关
switch = SiSwitch()
switch.toggled.connect(lambda on: print("switched:", on))

# 单选框
radio = SiRadioButton(parent)
radio.setText("Option A")
radio.toggled.connect(lambda checked: ...)

# 多选框
check = SiCheckBox(parent)
check.setText("Option B")
check.toggled.connect(lambda checked: ...)
```

### 4.3 标签系列

```python
from siui.components import SiLabel, SiIconLabel, SiSvgLabel, SiPixLabel, SiFlashLabel

# 基础文字标签
label = SiLabel()
label.setText("Hello")

# SVG 图标标签
icon = SiSvgLabel()
icon.load("path/to/icon.svg")  # 路径或 SVG 字符串
icon.setSvgSize(20, 20)

# 图标+文字组合（自动排列）
icon_label = SiIconLabel()
icon_label.load(svg_bytes)     # 加载 SVG 图标
icon_label.setText("Settings")   # 文字
icon_label.adjustSize()          # 自适应大小

# 图片标签（支持圆角）
pix = SiPixLabel()
pix.load("path/to/image.png")
pix.setBorderRadius(12)

# 闪烁标签
flash_label = SiFlashLabel()
flash_label.setFlashColor("#FF5733")
flash_label.flash()  # 触发一次闪烁
```

### 4.4 容器系列

**SiCard — 卡片容器**：

```python
from siui.components import SiCard

card = SiCard()
card.setTitle("玩家列表")
card.setTitleAlignment(Qt.AlignLeft)
card.setBodyContent(my_widget)  # 设置主体内容
```

> 具体 SiCard API 见 `siui/components/widgets/container.py`

---

## 5. 动画系统

### 5.1 动画类型

| 类名 | 类型 | 说明 |
|------|------|------|
| `SiExpAnimation` | 指数动画 | 步长与当前值相关，越接近目标越慢 |
| `SiExpAccelerateAnimation` | 加速指数动画 | 可自定义加速函数 |
| `SiCounterAnimation` | 计数器动画 | 在固定时长内从A变化到B，支持曲线 |
| `SiAnimationGroup` | 动画组 | 以 token 管理多个动画 |

### 5.2 SiExpAnimation 详解

```python
ani = SiExpAnimation()
ani.setFactor(1/8)    # 步长系数，越小动画越慢（0~1）
ani.setBias(0.5)     # 停止阈值，越小越精确
ani.setCurrent(start_val)
ani.setTarget(end_val)
ani.ticked.connect(handler)   # 每帧触发
ani.finished.connect(handler)  # 动画完成时触发
ani.try_to_start()           # 安全启动（避免重复启动）
```

### 5.3 SiCounterAnimation（时长动画）

```python
ani = SiCounterAnimation()
ani.setDuration(1000)       # 动画时长 1000ms
ani.setReversed(False)       # 正向/反向
ani.setCurve(Curve.LINEAR)   # 缓动曲线
ani.setTarget(100)           # 目标值
ani.ticked.connect(lambda v: print(v))  # v 从 0→100
ani.finished.connect(lambda: print("done"))
```

---

## 6. 图标系统

### 6.1 GlobalIconPack

```python
from siui.core import SiGlobal

iconpack = SiGlobal.siui.iconpack
iconpack.setDefaultColor("#D1CBD4")   # 设置默认颜色

# 获取 SVG bytes（可传给 QSvgWidget）
svg_bytes = iconpack.get("ic_fluent_settings_filled")

# 获取 QPixmap
pixmap = iconpack.toPixmap("ic_fluent_add_filled", QSize(24, 24))

# 获取 QIcon
qicon = iconpack.toIcon("ic_fluent_save_filled", QSize(64, 64))

# 自定义颜色
svg_bytes = iconpack.get("ic_fluent_warning_filled", "#FF5733")
```

### 6.2 常用图标名（Fluent UI）

```
操作类：
ic_fluent_add_filled          # 添加
ic_fluent_delete_filled       # 删除
ic_fluent_save_filled         # 保存
ic_fluent_settings_filled     # 设置
ic_fluent_edit_filled         # 编辑
ic_fluent_copy_filled         # 复制

状态类：
ic_fluent_info_regular        # 信息
ic_fluent_warning_filled      # 警告
ic_fluent_hand_wave_filled    # 打招呼
ic_fluent_checkmark_filled    # 勾选
ic_fluent_dismiss_filled      # 关闭/取消

导航类：
ic_fluent_home_filled         # 主页
ic_fluent_folder_filled       # 文件夹
```

---

## 7. 缩放因子与 DPI

```python
import siui
from siui.gui import reload_scale_factor

# 初始化（必须在创建任何窗口前调用）
reload_scale_factor()  # 自动从 Windows API 获取当前 DPI 缩放

# 设置环境变量
# QT_SCALE_FACTOR 环境变量会被 Qt 使用
```

**注意**：必须在 `import siui` 后、创建窗口前调用 `reload_scale_factor()`。

---

## 8. 在本项目中的应用规划

### 8.1 外部工具 GUI 使用场景

MP_PlayerManager 外部工具的 GUI 需求：

| 页面 | 需要的控件 |
|------|-----------|
| 存档选择 | SiCard（存档信息卡片）+ SiPushButton |
| 玩家列表 | SiCard（每个玩家）+ SiToggleButton（删除/替换） |
| 模板预览 | SiCard（角色摘要）+ SiLabel（角色名/卡组数） |
| 模板选择 | SiCard 列表 + SiPushButton（导入/应用） |
| 确认保存 | SiCard（变更摘要）+ SiPushButton（确认/取消） |

### 8.2 推荐使用清单

```
✅ 推荐使用（已重构）：
- SiPushButton      — 普通按钮
- SiToggleButton    — 开关按钮
- SiSwitch          — 滑动开关
- SiCheckBox        — 多选框
- SiLabel           — 文字标签
- SiIconLabel       — 图标+文字
- SiSvgLabel        — SVG 图标
- SiPixLabel        — 图片标签
- SiCard            — 卡片容器（container.py）
- SiScrollArea      — 滚动区域（scrollarea.py）

⚠️ 慎用/暂不用：
- SiLineEdit         — 重构中，可能不稳定
- templates/         — 重构中，禁止用于生产
- 未列出的旧组件       — 可能有缺陷
```

### 8.3 基础模板代码

```python
import sys
from PyQt5.QtWidgets import QApplication, QMainWindow
import siui
from siui.gui import reload_scale_factor

reload_scale_factor()  # 必须先调用

from siui.components import SiPushButton, SiCard

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("MP_PlayerManager")
        self.resize(900, 600)

        # 主卡片
        card = SiCard(self)
        card.resize(800, 500)
        card.setTitle("玩家列表")
        card.setTitleAlignment(Qt.AlignLeft)

        # 按钮
        btn = SiPushButton(self)
        btn.setText("添加玩家")
        btn.resize(120, 40)
        btn.clicked.connect(self.on_add)

        self.setCentralWidget(card)

    def on_add(self):
        print("添加玩家")

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec_())
```

---

## 9. 依赖与环境

```
Python >= 3.8
PyQt5 >= 5.15.10      # 核心依赖
numpy                 # 动画系统数组运算
pyperclip             # 剪贴板工具（可选）
```

**安装**：
```bash
git clone https://github.com/ChinaIceF/PyQt-SiliconUI.git
cd PyQt-SiliconUI
python setup.py install
```

**图标包安装**（setup.py 会自动复制）：
```
siui/gui/icons/packages/
├── fluent_ui_icon_filled.icons
├── fluent_ui_icon_regular.icons
└── fluent_ui_icon_light.icons
```

---

## 10. 相关链接

| 资源 | 地址 |
|------|------|
| GitHub | https://github.com/ChinaIceF/PyQt-SiliconUI |
| 官方示例 | `examples/Gallery for siui/start.py` |
| 配套应用 My-TODOs | https://github.com/ChinaIceF/My-TODOs |
| 官方文档（中文） | `docs/README_zh.md` |
| 编码规范 | `docs/coding_standard.md` |
