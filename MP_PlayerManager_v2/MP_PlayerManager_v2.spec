# MP_PlayerManager_v2 — PyInstaller spec
# 打包命令：pyinstaller MP_PlayerManager_v2.spec --clean

import sys
from pathlib import Path

SPEC_ROOT = Path(r"K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager_v2")
PARENT    = SPEC_ROOT.parent
sys.path.insert(0, str(PARENT))

block_cipher = None

a = Analysis(
    ['run.py'],
    pathex=[str(SPEC_ROOT)],
    binaries=[],
    datas=[],
    hiddenimports=[
        "i18n",
        "customtkinter",
        "typing_extensions",
        "darkdetect",
        "packaging",
        "packaging.version",
        "packaging.specifiers",
        "packaging.requirements",
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='MP_PlayerManager-v2.0.0',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
