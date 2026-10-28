import struct

def parse_cells_for_sheet(filepath, sst, target_offset):
    with open(filepath, 'rb') as f:
        data = f.read()

    pos = target_offset
    cells = {}
    merges = []
    col_widths = {}
    row_heights = {}

    while pos + 4 <= len(data):
        rec_id, rec_len = struct.unpack('<HH', data[pos:pos+4])
        pos += 4
        rec_data = data[pos:pos+rec_len]
        pos += rec_len

        # EOF of sheet
        if rec_id == 0x000A:
            break

        # LABELSST (string cell)
        elif rec_id == 0x00FD:
            r, c, xf_idx, sst_idx = struct.unpack('<HHHI', rec_data[:10])
            val = sst[sst_idx] if sst_idx < len(sst) else f"SST_{sst_idx}"
            cells[(r, c)] = val

        # LABEL (inline string)
        elif rec_id == 0x0204:
            r, c, xf_idx, str_len = struct.unpack('<HHHH', rec_data[:8])
            flag = rec_data[8]
            val = rec_data[9:9+str_len].decode('latin-1', errors='ignore') if flag == 0 else rec_data[9:9+str_len*2].decode('utf-16le', errors='ignore')
            cells[(r, c)] = val

        # NUMBER (float)
        elif rec_id == 0x0203:
            r, c, xf_idx = struct.unpack('<HHH', rec_data[:6])
            val = struct.unpack('<d', rec_data[6:14])[0]
            cells[(r, c)] = val

        # RK (encoded number)
        elif rec_id == 0x027E:
            r, c, xf_idx, rk = struct.unpack('<HHHI', rec_data[:10])
            if rk & 0x02:
                val = rk >> 2
            else:
                val = struct.unpack('<d', struct.pack('<II', 0, rk & ~3))[0]
            if rk & 0x01:
                val /= 100.0
            cells[(r, c)] = val

        # FORMULA
        elif rec_id == 0x0006:
            r, c, xf_idx = struct.unpack('<HHH', rec_data[:6])
            # val can be float or string result
            val = "<formula>"
            cells[(r, c)] = val

        # MERGEDCELLS
        elif rec_id == 0x00E5:
            num_ranges = struct.unpack('<H', rec_data[:2])[0]
            for i in range(num_ranges):
                r1, r2, c1, c2 = struct.unpack('<HHHH', rec_data[2+i*8 : 10+i*8])
                merges.append((r1, r2, c1, c2))

    return cells, merges

# Parse SST first
with open('scratch/workbook.biff8', 'rb') as f:
    data = f.read()

pos = 0
sst = []
sheets = []
while pos + 4 <= len(data):
    rec_id, rec_len = struct.unpack('<HH', data[pos:pos+4])
    pos += 4
    rec_data = data[pos:pos+rec_len]
    pos += rec_len
    if rec_id == 0x0085:
        stream_pos = struct.unpack('<I', rec_data[:4])[0]
        vis = rec_data[4]
        sheet_type = rec_data[5]
        name_len = rec_data[6]
        flag = rec_data[7]
        name = rec_data[8:8+name_len].decode('latin-1', errors='ignore') if flag == 0 else rec_data[8:8+name_len*2].decode('utf-16le', errors='ignore')
        sheets.append((name, stream_pos))
    elif rec_id == 0x00FC:
        total_strings, unique_strings = struct.unpack('<II', rec_data[:8])
        sst_pos = 8
        while sst_pos < len(rec_data) and len(sst) < unique_strings:
            if sst_pos + 3 > len(rec_data): break
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
            s = raw_bytes.decode('utf-16le', errors='ignore') if is_16bit else raw_bytes.decode('latin-1', errors='ignore')
            sst_pos += run_count * 4
            sst_pos += asian_len
            sst.append(s)

print("=== Analyzing Function1 ===")
f1_offset = [pos for name, pos in sheets if 'Function1' in name][0]
cells, merges = parse_cells_for_sheet('scratch/workbook.biff8', sst, f1_offset)
print(f"Total cells: {len(cells)}, Total merges: {len(merges)}")
print("Merges:")
for m in merges[:15]:
    print(f"  Rows {m[0]}-{m[1]}, Cols {m[2]}-{m[3]}")

# Dump first 25 rows
max_r = max(r for r, c in cells.keys()) if cells else 0
max_c = max(c for r, c in cells.keys()) if cells else 0
print(f"Grid range: 0..{max_r} rows, 0..{max_c} cols")
for r in range(min(30, max_r + 1)):
    row_vals = []
    for c in range(min(12, max_c + 1)):
        v = cells.get((r, c), "")
        if isinstance(v, str):
            v_str = repr(v) if len(v) < 15 else repr(v[:12] + "...")
        else:
            v_str = str(v)
        row_vals.append(v_str if v != "" else ".")
    print(f"R{r:02d}: " + " | ".join(row_vals))
