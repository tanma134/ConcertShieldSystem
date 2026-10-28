import zipfile
import xml.etree.ElementTree as ET

with zipfile.ZipFile('Report5_Unit Test Case_ConcertShield.xlsx') as z:
    # Read shared strings
    sst = []
    if 'xl/sharedStrings.xml' in z.namelist():
        sst_xml = z.read('xl/sharedStrings.xml')
        root_sst = ET.fromstring(sst_xml)
        for si in root_sst.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}si'):
            t = si.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t')
            sst.append(t.text if t is not None else '')

    # Read sheet4.xml (Test Report)
    sh_xml = z.read('xl/worksheets/sheet4.xml')
    root = ET.fromstring(sh_xml)
    ns = {'main': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}

    rows = root.findall('.//main:row', ns)
    print(f"Total rows in Test Report: {len(rows)}")
    for row in rows:
        r_idx = int(row.attrib.get('r', 0))
        if r_idx in [11, 12, 13, 38, 39, 41, 42, 43, 44, 45]:
            cell_vals = []
            for c in row.findall('main:c', ns):
                v_el = c.find('main:v', ns)
                f_el = c.find('main:f', ns)
                t_attr = c.attrib.get('t')
                val = ""
                if f_el is not None:
                    val = f"={f_el.text}"
                elif v_el is not None:
                    if t_attr == 's':
                        idx = int(v_el.text)
                        val = sst[idx] if idx < len(sst) else v_el.text
                    else:
                        val = v_el.text
                cell_vals.append(f"{c.attrib.get('r')}='{val}'")
            print(f"Row {r_idx:02d}: " + " | ".join(cell_vals[:8]))
