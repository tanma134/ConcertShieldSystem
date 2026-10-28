import struct
from dump_cells import parse_cells_for_sheet, sst, sheets

print("=== Analyzing FunctionList ===")
fl_offset = [pos for name, pos in sheets if 'FunctionList' in name][0]
cells, merges = parse_cells_for_sheet('scratch/workbook.biff8', sst, fl_offset)
print(f"Total cells: {len(cells)}, Total merges: {len(merges)}")

max_r = max(r for r, c in cells.keys()) if cells else 0
max_c = max(c for r, c in cells.keys()) if cells else 0
for r in range(min(20, max_r + 1)):
    row_vals = []
    for c in range(min(10, max_c + 1)):
        v = cells.get((r, c), "")
        if isinstance(v, str):
            v_str = repr(v) if len(v) < 20 else repr(v[:17] + "...")
        else:
            v_str = str(v)
        row_vals.append(v_str if v != "" else ".")
    print(f"R{r:02d}: " + " | ".join(row_vals))
