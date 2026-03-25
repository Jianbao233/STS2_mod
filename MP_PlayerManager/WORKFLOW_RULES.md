# MP_PlayerManager 工作流规则

> 本文件为 MP_PlayerManager 项目专属工作流规则，所有开发决策必须遵循本文。  
> 建立日期：2026-03-23  
> 更新日期：2026-03-24

---

## 1. 版本管理

本项目采用**方案文档版本号**制度：

| 版本 | 文档 | 形态 | 状态 |
|------|------|------|------|
| **v1** | `doc/v1_方案文档.md` | 独立 exe 工具（Python + PyInstaller） | 归档 |
| **v2** | `doc/v2_方案文档.md` | Godot Mod（FreeLoadout 扩展）+ 外部工具（PyQt-SiliconUI） | 开发中 |

- 每次对工具的重大诉求变化 → 新建版本文档 `doc/v{N}_方案文档.md`
- 同一版本内的迭代开发 → 在当前文档末尾追加变更记录
- **不许删除历史版本文档**
- 变更记录格式见本文末尾

---

## 2. 目录结构规范

```
MP_PlayerManager/                    ← 项目根目录
├── WORKFLOW_RULES.md               ← 本文件（工作流/架构规范）
├── MEMORY.md                       ← 子记忆文本（项目动态状态）
├── DEVLOG.md                       ← 开发细化记录（v2 Python 工具专属）
├── SPEC.md                         ← 目标规格说明（当前版本 v2）
├── doc/
│   ├── v1_方案文档.md              ← 第一版方案文档（归档）
│   └── v2_方案文档.md              ← 第二版方案文档（当前开发版本）
├── data/
│   └── user_templates.json         ← 用户自定义角色模板
├── tools/
│   ├── extract_icons.py            ← 图标提取工具
│   └── PYQT_SILICON_UI_ANALYSIS.md← PyQt-SiliconUI 完整架构分析
└── FreeLoadout/                    ← Mod 源码（FreeLoadout 扩展）
    ├── src/                        ← C# 源码
    ├── assets/                     ← 备用静态资产
    └── README.md                  ← FreeLoadout 扩展开发说明
```

---

## 3. 核心设计决策

### 3.1 双端架构

本项目分为**游戏内**和**游戏外**两个独立模块，协同工作：

```
游戏内（Mod / C#）
  └── FreeLoadout 扩展
      ├── 模板配置 Tab（TemplatesTab.cs）
      ├── 模板持久化（TemplateManager.cs）
      └── 导出 JSON

游戏外（外部工具 / Python + PyQt-SiliconUI）
  ├── PyQt-SiliconUI（灵动优雅的桌面 UI）
  ├── 读取模板 JSON
  ├── 读取 current_run_mp.save
  └── 向存档注入/替换/删除玩家
```

### 3.2 方案选择：方案2（Mod 内嵌）

| 模块 | 技术选型 | 理由 |
|------|---------|------|
| **游戏内** | FreeLoadout 扩展（C#） | 零资产复用游戏内置资源，无需打包 |
| **游戏外** | **PyQt-SiliconUI** + Python | 灵动优雅，自绘控件 + 内置动画，远优于标准 PyQt5 |
| **数据源** | 游戏内存（`ModelDb`） | 实时同步，无需手动更新 extracted/ |
| **存档操作** | JSON 编辑 | 游戏关闭后对 `current_run_mp.save` 编辑 |

**关于 PyQt-SiliconUI**：
- GitHub: https://github.com/ChinaIceF/PyQt-SiliconUI
- 必读文档：`tools/PYQT_SILICON_UI_ANALYSIS.md`
- ⚠️ `templates/` 模块禁止用于生产；`SiLineEdit` 重构中，慎用

### 3.3 存档操作原则

- 每次存档操作前**自动备份**
- 不修改 `schema_version` 字段
- 字段类型必须与原存档匹配
- 保留最后一个玩家（`players[]` 不允许为空）

### 3.4 Steam 好友用途

仅用于**显示友好**：读取好友列表，将 `steam_id` 映射为昵称。**不做任何 P2P 连接**。

---

## 4. 环境部署流程

**规则：任何新环境的部署，在执行前必须先告知用户具体操作步骤，获得确认后再执行。**

### 4.1 FreeLoadout 扩展项目部署

步骤（详见 `FreeLoadout/README.md`）：
1. 复制源码：freeloadout解包 → `FreeLoadout/src/`
2. 重命名命名空间：`FreeLoadout` → `MP_PlayerManager`
3. 添加 `mod_manifest.json`
4. `dotnet build` 验证编译

### 4.2 PyQt-SiliconUI 环境

安装步骤：
1. `git clone https://github.com/ChinaIceF/PyQt-SiliconUI.git`
2. `cd PyQt-SiliconUI && python setup.py install`
3. 验证：`python -c "import siui; from siui.gui import reload_scale_factor; reload_scale_factor(); print('OK')"`

---

## 5. 代码约定

- Mod 源码：`FreeLoadout/src/`
- 外部工具源码：`tools/` 下新建目录
- 图标等静态资产：`FreeLoadout/assets/`
- 模板数据：`data/`
- 所有新功能在 `MEMORY.md` 中记录状态

---

## 6. 变更记录追加格式

```markdown
---


## 变更记录（2026-03-24）

### 变更概要
建立三层会话收尾记录规范：MEMORY.md → WORKFLOW_RULES.md → DEVLOG.md

### 详细说明

- 新建 Cursor 规则文件 `MP_PlayerManager_v2/.cursor/rules/session-sync.mdc`
- 规则指定三层记录优先级：MEMORY.md（始终）→ WORKFLOW_RULES.md（架构/规范变更）→ DEVLOG.md（v2 Python 工具细化）
- WORKFLOW_RULES.md 新增 `## 7. 会话收尾记录规范` 章节，引用规则文件路径
- MEMORY.md `## 3.2` 核心文件表加入 `DEVLOG.md`
- WORKFLOW_RULES.md `## 2` 目录结构规范加入 `DEVLOG.md`

---

## 变更记录（YYYY-MM-DD）

### 变更概要
[一句话描述]

### 详细说明
[具体变更内容]
```

---

## 7. 会话收尾记录规范

每次对话有实质变更后，按以下三层结构依次更新：

| 层级 | 文件 | 触发条件 |
|------|------|---------|
| **1** | `MEMORY.md` → `## 10. 会话记录` | **始终** |
| **2** | `WORKFLOW_RULES.md` 末尾 | 架构/流程/规范变更时 |
| **3** | `MP_PlayerManager_v2/DEVLOG.md` | 仅 v2 Python 工具代码变动时 |

详细内容见 Cursor 规则文件：
`MP_PlayerManager_v2/.cursor/rules/session-sync.mdc`
