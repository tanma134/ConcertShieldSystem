import struct

def read_ole2_streams(filepath):
    with open(filepath, 'rb') as f:
        data = f.read()

    # OLE2 Magic: D0 CF 11 E0 A1 B1 1A E1
    if data[:8] != b'\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1':
        print("Not an OLE2 file")
        return

    sector_shift = struct.unpack('<H', data[30:32])[0]
    sector_size = 1 << sector_shift
    print("Sector size:", sector_size)

    dir_start_sector = struct.unpack('<I', data[48:52])[0]
    print("Directory start sector:", dir_start_sector)

    # DIFAT / FAT
    num_fat_sectors = struct.unpack('<I', data[44:48])[0]
    fat_sectors = []
    for i in range(109):
        sec = struct.unpack('<I', data[76 + i*4 : 80 + i*4])[0]
        if sec < 0xFFFFFFFC:
            fat_sectors.append(sec)

    fat = []
    for sec_idx in fat_sectors:
        offset = (sec_idx + 1) * sector_size
        entries = struct.unpack(f'<{sector_size // 4}I', data[offset : offset + sector_size])
        fat.extend(entries)

    # Read directory sectors
    dir_sec = dir_start_sector
    dir_data = bytearray()
    while dir_sec < 0xFFFFFFFC:
        offset = (dir_sec + 1) * sector_size
        dir_data.extend(data[offset : offset + sector_size])
        dir_sec = fat[dir_sec]

    print("Directory entries found:")
    num_entries = len(dir_data) // 128
    for i in range(num_entries):
        entry = dir_data[i*128 : (i+1)*128]
        name_len = struct.unpack('<H', entry[64:66])[0]
        name = entry[:name_len].decode('utf-16le', errors='ignore').rstrip('\x00')
        obj_type = entry[66]
        start_sec = struct.unpack('<I', entry[116:120])[0]
        size = struct.unpack('<Q', entry[120:128])[0]
        print(f"  [{i}] '{name}' (type={obj_type}, start_sec={start_sec}, size={size})")

read_ole2_streams('D:/BEN9/Report5_Unit Test Case.xls')
