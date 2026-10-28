import json
import sys
import os

from def_group1 import GROUP1_FUNCTIONS
from def_group2 import GROUP2_FUNCTIONS
from def_group3 import GROUP3_FUNCTIONS
from def_group4 import GROUP4_FUNCTIONS
from def_group5 import GROUP5_FUNCTIONS
from def_group6 import GROUP6_FUNCTIONS

ALL_FUNCTIONS = (
    GROUP1_FUNCTIONS +
    GROUP2_FUNCTIONS +
    GROUP3_FUNCTIONS +
    GROUP4_FUNCTIONS +
    GROUP5_FUNCTIONS +
    GROUP6_FUNCTIONS
)

with open('scratch/functions_data.json', 'w', encoding='utf-8') as f:
    json.dump(ALL_FUNCTIONS, f, ensure_ascii=False, indent=2)

print(f"Exported {len(ALL_FUNCTIONS)} functions to scratch/functions_data.json")
