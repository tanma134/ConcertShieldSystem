import zipfile

with zipfile.ZipFile('D:/BEN9/Report5_Test Case Document.xlsx') as z:
    styles_xml = z.read('xl/styles.xml').decode('utf-8')

with open('scratch/sample_styles.xml', 'w', encoding='utf-8') as f:
    f.write(styles_xml)

print("styles.xml extracted, length:", len(styles_xml))
