# GitHub Release 发布 Mod 指南

> 面向无发布经验的 Mod 作者，以 NoClientCheats 为例说明从仓库到发行版的全流程。

---

## 一、整体逻辑

| 概念 | 说明 |
|------|------|
| **仓库 (Repository)** | 放源码、README、构建脚本的地方 |
| **Tag** | 版本标签（如 v1.0.0），每个 Release 绑定一个 Tag |
| **Release** | 面向用户的「发行版」，包含版本说明和**可下载附件**（zip 等） |
| **附件 (Assets)** | 用户实际下载的 Mod 打包文件（如 NoClientCheats-v1.0.0.zip） |

**流程**：构建 Mod → 打包 zip → 创建 Release → 上传 zip 作为附件 → 用户从 Releases 页下载。

---

## 二、发布前准备

### 1. 构建 Mod
```powershell
cd K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats
.\build.ps1
```
产物在 `{游戏}\mods\NoClientCheats\`。

### 2. 准备发行包
```powershell
.\prepare-release.ps1 -Version "1.0.0"
```
会生成 `release\NoClientCheats-v1.0.0.zip`。  
zip 内为 `NoClientCheats\` 目录（含 dll、pck、mod_manifest.json），用户解压后把该目录放入游戏 `mods` 下即可。

---

## 三、发布方式

### 方式 A：GitHub 网页（适合新手）

1. 打开仓库 → 右侧 **Releases** → **Create a new release**
2. **Choose a tag**：输入 `v1.0.0`，选择 **Create new tag**
3. **Release title**：如 `No Client Cheats v1.0.0`
4. **Description**：版本说明，可写更新内容
5. 在 **Attach binaries** 区域，拖入或选择 `NoClientCheats-v1.0.0.zip`
6. 点击 **Publish release**

### 方式 B：GitHub CLI（命令行）

先安装 [GitHub CLI](https://cli.github.com/) 并登录：

```bash
gh auth login
```

创建并上传 Release：

```bash
cd K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats
gh release create v1.0.0 ./release/NoClientCheats-v1.0.0.zip --title "No Client Cheats v1.0.0" --notes "首次发布。仅房主需安装。"
```

---

## 四、README 中指向 Releases

在 README.md 的安装说明里，把 `YOUR_USERNAME` 换成实际 GitHub 用户名，例如：

```markdown
从 [Releases](https://github.com/YOUR_USERNAME/NoClientCheats/releases) 下载最新版 ...
```

---

## 五、版本更新时的流程

1. 修改 `mod_manifest.json` 中的 `version`
2. 执行 `.\build.ps1`
3. 执行 `.\prepare-release.ps1 -Version "1.0.1"`
4. 按方式 A 或 B 创建新 Release（tag 如 `v1.0.1`），上传新的 zip

---

## 六、常见问题

| 问题 | 说明 |
|------|------|
| 用户解压后 Mod 不生效 | 确保 zip 内是 `NoClientCheats\` 目录，用户应得到 `mods\NoClientCheats\` 最终路径 |
| 不想用 tag | Release 必须绑定 tag；可先创建 tag，再建 Release |
| 附件大小限制 | GitHub 单文件建议 < 100MB；Mod 通常很小 |

---

## 七、参考

- [Managing releases in a repository - GitHub Docs](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
- [gh release create - GitHub CLI](https://cli.github.com/manual/gh_release_create)
