import json

with open(r'D:\BEN9\3\ConcertShieldSystem\scratch\functions_data.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

print(f"Total functions: {len(data)}")
for i, fc in enumerate(data):
    num_pre = len(fc.get('preconditions', []))
    inputs = fc.get('inputs', [])
    num_inp_params = len(inputs)
    num_inp_vals = sum(len(inp[1]) for inp in inputs)
    total_cond = num_pre + num_inp_params + num_inp_vals
    num_ret = len(fc.get('returns', []))
    num_exc = len(fc.get('exceptions', []))
    num_tc = len(fc.get('test_cases', []))
    print(f"[{i+1:02d}] {fc['sheet_name']}: Cond={total_cond} (Pre={num_pre}, InpP={num_inp_params}, InpV={num_inp_vals}), Ret={num_ret}, Exc={num_exc}, TCs={num_tc}")
