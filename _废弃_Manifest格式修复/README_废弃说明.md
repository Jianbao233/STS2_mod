# 废弃项目：Manifest 格式修复工具

**废弃日期**：2026-03-18

## 项目说明

本文件夹包含 STS2 模组 Manifest 格式修复相关脚本，用于帮助遇到「检测到错误」或 manifest 格式问题的玩家自动修复 `mod_manifest.json`。

## 废弃原因

用户决定取消整个格式修复项目，将相关文件归档至此。

## 包含文件

- `修复模组Manifest格式.bat` -  standalone 单文件 bat（内嵌 PowerShell 逻辑）
- `STS2_ModManifestFixer.bat` - 英文版 bat（由 _build_single_bat.ps1 生成）
- `fix_mod_manifests.ps1` - 完整版 PowerShell 修复脚本（需与 bat 配合使用）
- `_build_single_bat.ps1` - 生成 standalone bat 的构建脚本

## 功能概览（仅供参考）

- [1] 扫描并修复 manifest 格式（id、has_pck、has_dll、dependencies、affects_gameplay）
- [2] 仅预览不修改
- [3] 修复 mod_mainfest.json 拼写错误
- [4] 处理数据文件（VC_STS2_FULL_IDS 移至游戏根、settings.json → settings.cfg）

## 历史问题

- CMD echo 中文时易触发解析错误（「误文件名」「正确名」等被误识别为命令）
- 最终方案为菜单改为纯 ASCII，但项目整体已废弃
