#!/usr/bin/env python3
"""Analyze Godot 4.x PCK file structure"""

import struct
import sys

def read_pck_header(filename):
    """Read and analyze PCK file header"""
    with open(filename, 'rb') as f:
        data = f.read()
    
    print(f"File size: {len(data)} bytes")
    print(f"\nFirst 128 bytes (hex):")
    print(' '.join(f'{b:02X}' for b in data[:128]))
    print()
    
    if len(data) < 32:
        print("File too small for valid PCK header")
        return
    
    # Magic number (first 4 bytes) - should be "GDPC" = 0x43504447
    magic = struct.unpack('<I', data[0:4])[0]
    print(f"Magic: 0x{magic:08X} ({magic.to_bytes(4, 'little').decode('ascii', errors='replace')})")
    
    if magic == 0x43504447:
        print("[OK] Valid Godot PCK magic number!")
    else:
        print("[X] Invalid magic number - this might not be a PCK file")
        print(f"  Expected: 0x43504447 (GDPC)")
        print(f"  Got:      0x{magic:08X}")
        return
    
    # PCK Version
    version = struct.unpack('<I', data[4:8])[0]
    print(f"PCK Version: {version}")
    
    # Engine version (3 uint32s)
    major = struct.unpack('<I', data[8:12])[0]
    minor = struct.unpack('<I', data[12:16])[0]
    patch = struct.unpack('<I', data[16:20])[0]
    print(f"Engine Version: {major}.{minor}.{patch}")
    
    # Reserved (16 uint32s = 64 bytes)
    reserved = data[20:84]
    print(f"Reserved: {reserved[:32].hex()}")
    
    # Version-specific parsing
    print()
    if version <= 1:
        print("PCK Version 0/1: Directory follows header immediately")
        parse_directory_v0_v1(data, 84)
    elif version == 2:
        print("PCK Version 2: Encryption support")
        flags = struct.unpack('<I', data[84:88])[0]
        print(f"Flags: 0x{flags:08X}")
        file_base = struct.unpack('<Q', data[88:96])[0]
        print(f"File Base Offset: {file_base}")
        parse_directory_v0_v1(data, 96)
    elif version >= 3:
        print(f"PCK Version {version}: Directory at end of file")
        
        # Search for directory - in v3, it's at the end
        # Look backwards from end for GDPC magic or file count
        found_dir = False
        for search_start in range(len(data) - 100, max(0, len(data) - 50000), -1):
            if data[search_start:search_start+4] == b'GDPC':
                print(f"\nFound GDPC magic at offset {search_start} - directory")
                dir_offset = search_start
                found_dir = True
                break
        
        if not found_dir:
            # Try the uint64 at offset 84
            dir_offset_bytes = data[84:92]
            print(f"\nBytes at offset 84-91: {' '.join(f'{b:02X}' for b in dir_offset_bytes)}")
            dir_offset_val = struct.unpack('<Q', dir_offset_bytes)[0]
            print(f"Directory offset from header (uint64): {dir_offset_val} (0x{dir_offset_val:08X})")
            
            # Check if it looks valid
            if 92 < dir_offset_val < len(data):
                dir_offset = dir_offset_val
                found_dir = True
        
        if not found_dir:
            # Default: search for file count (should be uint32 with small value)
            for i in range(92, len(data) - 100):
                val = struct.unpack('<I', data[i:i+4])[0]
                if 0 < val < 100:  # Reasonable file count
                    # Check if next bytes look like a file path
                    if i + 8 < len(data) and data[i+8:i+11] in [b'res', b'src', b'mod']:
                        print(f"\nPotential directory at offset {i} (file count: {val})")
                        dir_offset = i
                        found_dir = True
                        break
        
        if not found_dir:
            # Fallback: try offset 22240 (0x54E0)
            dir_offset = 0x54E0
            if dir_offset < len(data):
                print(f"\nTrying directory at offset 0x54E0 = {dir_offset}")
                found_dir = True
        
        if found_dir and dir_offset > 0 and dir_offset < len(data):
            print(f"\nLast 64 bytes before directory (offset {dir_offset}):")
            print(' '.join(f'{b:02X}' for b in data[max(0,dir_offset-64):dir_offset]))
            print(f"\nDirectory starts at offset {dir_offset}:")
            print(' '.join(f'{b:02X}' for b in data[dir_offset:min(dir_offset+128, len(data))]))
            parse_directory_at_offset(data, dir_offset)

