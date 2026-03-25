# -*- coding: utf-8 -*-
"""入口脚本（直接运行此文件或打包后的 exe）"""
import sys
from pathlib import Path

# 将 MP_PlayerManager_v2 的父目录加入搜索路径，使其可作为包导入
_tool_dir = Path(__file__).parent.resolve()          # .../MP_PlayerManager_v2
_parent   = _tool_dir.parent                          # .../STS2_mod
if str(_parent) not in sys.path:
    sys.path.insert(0, str(_parent))

from main import main

if __name__ == "__main__":
    main()
