#!/usr/bin/env python3
"""Analyze Godot 4.x PCK file structure - improved parser"""

import struct
import sys

def analyze_pck_detailed(filename):
    """Analyze PCK file with detailed byte inspection"""
    with open(filename, 'rb') as f:
        data = f.read()
    
    print(f"File size: {len(data)} bytes ({len(data)/1024:.1f} KB)")
    print()
    
    # Parse header
    magic = struct.unpack('<I', data[0:4])[0]
    print(f"Magic: 0x{magic:08X} = '{magic.to_bytes(4, 'little').decode('ascii', errors='replace')}'")
    
    version = struct.unpack('<I', data[4:8])[0]
    major = struct.unpack('<I', data[8:12])[0]
    minor = struct.unpack('<I', data[12:16])[0]
    patch = struct.unpack('<I', data[16:20])[0]
    
    print(f"PCK Version: {version}")
    print(f"Engine: {major}.{minor}.{patch}")
    
    # Header format varies by version
    if version <= 1:
        # v0/v1: 84 bytes header
        print("\n=== PCK v0/v1 format (header 84 bytes) ===")
        file_count = struct.unpack('<I', data[84:88])[0]
        dir_offset = 88
    elif version == 2:
        # v2: adds flags and file_base
        print("\n=== PCK v2 format ===")
        flags = struct.unpack('<I', data[84:88])[0]
        file_base = struct.unpack('<Q', data[88:96])[0]
        file_count = struct.unpack('<I', data[96:100])[0]
        dir_offset = 100
    elif version >= 3:
        # v3: adds directory offset at bytes 84-92
        print("\n=== PCK v3 format (directory at end) ===")
        dir_offset_header = struct.unpack('<Q', data[84:92])[0]
        print(f"Directory offset from header: {dir_offset_header}")
        
        # Directory is at end of file for v3
        # First 4 bytes after header are padding, then file data
        dir_offset = 0x54E0  # Known location from analysis
        if dir_offset >= len(data):
            dir_offset = len(data) - 2000  # fallback
        
        file_count = 0  # Will be read from directory
    
    print(f"File count: {file_count}")
    print(f"Directory at offset: {dir_offset}")
    
    # Parse directory entries
    current = dir_offset
    file_count_actual = struct.unpack('<I', data[current:current+4])[0]
    print(f"\nActual file count from directory: {file_count_actual}")
    
    entries = []
    current += 4  # skip file count
    
    for i in range(file_count_actual):
        if current + 4 > len(data):
            print(f"End of data at entry {i+1}")
            break
        
        # Name length
        name_len = struct.unpack('<I', data[current:current+4])[0]
        current += 4
        
        # Name
        if current + name_len > len(data):
            print(f"Truncated name at entry {i+1}")
            break
        name = data[current:current+name_len].decode('utf-8', errors='replace')
        current += name_len
        
        # Pad to 8 bytes
        pad = (8 - (name_len % 8)) % 8
        current += pad
        
        # After name, fields are different in Godot 4.3+:
        # uint32 flags, uint64 offset, uint64 size, 16 bytes md5, uint32 flags2
        
        if current + 4 > len(data):
            print(f"Truncated at entry {i+1}")
            break
        
        # First uint32 after name could be flags or part of offset
        test_val = struct.unpack('<I', data[current:current+4])[0]
        
        # Check if this looks like flags (small value) or offset (large value)
        if test_val < 0x100 and current + 20 <= len(data):
            # Likely flags first
            flags = test_val
            current += 4
            
            offset_lo = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            offset_hi = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            file_offset = (offset_hi << 32) | offset_lo
            
            size_lo = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            size_hi = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            file_size = (size_hi << 32) | size_lo
            
            md5 = data[current:current+16]
            current += 16
        else:
            # Offset first (uint64)
            offset_lo = test_val
            current += 4
            offset_hi = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            file_offset = (offset_hi << 32) | offset_lo
            
            size_lo = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            size_hi = struct.unpack('<I', data[current:current+4])[0]
            current += 4
            file_size = (size_hi << 32) | size_lo
            
            md5 = data[current:current+16]
            current += 16
            
            flags = struct.unpack('<I', data[current:current+4])[0]
            current += 4
        
        entries.append({
            'name': name,
            'offset': file_offset,
            'size': file_size,
            'flags': flags,
            'md5': md5.hex()[:32]
        })
        
        encryption = " [encrypted]" if (flags & 1) else ""
        removal = " [removal]" if (flags & 2) else ""
        
        print(f"  {i+1}. {name}{encryption}{removal}")
        print(f"     Offset: {file_offset}, Size: {file_size}, Flags: 0x{flags:08X}")
    
    return entries

def extract_files(filename, entries, base_offset=0):
    """Try to extract files from PCK"""
    with open(filename, 'rb') as f:
        data = f.read()
    
    print(f"\n=== Extracting files ===")
    
    # File data starts after header
    # For v3, data starts at byte 92 (after 84-byte header + 8-byte dir offset)
    file_data_start = 92
    
    for i, entry in enumerate(entries):
        name = entry['name']
        offset = entry['offset']
        size = entry['size']
        
        # Convert relative offset to absolute
        abs_offset = file_data_start + offset
        
        if abs_offset + size > len(data):
            print(f"  {i+1}. {name} - INVALID (offset {abs_offset} + size {size} > {len(data)})")
            continue
        
        # Extract file content
        content = data[abs_offset:abs_offset+size]
        
        # Print first 100 bytes as hex + ascii
        preview = content[:min(100, len(content))]
        hex_str = ' '.join(f'{b:02X}' for b in preview)
        ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in preview)
        
        print(f"  {i+1}. {name}")
        print(f"     Size: {size} bytes, Absolute offset: {abs_offset}")
        print(f"     First bytes: {hex_str}")
        print(f"     Ascii: {ascii_str}")

if __name__ == "__main__":
    filename = r"K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\FreeLoadout-STS2_0.99-0.2.0\FreeLoadout.pck"
    if len(sys.argv) > 1:
        filename = sys.argv[1]
    
    entries = analyze_pck_detailed(filename)
    
    if len(entries) > 0:
        extract_files(filename, entries)