def parse_directory_v0_v1(data, offset):
    """Parse directory for PCK version 0/1"""
    if len(data) < offset + 4:
        print("Not enough data for file count")
        return
    
    file_count = struct.unpack('<I', data[offset:offset+4])[0]
    print(f"File Count: {file_count}")
    
    current_offset = offset + 4
    print(f"\nDirectory entries:")
    for i in range(file_count):
        if len(data) < current_offset + 4:
            break
        
        name_len = struct.unpack('<I', data[current_offset:current_offset+4])[0]
        current_offset += 4
        
        name_bytes = data[current_offset:current_offset + name_len]
        name = name_bytes.decode('utf-8', errors='replace')
        current_offset += name_len
        
        # Pad to 8-byte alignment
        padded_len = (name_len + 7) & ~7
        current_offset += (padded_len - name_len)
        
        file_offset = struct.unpack('<Q', data[current_offset:current_offset+8])[0]
        current_offset += 8
        
        file_size = struct.unpack('<Q', data[current_offset:current_offset+8])[0]
        current_offset += 8
        
        md5 = data[current_offset:current_offset+16]
        current_offset += 16
        
        print(f"  {i+1}. {name}")
        print(f"     Offset: {file_offset}, Size: {file_size}")
        print(f"     MD5: {md5.hex()}")

def parse_directory_at_offset(data, offset):
    """Parse directory at specific offset (PCK v3+)
    
    Directory Entry Format (v3):
    - uint32: Name length
    - name_len bytes: Name (UTF-8)
    - padded to 8 bytes
    - uint64: File offset (relative to file base)
    - uint64: File size
    - 16 bytes: MD5 checksum
    - uint32: Flags (encrypted/deleted)
    """
    if len(data) < offset + 4:
        print("Not enough data for file count")
        return
    
    file_count = struct.unpack('<I', data[offset:offset+4])[0]
    print(f"File Count: {file_count}")
    
    current_offset = offset + 4
    files_info = []
    print(f"\nDirectory entries:")
    for i in range(file_count):
        if len(data) < current_offset + 4:
            print(f"  Warning: Not enough data for entry {i+1}")
            break
        
        # Name length
        name_len = struct.unpack('<I', data[current_offset:current_offset+4])[0]
        current_offset += 4
        
        # Name
        if len(data) < current_offset + name_len:
            print(f"  Warning: Not enough data for name in entry {i+1}")
            break
        name_bytes = data[current_offset:current_offset + name_len]
        name = name_bytes.decode('utf-8', errors='replace')
        current_offset += name_len
        
        # Pad to 8-byte alignment
        padded_len = (name_len + 7) & ~7
        current_offset += (padded_len - name_len)
        
        # File offset (uint64)
        if len(data) < current_offset + 8:
            print(f"  Warning: Not enough data for offset in entry {i+1}")
            break
        file_offset = struct.unpack('<Q', data[current_offset:current_offset+8])[0]
        current_offset += 8
        
        # File size (uint64)
        if len(data) < current_offset + 8:
            print(f"  Warning: Not enough data for size in entry {i+1}")
            break
        file_size = struct.unpack('<Q', data[current_offset:current_offset+8])[0]
        current_offset += 8
        
        # MD5 (16 bytes)
        if len(data) < current_offset + 16:
            print(f"  Warning: Not enough data for MD5 in entry {i+1}")
            break
        md5 = data[current_offset:current_offset+16]
        current_offset += 16
        
        # Flags (uint32)
        if len(data) < current_offset + 4:
            print(f"  Warning: Not enough data for flags in entry {i+1}")
            break
        flags = struct.unpack('<I', data[current_offset:current_offset+4])[0]
        current_offset += 4
        
        encryption = " [encrypted]" if (flags & 1) else ""
        removal = " [removal]" if (flags & 2) else ""
        
        files_info.append({
            'name': name,
            'offset': file_offset,
            'size': file_size,
            'flags': flags
        })
        
        print(f"  {i+1}. {name}{encryption}{removal}")
        print(f"     Flags: 0x{flags:08X}, Offset: {file_offset}, Size: {file_size}")
    
    return files_info

if __name__ == "__main__":
    filename = r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\FreeLoadout-STS2_0.99-0.2.0\FreeLoadout.pck"
    if len(sys.argv) > 1:
        filename = sys.argv[1]
    read_pck_header(filename)
