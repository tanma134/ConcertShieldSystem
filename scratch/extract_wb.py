import struct

def extract_workbook_stream(filepath, out_path):
    with open(filepath, 'rb') as f:
        data = f.read()

    sector_shift = struct.unpack('<H', data[30:32])[0]
    sector_size = 1 << sector_shift
    dir_start_sector = struct.unpack('<I', data[48:52])[0]

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

    num_entries = len(dir_data) // 128
    wb_entry = None
    for i in range(num_entries):
        entry = dir_data[i*128 : (i+1)*128]
        name_len = struct.unpack('<H', entry[64:66])[0]
        name = entry[:name_len].decode('utf-16le', errors='ignore').rstrip('\x00')
        if name == 'Workbook':
            wb_entry = entry
            break

    start_sec = struct.unpack('<I', wb_entry[116:120])[0]
    size = struct.unpack('<Q', wb_entry[120:128])[0]

    wb_data = bytearray()
    sec = start_sec
    while sec < 0xFFFFFFFC and len(wb_data) < size:
        offset = (sec + 1) * sector_size
        wb_data.extend(data[offset : offset + sector_size])
        sec = fat[sec]

    wb_data = wb_data[:size]
    with open(out_path, 'wb') as f:
        f.write(wb_data)
    print(f"Extracted Workbook stream to {out_path} ({len(wb_data)} bytes)")

extract_workbook_stream('D:/BEN9/Report5_Unit Test Case.xls', 'scratch/workbook.biff8')
