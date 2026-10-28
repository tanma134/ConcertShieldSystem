import struct

def parse_biff_stream(filepath):
    with open(filepath, 'rb') as f:
        data = f.read()

    pos = 0
    records = []
    sheets = []
    sst = []
    formats = {}
    xf = []
    fonts = []

    while pos + 4 <= len(data):
        rec_id, rec_len = struct.unpack('<HH', data[pos:pos+4])
        pos += 4
        rec_data = data[pos:pos+rec_len]
        pos += rec_len

        # BOUNDSHEET
        if rec_id == 0x0085:
            stream_pos = struct.unpack('<I', rec_data[:4])[0]
            vis = rec_data[4]
            sheet_type = rec_data[5]
            name_len = rec_data[6]
            flag = rec_data[7]
            if flag == 0:
                name = rec_data[8:8+name_len].decode('latin-1', errors='ignore')
            else:
                name = rec_data[8:8+name_len*2].decode('utf-16le', errors='ignore')
            sheets.append((name, stream_pos))

        # SST (Shared String Table)
        elif rec_id == 0x00FC:
            total_strings, unique_strings = struct.unpack('<II', rec_data[:8])
            sst_pos = 8
            while sst_pos < len(rec_data) and len(sst) < unique_strings:
                if sst_pos + 3 > len(rec_data):
                    break
                chars_count = struct.unpack('<H', rec_data[sst_pos:sst_pos+2])[0]
                flags = rec_data[sst_pos+2]
                sst_pos += 3
                is_16bit = (flags & 0x01) != 0
                has_asian = (flags & 0x04) != 0
                has_formatting = (flags & 0x08) != 0
                run_count = 0
                if has_formatting:
                    if sst_pos + 2 > len(rec_data): break
                    run_count = struct.unpack('<H', rec_data[sst_pos:sst_pos+2])[0]
                    sst_pos += 2
                asian_len = 0
                if has_asian:
                    if sst_pos + 4 > len(rec_data): break
                    asian_len = struct.unpack('<I', rec_data[sst_pos:sst_pos+4])[0]
                    sst_pos += 4
                byte_len = chars_count * 2 if is_16bit else chars_count
                if sst_pos + byte_len > len(rec_data):
                    byte_len = len(rec_data) - sst_pos
                raw_bytes = rec_data[sst_pos:sst_pos+byte_len]
                sst_pos += byte_len
                if is_16bit:
                    s = raw_bytes.decode('utf-16le', errors='ignore')
                else:
                    s = raw_bytes.decode('latin-1', errors='ignore')
                sst_pos += run_count * 4
                sst_pos += asian_len
                sst.append(s)

    print("Parsed Sheets:")
    for s_name, s_pos in sheets:
        safe_name = s_name.encode('ascii', 'backslashreplace').decode('ascii')
        print(f"  - {safe_name} at offset {s_pos}")
    print(f"Total Shared Strings: {len(sst)}")
    for i in range(min(40, len(sst))):
        print(f"  [{i}] {repr(sst[i])}")

parse_biff_stream('scratch/workbook.biff8')
