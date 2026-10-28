import json

with open(r'D:\BEN9\3\ConcertShieldSystem\scratch\functions_data.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

fc = data[0]
print("Keys of fc:", list(fc.keys()))
print("num_tcs:", fc.get('num_tcs'))
print("types:", fc.get('types'))
print("len types:", len(fc.get('types', [])))
