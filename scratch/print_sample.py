import json

with open(r'D:\BEN9\3\ConcertShieldSystem\scratch\functions_data.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

fc = data[0]
for k, v in fc.items():
    if isinstance(v, list):
        print(f"{k}: list of length {len(v)}")
    else:
        print(f"{k}: {repr(v)}")
