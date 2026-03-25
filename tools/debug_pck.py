#!/usr/bin/env python3
"""Debug PCK directory parsing - examine raw bytes"""

import struct

filename = r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\FreeLoadout-STS2_0.99-0.2.0\FreeLoadout.pck"

with open(filename, 'rb') as f:
    data = f.read()

print(f"File size: {len(data)} bytes\n")

# Directory starts at 0x54E0 = 21728
dir_offset = 0x54E0

# Print raw bytes around directory
print("Bytes before directory (21728-21760):")
print(' '.join(f'{b:02X}' for b in data[21728:21760]))

print("\nFirst 256 bytes of directory:")
for i in range(0, min(256, len(data) - dir_offset), 16):
    hex_str = ' '.join(f'{b:02X}' for b in data[dir_offset+i:dir_offset+i+16])
    ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in data[dir_offset+i:dir_offset+i+16])
    print(f"{dir_offset+i:08X}: {hex_str:<48} |{ascii_str}|")

print("\n" + "="*80)
print("Parsing directory structure:")
print("="*80)

current = dir_offset

# File count
file_count = struct.unpack('<I', data[current:current+4])[0]
print(f"\nFile count: {file_count}")
current += 4

print(f"\n--- Entry 1 ---")
print(f"Offset: {current}")

# Name length
name_len = struct.unpack('<I', data[current:current+4])[0]
print(f"Name length: {name_len}")
current += 4

# Name
name = data[current:current+name_len].decode('utf-8', errors='replace')
print(f"Name: {name}")
current += name_len

# Pad to 8-byte alignment
pad = (8 - (name_len % 8)) % 8
print(f"Padding: {pad} bytes")
current += pad

print(f"After name padding: {current}")

# According to Godot 4.5 source, the format is:
# uint64 offset (relative to file base)
# uint64 size
# 16 bytes MD5
# uint32 flags

# Try parsing as offset (uint64 LE)
offset_bytes = data[current:current+8]
offset = struct.unpack('<Q', offset_bytes)[0]
print(f"Offset (uint64): {offset} (0x{offset:016X})")
current += 8

# Size (uint64 LE)
size_bytes = data[current:current+8]
size = struct.unpack('<Q', size_bytes)[0]
print(f"Size (uint64): {size} (0x{size:016X})")
current += 8

# MD5 (16 bytes)
md5 = data[current:current+16]
print(f"MD5: {md5.hex()}")
current += 16

# Flags (uint32)
flags = struct.unpack('<I', data[current:current+4])[0]
print(f"Flags: 0x{flags:08X}")
current += 4

print(f"\n--- Entry 2 ---")
print(f"Offset: {current}")

# Name length
name_len = struct.unpack('<I', data[current:current+4])[0]
print(f"Name length: {name_len}")
current += 4

# Name
name = data[current:current+name_len].decode('utf-8', errors='replace')
print(f"Name: {name}")
current += name_len

# Pad
pad = (8 - (name_len % 8)) % 8
print(f"Padding: {pad} bytes")
current += pad

print(f"After name padding: {current}")

# Offset
offset = struct.unpack('<Q', data[current:current+8])[0]
print(f"Offset (uint64): {offset} (0x{offset:016X})")
current += 8

# Size
size = struct.unpack('<Q', data[current:current+8])[0]
print(f"Size (uint64): {size} (0x{size:016X})")
current += 8

# MD5
md5 = data[current:current+16]
print(f"MD5: {md5.hex()}")
current += 16

# Flags
flags = struct.unpack('<I', data[current:current+4])[0]
print(f"Flags: 0x{flags:08X}")
current += 4

# Check file data area (starts at byte 92)
print("\n" + "="*80)
print("File data area (bytes 92-200):")
print("="*80)
for i in range(0, min(108, len(data) - 92), 16):
    hex_str = ' '.join(f'{b:02X}' for b in data[92+i:92+i+16])
    ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in data[92+i:92+i+16])
    print(f"{92+i:08X}: {hex_str:<48} |{ascii_str}|")

# Check bytes 0x54E0 - 0x54F0 (where file data should start at offset 46)
print("\nBytes at offset where file 1 data should be (0x54E0 = 21728):")
for i in range(0, min(64, len(data) - 0x54E0), 16):
    hex_str = ' '.join(f'{b:02X}' for b in data[0x54E0+i:0x54E0+i+16])
    ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in data[0x54E0+i:0x54E0+i+16])
    print(f"{0x54E0+i:08X}: {hex_str:<48} |{ascii_str}|")
