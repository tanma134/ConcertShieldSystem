import io
import zipfile
import xml.sax.saxutils

def col_idx_to_name(col_idx):
    name = ""
    col_idx += 1
    while col_idx > 0:
        col_idx, rem = divmod(col_idx - 1, 26)
        name = chr(65 + rem) + name
    return name

def escape_xml(s):
    return xml.sax.saxutils.escape(str(s))

STYLES_XML = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="8">
    <!-- 0: Normal 10pt -->
    <font><sz val="10"/><color rgb="FF000000"/><name val="Arial"/></font>
    <!-- 1: Bold 10pt -->
    <font><b/><sz val="10"/><color rgb="FF000000"/><name val="Arial"/></font>
    <!-- 2: White Bold 10pt -->
    <font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Arial"/></font>
    <!-- 3: Title 16pt Bold -->
    <font><b/><sz val="16"/><color rgb="FF000000"/><name val="Arial"/></font>
    <!-- 4: Red Bold 10pt -->
    <font><b/><sz val="10"/><color rgb="FFFF0000"/><name val="Arial"/></font>
    <!-- 5: Hyperlink Blue Underline 10pt -->
    <font><u/><sz val="10"/><color rgb="FF0000FF"/><name val="Arial"/></font>
    <!-- 6: Italic 10pt -->
    <font><i/><sz val="10"/><color rgb="FF000000"/><name val="Arial"/></font>
    <!-- 7: Bold 12pt -->
    <font><b/><sz val="12"/><color rgb="FF000000"/><name val="Arial"/></font>
  </fonts>
  <fills count="6">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <!-- 2: Dark Blue #002060 -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF002060"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 3: Light Gray #F2F2F2 -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFF2F2F2"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 4: Pale Yellow #FFF2CC -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 5: Navy Blue #000080 -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF000080"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="3">
    <!-- 0: None -->
    <border><left/><right/><top/><bottom/></border>
    <!-- 1: Thin black all around -->
    <border>
      <left style="thin"><color rgb="FF000000"/></left>
      <right style="thin"><color rgb="FF000000"/></right>
      <top style="thin"><color rgb="FF000000"/></top>
      <bottom style="thin"><color rgb="FF000000"/></bottom>
    </border>
    <!-- 2: Dotted vertical, thin horizontal -->
    <border>
      <left style="dotted"><color rgb="FF000000"/></left>
      <right style="dotted"><color rgb="FF000000"/></right>
      <top style="thin"><color rgb="FF000000"/></top>
      <bottom style="thin"><color rgb="FF000000"/></bottom>
    </border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
  </cellStyleXfs>
  <cellXfs count="18">
    <!-- 0: Default Normal -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <!-- 1: Normal with border -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="left"/>
    </xf>
    <!-- 2: Center with border (for O marks, P/F, etc) -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
    <!-- 3: Bold with border -->
    <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="left"/>
    </xf>
    <!-- 4: Bold center with border -->
    <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
    <!-- 5: Header Dark Blue (White bold text, center) -->
    <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center" wrapText="1"/>
    </xf>
    <!-- 6: Header Dark Blue Rotated 90 deg (White text, center) -->
    <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center" textRotation="90"/>
    </xf>
    <!-- 7: Header Dark Blue Rotated 90 deg RED (Red bold text, center) -->
    <xf numFmtId="0" fontId="4" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center" textRotation="90"/>
    </xf>
    <!-- 8: Sidebar Dark Blue Rotated 90 deg (White bold text, center) -->
    <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center" textRotation="90"/>
    </xf>
    <!-- 9: Hyperlink with border (Blue underline, left) -->
    <xf numFmtId="0" fontId="5" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="left"/>
    </xf>
    <!-- 10: Hyperlink Center with border -->
    <xf numFmtId="0" fontId="5" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
    <!-- 11: Title 16pt Bold Center -->
    <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
    <!-- 12: Subtotal row (Dark Blue, White bold text, center) -->
    <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
    <!-- 13: Percent bold right -->
    <xf numFmtId="10" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1" applyAlignment="1">
      <alignment vertical="center" horizontal="right"/>
    </xf>
    <!-- 14: Defect ID vertical (Center, textRotation=90) -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="center" textRotation="90"/>
    </xf>
    <!-- 15: Info box label (Bold, light gray, thin border) -->
    <xf numFmtId="0" fontId="1" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="left"/>
    </xf>
    <!-- 16: Italic text with border -->
    <xf numFmtId="0" fontId="6" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
      <alignment vertical="center" horizontal="left"/>
    </xf>
    <!-- 17: Normal center no border -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1">
      <alignment vertical="center" horizontal="center"/>
    </xf>
  </cellXfs>
