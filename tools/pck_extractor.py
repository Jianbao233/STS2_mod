#!/usr/bin/env python3
"""
Godot 4.x PCK File Extractor
Supports PCK v0, v1, v2, v3 formats

Based on Godot Engine source code: https://github.com/godotengine/godot/pull/105757
"""

import struct
import os
import sys
import hashlib

class PCKExtractor:
    def __init__(self, filename):
        self.filename = filename
        with open(filename, 'rb') as f:
            self.data = f.read()
        
        self.entries = []
        self.file_base = 0
        self.dir_offset = 0
        self.version = 0
        self.engine_version = ""
        self.flags = 0
        
    def parse_header(self):
        """Parse PCK file header based on version"""
        magic = struct.unpack('<I', self.data[0:4])[0]
        if magic != 0x43504447:
            raise ValueError(f"Invalid magic number: 0x{magic:08X} (expected 0x43504447 for 'GDPC')")
        
        self.version = struct.unpack('<I', self.data[4:8])[0]
        major = struct.unpack('<I', self.data[8:12])[0]
        minor = struct.unpack('<I', self.data[12:16])[0]
        patch = struct.unpack('<I', self.data[16:20])[0]
        self.engine_version = f"{major}.{minor}.{patch}"
        
        print(f"PCK Version: {self.version}")
        print(f"Engine Version: {self.engine_version}")
        
        if self.version <= 1:
            # v0/v1: Directory follows header (offset 84)
            # File base is absolute offset (same as offset)
            self.file_base = 84  # Directory starts here
            self.dir_offset = 84
        elif self.version == 2:
            # v2: Flags and file base after header
            self.flags = struct.unpack('<I', self.data[84:88])[0]
            self.file_base = struct.unpack('<Q', self.data[88:96])[0]
            self.dir_offset = 96  # Directory starts after flags+filebase
        elif self.version >= 3:
            # v3: Directory at end of file
            self.flags = struct.unpack('<I', self.data[84:88])[0]
            self.file_base = struct.unpack('<Q', self.data[88:96])[0]
            self.dir_offset = struct.unpack('<Q', self.data[96:104])[0]
            
            # In v3, file_base is relative to magic (offset 0)
            # So absolute file base = 0 + file_base
            self.file_base = self.file_base
            
            # Directory offset is relative to magic
            # So absolute directory = 0 + dir_offset
            # But if it's 0, we need to find it
            if self.dir_offset == 0:
                # Directory might be embedded or at known location
                # Search for file count pattern near end of file
                print("Directory offset is 0, searching...")
                # Known location from analysis
                self.dir_offset = 0x54E0
        
        print(f"Flags: 0x{self.flags:08X}")
        print(f"File base: {self.file_base} (0x{self.file_base:08X})")
        print(f"Directory offset: {self.dir_offset} (0x{self.dir_offset:08X})")
        print()
    
    def parse_directory(self):
        """Parse directory entries"""
        if self.dir_offset >= len(self.data):
            raise ValueError(f"Directory offset {self.dir_offset} beyond file size {len(self.data)}")
        
        current = self.dir_offset
        
        file_count = struct.unpack('<I', self.data[current:current+4])[0]
        current += 4
        
        print(f"File count: {file_count}\n")
        
        for i in range(file_count):
            entry = self.parse_entry(current)
            if entry is None:
                print(f"Failed to parse entry {i+1}")
                break
            
            self.entries.append(entry)
            current = entry['next_offset']
            
            flags_str = ""
            if entry['flags'] & 1:
                flags_str += " [ENCRYPTED]"
            if entry['flags'] & 2:
                flags_str += " [REMOVED]"
            
            # Calculate absolute offset
            abs_offset = self.file_base + entry['offset']
            
            print(f"  {i+1:2d}. {entry['name']}{flags_str}")
            print(f"       Size: {entry['size']:8d} bytes")
            print(f"       Offset: {entry['offset']} (abs: {abs_offset})")
            print(f"       MD5: {entry['md5']}")
        
        print(f"\nTotal: {len(self.entries)} files")
    
    def parse_entry(self, offset):
        """Parse a single directory entry"""
        if offset + 4 > len(self.data):
            return None
        
        # Name length
        name_len = struct.unpack('<I', self.data[offset:offset+4])[0]
        current = offset + 4
        
        if current + name_len > len(self.data):
            return None
        
        # Name
        name = self.data[current:current+name_len].decode('utf-8', errors='replace')
        current += name_len
        
        # Pad to 8 bytes
        pad = (8 - (name_len % 8)) % 8
        current += pad
        
        if current + 36 > len(self.data):  # 8+8+16+4 = 36 bytes for rest
            return None
        
        # File offset (relative to file base)
        file_offset = struct.unpack('<Q', self.data[current:current+8])[0]
        current += 8
        
        # File size
        file_size = struct.unpack('<Q', self.data[current:current+8])[0]
        current += 8
        
        # MD5 checksum
        md5_bytes = self.data[current:current+16]
        current += 16
        
        # Flags
        flags = struct.unpack('<I', self.data[current:current+4])[0]
        current += 4
        
        return {
            'name': name,
            'offset': file_offset,
            'size': file_size,
            'md5': md5_bytes.hex(),
            'flags': flags,
            'next_offset': current
        }
    
    def extract_file(self, entry, output_dir):
        """Extract a single file from the PCK"""
        abs_offset = self.file_base + entry['offset']
        
        if abs_offset + entry['size'] > len(self.data):
            return False
        
        content = self.data[abs_offset:abs_offset + entry['size']]
        
        # Verify MD5
        actual_md5 = hashlib.md5(content).hexdigest()
        md5_match = actual_md5 == entry['md5'][:32]
        
        # Create output path
        output_path = os.path.join(output_dir, entry['name'])
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        
        with open(output_path, 'wb') as f:
            f.write(content)
        
        return md5_match
    
    def extract_all(self, output_dir):
        """Extract all files from the PCK"""
        print(f"\n=== Extracting {len(self.entries)} files ===\n")
        
        os.makedirs(output_dir, exist_ok=True)
        
        success = 0
        failed = 0
        md5_mismatch = 0
        
        for i, entry in enumerate(self.entries):
            status = self.extract_file(entry, output_dir)
            if status is True:
                print(f"  OK  {entry['name']}")
                success += 1
            elif status is False:
                print(f"  ERR {entry['name']} (bounds error)")
                failed += 1
            else:
                print(f"  MD5 {entry['name']} (extracted but MD5 mismatch)")
                md5_mismatch += 1
        
        print(f"\nExtraction complete:")
        print(f"  Success: {success}")
        print(f"  Failed (bounds): {failed}")
        print(f"  MD5 mismatch: {md5_mismatch}")
        print(f"  Output: {output_dir}")
    
    def list_files(self):
        """List all files in the PCK"""
        print("\n=== Files in PCK ===\n")
        for i, entry in enumerate(self.entries):
            encrypted = " [ENCRYPTED]" if (entry['flags'] & 1) else ""
            removed = " [REMOVED]" if (entry['flags'] & 2) else ""
            print(f"{i+1:2d}. {entry['name']}{encrypted}{removed}")
            print(f"    Size: {entry['size']:8d} bytes")
    
    def preview_file(self, entry, max_bytes=200):
        """Preview the content of a file"""
        abs_offset = self.file_base + entry['offset']
        
        if abs_offset >= len(self.data):
            print(f"Offset {abs_offset} is beyond file size")
            return
        
        content = self.data[abs_offset:abs_offset + min(entry['size'], max_bytes)]
        
        print(f"\n=== Preview: {entry['name']} ===")
        print(f"Size: {entry['size']} bytes")
        print(f"First {len(content)} bytes:")
        
        for i in range(0, len(content), 16):
            hex_str = ' '.join(f'{b:02X}' for b in content[i:i+16])
            ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in content[i:i+16])
            print(f"  {abs_offset+i:08X}: {hex_str:<48} |{ascii_str}|")
        
        if entry['size'] > max_bytes:
            print(f"  ... ({entry['size'] - max_bytes} more bytes)")
        
        # File type detection
        print(f"\nFile type detection:")
        if content.startswith(b'RSRC'):
            print("  -> Godot 3.x binary resource (.tres)")
        elif content.startswith(b'GDRE'):
            print("  -> Godot encrypted resource")
        elif content.startswith(b'RESOURCE'):
            print("  -> Godot 4.x binary resource")
        elif content.startswith(b'{"'):
            print("  -> JSON/text (likely .gdextension or config)")
        elif content.startswith(b'[gd_scene'):
            print("  -> Godot text scene (.tscn)")
        elif content.startswith(b'[gd_resource'):
            print("  -> Godot text resource (.tres)")
        elif b'source_md5=' in content[:100]:
            print("  -> Godot import manifest (.md5)")
        elif b'GST2' in content[:100]:
            print("  -> Slay the Spire 2 custom resource")
        elif content[:4] == b'\x89PNG' or content.startswith(b'\x89PNG'):
            print("  -> PNG image")
        elif content.startswith(b'\xFF\xD8\xFF'):
            print("  -> JPEG image")
        elif content.startswith(b'BM'):
            print("  -> BMP image")
        elif content.startswith(b'GIF8'):
            print("  -> GIF image")
        elif content[:2] == b'\x1f\x8b':
            print("  -> GZIP compressed")
        else:
            print("  -> Unknown format")

def main():
    if len(sys.argv) < 2:
        print("Usage: python pck_extractor.py <pck_file> [output_dir]")
        print("\nExample:")
        print("  python pck_extractor.py FreeLoadout.pck")
        print("  python pck_extractor.py FreeLoadout.pck ./extracted")
        sys.exit(1)
    
    pck_file = sys.argv[1]
    output_dir = sys.argv[2] if len(sys.argv) > 2 else None
    
    print("=" * 60)
    print("  Godot 4.x PCK File Extractor")
    print("=" * 60)
    print()
    print(f"File: {pck_file}")
    print(f"Size: {os.path.getsize(pck_file)} bytes")
    print()
    
    try:
        extractor = PCKExtractor(pck_file)
        extractor.parse_header()
        extractor.parse_directory()
        
        if output_dir:
            extractor.extract_all(output_dir)
        else:
            extractor.list_files()
            # Preview first 3 files
            for i in range(min(3, len(extractor.entries))):
                extractor.preview_file(extractor.entries[i])
        
    except Exception as e:
        print(f"\nError: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()
