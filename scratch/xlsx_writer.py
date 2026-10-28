import io
import zipfile
import xml.sax.saxutils

def col_idx_to_name(col_idx):
    """0-indexed column index to Excel column name (0 -> A, 27 -> AB)."""
    name = ""
    col_idx += 1
    while col_idx > 0:
        col_idx, rem = divmod(col_idx - 1, 26)
        name = chr(65 + rem) + name
    return name

class SimpleWorkbook:
    def __init__(self):
        self.sheets = []  # list of (sheet_name, rows_data)

    def add_sheet(self, name, rows_data):
        """rows_data is a list of lists of values (str, int, float, or None)."""
        # Excel sheet names max 31 chars, no invalid chars : \ / ? * [ ]
        safe_name = re_safe_name(name)[:31]
        self.sheets.append((safe_name, rows_data))

    def save(self, filepath):
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, 'w', zipfile.ZIP_DEFLATED) as z:
            # 1. [Content_Types].xml
            ct_lines = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">',
                        '  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>',
                        '  <Default Extension="xml" ContentType="application/xml"/>',
                        '  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>']
            for i in range(1, len(self.sheets) + 1):
                ct_lines.append(f'  <Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>')
            ct_lines.append('</Types>')
            z.writestr('[Content_Types].xml', '\n'.join(ct_lines))

            # 2. _rels/.rels
            z.writestr('_rels/.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>''')

            # 3. xl/workbook.xml
            wb_lines = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                        '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">',
                        '  <sheets>']
            for i, (name, _) in enumerate(self.sheets, 1):
                esc_name = xml.sax.saxutils.escape(name)
                wb_lines.append(f'    <sheet name="{esc_name}" sheetId="{i}" r:id="rId{i}"/>')
            wb_lines.append('  </sheets>')
            wb_lines.append('</workbook>')
            z.writestr('xl/workbook.xml', '\n'.join(wb_lines))

            # 4. xl/_rels/workbook.xml.rels
            wb_rels = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                       '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">']
            for i in range(1, len(self.sheets) + 1):
                wb_rels.append(f'  <Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>')
            wb_rels.append('</Relationships>')
            z.writestr('xl/_rels/workbook.xml.rels', '\n'.join(wb_rels))

            # 5. Sheets
            for i, (name, rows_data) in enumerate(self.sheets, 1):
                sheet_xml = self._build_sheet_xml(rows_data)
                z.writestr(f'xl/worksheets/sheet{i}.xml', sheet_xml)

        with open(filepath, 'wb') as f:
            f.write(buf.getvalue())
        print(f"Saved {filepath} with {len(self.sheets)} sheets, size: {len(buf.getvalue())} bytes")

    def _build_sheet_xml(self, rows_data):
        lines = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                 '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">',
                 '  <sheetData>']
        for r_idx, row in enumerate(rows_data, 1):
            if not row:
                continue
            # Check if entire row is empty
            if all(v is None or v == '' for v in row):
                continue
            c_cells = []
            for c_idx, val in enumerate(row):
                if val is None or val == '':
                    continue
                cell_ref = f"{col_idx_to_name(c_idx)}{r_idx}"
                if isinstance(val, (int, float)):
                    c_cells.append(f'<c r="{cell_ref}"><v>{val}</v></c>')
                elif isinstance(val, str) and val.startswith('='):
                    # Formula
                    f_esc = xml.sax.saxutils.escape(val[1:])
                    c_cells.append(f'<c r="{cell_ref}"><f>{f_esc}</f></c>')
                else:
                    t_esc = xml.sax.saxutils.escape(str(val))
                    c_cells.append(f'<c r="{cell_ref}" t="inlineStr"><is><t>{t_esc}</t></is></c>')
            if c_cells:
                lines.append(f'    <row r="{r_idx}">{"".join(c_cells)}</row>')
        lines.append('  </sheetData>')
        lines.append('</worksheet>')
        return '\n'.join(lines)

def re_safe_name(name):
    for ch in [':', '\\', '/', '?', '*', '[', ']']:
        name = name.replace(ch, '_')
    return name

if __name__ == '__main__':
    wb = SimpleWorkbook()
    wb.add_sheet("Cover", [["Project Name", "ConcertShield"], ["Date", "2026-10-26"]])
    wb.add_sheet("Report", [["No", "Function", "Total"], [1, "TestFunc", 15], ["Total", "", "=SUM(C2:C2)"]])
    wb.save("scratch/test_multi.xlsx")
