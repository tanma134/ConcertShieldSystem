import xml.etree.ElementTree as ET

root = ET.parse('scratch/sample_styles.xml').getroot()
ns = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}

print("Fills:")
for i, f in enumerate(root.findall('.//s:fill', ns)):
    pf = f.find('s:patternFill', ns)
    pt = pf.attrib.get('patternType') if pf is not None else None
    fg = pf.find('s:fgColor', ns) if pf is not None else None
    rgb = fg.attrib.get('rgb') if fg is not None else (fg.attrib.get('indexed') if fg is not None else None)
    print(f"  [{i}] pattern={pt}, color={rgb}")

print("\nFonts:")
for i, f in enumerate(root.findall('.//s:fonts/s:font', ns)):
    name = f.find('s:name', ns).attrib.get('val') if f.find('s:name', ns) is not None else ''
    sz = f.find('s:sz', ns).attrib.get('val') if f.find('s:sz', ns) is not None else ''
    b = f.find('s:b', ns) is not None
    color = f.find('s:color', ns).attrib if f.find('s:color', ns) is not None else {}
    print(f"  [{i}] {name} {sz}pt, bold={b}, color={color}")
