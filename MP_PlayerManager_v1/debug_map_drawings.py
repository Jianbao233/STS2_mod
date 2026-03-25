# -*- coding: utf-8 -*-
"""
map_drawings 解码调试脚本
"""
import base64
import gzip
import zlib
import json
from pathlib import Path

SAVE_PATH = Path(
    r"c:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\modded\profile1\saves\current_run_mp.save"
)

with open(SAVE_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

b64str = data.get("map_drawings", "")
print(f"原始长度: {len(b64str)}")
print(f"前40字符: {b64str[:40]}")

# 方法1: 标准 Base64 -> gzip -> 解压
try:
    raw = base64.b64decode(b64str)
    print(f"\n方法1 (gzip): 解码后 raw 长度={len(raw)}, 前8字节={raw[:8].hex()}")
    dec = gzip.decompress(raw)
    print(f"解压后: {dec[:200]}")
except Exception as e:
    print(f"方法1失败: {e}")

# 方法2: zlib
try:
    raw = base64.b64decode(b64str)
    dec = zlib.decompress(raw)
    print(f"\n方法2 (zlib): 解压后: {dec[:200]}")
except Exception as e:
    print(f"方法2失败: {e}")

# 方法3: 直接 gzip 不解码
try:
    dec = gzip.decompress(b64str.encode('ascii'))
    print(f"\n方法3 (gzip 直解): {dec[:200]}")
except Exception as e:
    print(f"方法3失败: {e}")

# 方法4: zlib 直解
try:
    dec = zlib.decompress(b64str.encode('ascii'))
    print(f"\n方法4 (zlib 直解): {dec[:200]}")
except Exception as e:
    print(f"方法4失败: {e}")

# 方法5: 不解压gzip，只看原始字节
try:
    raw = base64.b64decode(b64str)
    # 尝试 utf-8
    s = raw.decode('utf-8', errors='replace')
    print(f"\n方法5 (raw->utf8): {s[:100]}")
except Exception as e:
    print(f"方法5失败: {e}")

# 看看是不是 json
try:
    raw = base64.b64decode(b64str)
    s = raw.decode('utf-8', errors='replace')
    parsed = json.loads(s)
    print(f"\n方法6 (raw->json): {json.dumps(parsed, ensure_ascii=False)[:300]}")
except Exception as e:
    print(f"\n方法6失败: {e}")

# 对比 backup 里的 map_drawings
BACKUP_PATH = Path(
    r"C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\backups\modded_p1_auto_before_copy_20260313_175703\current_run_mp.save"
)
with open(BACKUP_PATH, "r", encoding="utf-8") as f:
    backup_data = json.load(f)

backup_b64 = backup_data.get("map_drawings", "")
print(f"\n\n备份 map_drawings 长度: {len(backup_b64)}")
print(f"备份前40字符: {backup_b64[:40]}")

try:
    raw = base64.b64decode(backup_b64)
    print(f"备份 raw 长度={len(raw)}, 前8字节={raw[:8].hex()}")
    dec = gzip.decompress(raw)
    print(f"备份解压成功: {json.loads(dec)[:1] if isinstance(dec, bytes) else dec}")
except Exception as e:
    print(f"备份解码失败: {e}")
