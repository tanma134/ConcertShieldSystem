import re
import glob

for f in sorted(glob.glob('scratch/gen_class_group*.py')):
    content = open(f, encoding='utf-8').read()
    sections = content.split('def generate_')[1:]
    for sec in sections:
        name = sec.split('(')[0]
        ctrl = re.findall(r'add_class\(\"ctrl\",\s*\"([^\"]+)\"', sec)
        svc = re.findall(r'add_class\(\"svc\",\s*\"([^\"]+)\"', sec)
        methods = re.findall(r'\+([A-Za-z0-9_]+\([^\)]*\):[^\",\n]+)', sec)
        dtos = re.findall(r'add_class\(\"[^\"]*dto[^\"]*\",\s*\"([^\"]+)\"', sec)
        print(f"UC: {name}")
        print(f"  Ctrl: {ctrl[0] if ctrl else 'None'}")
        print(f"  Svc:  {svc[0] if svc else 'None'}")
        print(f"  Methods: {methods[:3]}")
        print(f"  DTOs: {dtos}")
        print()
