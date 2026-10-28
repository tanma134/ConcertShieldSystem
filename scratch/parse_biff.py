import struct

def parse_biff_from_file(filename):
    with open(filename, 'rb') as f:
        data = f.read()

    idx = 0
    worksheets = []
    while True:
        idx = data.find(b'\x85\x00', idx)
        if idx == -1:
            break
        if idx + 10 < len(data):
            rec_len = struct.unpack('<H', data[idx+2:idx+4])[0]
            if 6 <= rec_len <= 64:
                pos, vis, sheet_type, name_len = struct.unpack('<IBBB', data[idx+4:idx+11])
                name_flag = data[idx+11]
                if name_flag == 0 and idx + 12 + name_len <= len(data):
                    sheet_name = data[idx+12:idx+12+name_len].decode('latin-1', errors='ignore')
                    worksheets.append(sheet_name)
                elif name_flag == 1 and idx + 12 + name_len * 2 <= len(data):
                    sheet_name = data[idx+12:idx+12+name_len*2].decode('utf-16le', errors='ignore')
                    worksheets.append(sheet_name)
        idx += 2
    for s in worksheets:
        print(" Sheet:", repr(s))

parse_biff_from_file('D:/BEN9/Report5_Unit Test Case.xls')
