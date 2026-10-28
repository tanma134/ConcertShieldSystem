import glob, re

files = [
    r'd:\BEN9\3\ConcertShieldSystem\scratch\generate_all.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group1.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group2.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group3.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group4.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group5.py',
    r'd:\BEN9\3\ConcertShieldSystem\scratch\gen_group6.py',
]

for fpath in files:
    with open(fpath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    print(f'=== {fpath} ===')
    for idx, line in enumerate(lines):
        if any(keyword in line for keyword in ['SELECT', 'INSERT', 'UPDATE', 'DELETE']):
            print(f'  L{idx+1}: {line.strip()}')