</styleSheet>"""

class StyledSheet:
    def __init__(self, name):
        self.name = name
        self.cells = {}        # (row_1idx, col_1idx) -> {'val': ..., 'style': int, 'type': 'str'|'num'|'formula'}
        self.merges = []       # list of range strings e.g. "A10:A29"
        self.col_widths = {}   # col_1idx -> width (float)
        self.row_heights = {}  # row_1idx -> height (float)
        self.hyperlinks = []   # list of (cell_ref, location, display)

    def set_cell(self, row, col, val, style=0, is_formula=False):
        """row and col are 1-indexed."""
        if val is None or val == '':
            return
        cell_type = 'formula' if is_formula or (isinstance(val, str) and val.startswith('=')) else ('num' if isinstance(val, (int, float)) else 'str')
        self.cells[(row, col)] = {
            'val': val,
            'style': style,
            'type': cell_type
        }

    def merge_cells(self, start_row, start_col, end_row, end_col):
        ref = f"{col_idx_to_name(start_col-1)}{start_row}:{col_idx_to_name(end_col-1)}{end_row}"
        self.merges.append(ref)

    def add_hyperlink(self, row, col, target_sheet, display_text):
        cell_ref = f"{col_idx_to_name(col-1)}{row}"
        loc = f"'{target_sheet}'!A1"
        self.hyperlinks.append((cell_ref, loc, display_text))

class StyledWorkbook:
    def __init__(self):
        self.sheets = []

    def add_sheet(self, sheet):
        self.sheets.append(sheet)

    def save(self, filepath):
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, 'w', zipfile.ZIP_DEFLATED) as z:
            # 1. [Content_Types].xml
            ct = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                  '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">',
                  '  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>',
                  '  <Default Extension="xml" ContentType="application/xml"/>',
                  '  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>',
                  '  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>']
            for i in range(1, len(self.sheets) + 1):
                ct.append(f'  <Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>')
            ct.append('</Types>')
            z.writestr('[Content_Types].xml', '\n'.join(ct))

            # 2. _rels/.rels
            z.writestr('_rels/.rels', '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>''')

            # 3. xl/styles.xml
            z.writestr('xl/styles.xml', STYLES_XML)

            # 4. xl/workbook.xml
            wb_lines = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                        '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">',
                        '  <sheets>']
            for i, sheet in enumerate(self.sheets, 1):
                safe_name = escape_xml(sheet.name[:31])
                wb_lines.append(f'    <sheet name="{safe_name}" sheetId="{i}" r:id="rId{i}"/>')
            wb_lines.append('  </sheets>')
            wb_lines.append('</workbook>')
            z.writestr('xl/workbook.xml', '\n'.join(wb_lines))

            # 5. xl/_rels/workbook.xml.rels
            wb_rels = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                       '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">']
            for i in range(1, len(self.sheets) + 1):
                wb_rels.append(f'  <Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>')
            wb_rels.append(f'  <Relationship Id="rId{len(self.sheets)+1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>')
            wb_rels.append('</Relationships>')
            z.writestr('xl/_rels/workbook.xml.rels', '\n'.join(wb_rels))

            # 6. Worksheets
            for i, sheet in enumerate(self.sheets, 1):
                z.writestr(f'xl/worksheets/sheet{i}.xml', self._build_sheet_xml(sheet))

        with open(filepath, 'wb') as f:
            f.write(buf.getvalue())
        print(f"Saved styled workbook to {filepath} ({len(self.sheets)} sheets, {len(buf.getvalue())} bytes)")

    def _build_sheet_xml(self, sheet):
        lines = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
                 '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">']
        
        # Column widths
        if sheet.col_widths:
            lines.append('  <cols>')
            for c_idx in sorted(sheet.col_widths.keys()):
                w = sheet.col_widths[c_idx]
                lines.append(f'    <col min="{c_idx}" max="{c_idx}" width="{w}" customWidth="1"/>')
            lines.append('  </cols>')

        # Sheet Data
        lines.append('  <sheetData>')
        max_row = max((r for r, c in sheet.cells.keys()), default=0)
        for r_idx in range(1, max_row + 1):
            row_cells = []
            for (r, c), c_data in sorted(sheet.cells.items()):
                if r != r_idx:
                    continue
                cell_ref = f"{col_idx_to_name(c-1)}{r}"
                st_attr = f' s="{c_data["style"]}"' if c_data['style'] > 0 else ''
                c_type = c_data['type']
                val = c_data['val']

                if c_type == 'formula':
                    f_text = escape_xml(val[1:] if str(val).startswith('=') else str(val))
                    row_cells.append(f'<c r="{cell_ref}"{st_attr}><f>{f_text}</f></c>')
                elif c_type == 'num':
                    row_cells.append(f'<c r="{cell_ref}"{st_attr}><v>{val}</v></c>')
                else:
                    t_text = escape_xml(str(val))
                    row_cells.append(f'<c r="{cell_ref}"{st_attr} t="inlineStr"><is><t>{t_text}</t></is></c>')

            ht_attr = f' ht="{sheet.row_heights[r_idx]}" customHeight="1"' if r_idx in sheet.row_heights else ''
            if row_cells:
                lines.append(f'    <row r="{r_idx}"{ht_attr}>{"".join(row_cells)}</row>')
            elif ht_attr:
                lines.append(f'    <row r="{r_idx}"{ht_attr}/>')
        lines.append('  </sheetData>')

        # Merged Cells
        if sheet.merges:
            lines.append(f'  <mergeCells count="{len(sheet.merges)}">')
            for m in sheet.merges:
                lines.append(f'    <mergeCell ref="{m}"/>')
            lines.append('  </mergeCells>')

        # Hyperlinks
        if sheet.hyperlinks:
            lines.append(f'  <hyperlinks>')
            for cell_ref, loc, display in sheet.hyperlinks:
                lines.append(f'    <hyperlink ref="{cell_ref}" location="{escape_xml(loc)}" display="{escape_xml(display)}"/>')
            lines.append('  </hyperlinks>')

        lines.append('</worksheet>')
        return '\n'.join(lines)
