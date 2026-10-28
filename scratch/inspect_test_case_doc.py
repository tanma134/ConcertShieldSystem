import zipfile
import xml.etree.ElementTree as ET

with zipfile.ZipFile('D:/BEN9/Report5_Test Case Document.xlsx') as z:
    wb_xml = z.read('xl/workbook.xml')
    root = ET.fromstring(wb_xml)
    ns = {'main': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
    sheets = root.findall('.//main:sheet', ns)
    print("Sheets in Report5_Test Case Document.xlsx:")
    for s in sheets:
        print("  Sheet:", s.attrib.get('name'), "sheetId:", s.attrib.get('sheetId'))
