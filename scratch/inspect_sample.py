# -*- coding: utf-8 -*-
import subprocess

out = subprocess.check_output(['git', 'show', '047ee292ac62c085bdd17745e7dee3c53ec1647b:CheckoutSequence.xml']).decode('utf-8')
lines = out.splitlines()
for i, line in enumerate(lines):
    if any(k in line for k in ['id1', 'id2', 'id76', 'id77', 'id78', 'id79', 'id80', 'id81', 'id82', 'id83', 'id84', 'id85']):
        print(f"L{i}: {line}")
        for j in range(i, min(len(lines), i+8)):
            if '</mxCell>' in lines[j] or '/>' in lines[j]:
                print(lines[j])
                break
            else:
                print(lines[j])
        print("-" * 30)
