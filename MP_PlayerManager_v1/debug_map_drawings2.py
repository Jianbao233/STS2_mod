# -*- coding: utf-8 -*-
"""
map_drawings 深度调试
"""
import base64
import gzip
import json
from pathlib import Path

SAVE_PATH = Path(
    r"c:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594"
    r"\modded\profile1\saves\current_run_mp.save"
)
with open(SAVE_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

b64str = data.get("map_drawings", "")

# gzip 解压
raw = base64.b64decode(b64str)
print(f"raw 长度: {len(raw)}")
print(f"raw 十六进制: {raw.hex()}")

# 分析二进制结构（当前存档的 gzip 内容）
# gzip 头: 1f 8b 08 00 00 00 00 00 00 03
# 数据: 02 00 00 00 vN Ca h 01 00 ...
#       02 00 00 00 = 小端 int32 = 2?
# 尝试不同的 int 大小端解释
import struct
if len(raw) >= 4:
    val_le32 = int.from_bytes(raw[:4], 'little')
    val_be32 = int.from_bytes(raw[:4], 'big')
    val_le64 = int.from_bytes(raw[:8], 'little')
    print(f"前4字节 小端int32: {val_le32}, 大端int32: {val_be32}")
    print(f"前8字节 小端int64: {val_le64}")

    # 可能是 varint 编码 (protobuf style)
    def read_varint(data, offset=0):
        result = 0
        shift = 0
        i = offset
        while True:
            if i >= len(data):
                break
            byte = data[i]
            i += 1
            result |= (byte & 0x7F) << shift
            if not (byte & 0x80):
                break
            shift += 7
        return result, i

    # 尝试作为 protobuf map<string, DrawingData> 或类似格式
    print(f"\n尝试 varint 解析:")
    for start in [0, 4, 8]:
        v, nxt = read_varint(raw, start)
        print(f"  offset={start}: varint={v}, next_offset={nxt}")

    # 打印所有字节
    print(f"\n完整字节内容 (每8字节为一组):")
    for i in range(0, len(raw), 8):
        chunk = raw[i:i+8]
        le_values = []
        for size in [1, 2, 4, 8]:
            if len(chunk) >= size:
                try:
                    le_values.append(int.from_bytes(chunk[:size], 'little'))
                except:
                    pass
        print(f"  [{i:04d}]: {chunk.hex():<24} le={le_values}")
