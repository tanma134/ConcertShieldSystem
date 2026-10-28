# -*- coding: utf-8 -*-
"""
Full test suite generator for 27 use cases.
Generates:
1. UnitTest_ConcertShield_Specification.xlsx (Complete Excel workbook)
2. UnitTest_ConcertShield_All.csv (Full CSV export in user format)
3. unit_tests_csv/*.csv (Individual CSV files for each function)
"""

import os
import csv
from simple_xlsx import SimpleWorkbook

def make_tc_sheet(fc):
    """
    Constructs the exact rows for a function test sheet according to user template.
    fc is a dictionary containing:
      code, name, created_by, executed_by, loc, lack_of_tc, test_req,
      num_tc, tc_types, tc_status, executed_date,
      preconditions: [ (text, [tc_indices]) ],
      inputs: [ (param_name, [ (val_text, [tc_indices]) ]) ],
      returns: [ (val_text, [tc_indices]) ],
      exceptions: [ (exc_text, [tc_indices]) ],
      logs: [ (log_text, [tc_indices]) ]
    """
    num_tc = fc['num_tc']
    tc_ids = [f"UTCID{i+1:02d}" for i in range(num_tc)]
    tc_types = fc['tc_types']
    tc_status = fc['tc_status']
    exec_date = fc.get('executed_date', '26/10/2026')
    
    passed_cnt = sum(1 for s in tc_status if s == 'P')
    failed_cnt = sum(1 for s in tc_status if s == 'F')
    untested_cnt = sum(1 for s in tc_status if s not in ('P', 'F'))
    n_cnt = sum(1 for t in tc_types if t == 'N')
    a_cnt = sum(1 for t in tc_types if t == 'A')
    b_cnt = sum(1 for t in tc_types if t == 'B')

    rows = []
    # Row 1
    r1 = ['Function Code', '', fc['code'], '', '', 'Function Name', '', '', '', '', '', fc['name']]
    rows.append(r1)
    # Row 2
    r2 = ['Created By', '', fc.get('created_by', 'Developer Name'), '', '', 'Executed By', '', '', '', '', '', fc.get('executed_by', 'QA Tester')]
    rows.append(r2)
    # Row 3
    r3 = ['Lines  of code', '', float(fc['loc']), '', '', 'Lack of test cases', '', '', '', '', '', float(fc.get('lack_of_tc', 0.0))]
    rows.append(r3)
    # Row 4
    r4 = ['Test requirement', '', fc['test_req']]
    rows.append(r4)
    # Row 5
    r5 = ['Passed', '', 'Failed', '', 'Untested', '', '', '', '', '', 'N/A/B', '', '', '', 'Total Test Cases']
    rows.append(r5)
    # Row 6
    r6 = [float(passed_cnt), '', float(failed_cnt), '', float(untested_cnt), '', '', '', '', '', f"{n_cnt},{a_cnt},{b_cnt}", float(n_cnt), float(a_cnt), float(b_cnt), float(num_tc)]
    rows.append(r6)
    # Row 7 (blank)
    rows.append([])
    # Row 8 (UTCIDs)
    r8 = ['', '', '', '', ''] + tc_ids
    rows.append(r8)
    
    # Condition, Precondition
    r9 = ['Condition', 'Precondition']
    rows.append(r9)
    for pre_text, applied_tcs in fc['preconditions']:
        r_pre = ['', '', '', pre_text, ''] + ['O' if i+1 in applied_tcs else '' for i in range(num_tc)]
        rows.append(r_pre)
        
    # Inputs
    for param_name, val_list in fc['inputs']:
        rows.append(['', param_name])
        for val_text, applied_tcs in val_list:
            r_val = ['', '', '', val_text, ''] + ['O' if i+1 in applied_tcs else '' for i in range(num_tc)]
            rows.append(r_val)
            
    # Confirm, Return
    rows.append(['Confirm', 'Return'])
    for ret_text, applied_tcs in fc['returns']:
        r_ret = ['', '', '', ret_text, ''] + ['O' if i+1 in applied_tcs else '' for i in range(num_tc)]
        rows.append(r_ret)
        
    # Exception
    rows.append(['', 'Exception'])
    for exc_text, applied_tcs in fc['exceptions']:
        r_exc = ['', '', '', exc_text, ''] + ['O' if i+1 in applied_tcs else '' for i in range(num_tc)]
        rows.append(r_exc)
        
    # Log message
    rows.append(['', 'Log message'])
    for log_text, applied_tcs in fc['logs']:
        r_log = ['', '', '', log_text, ''] + ['O' if i+1 in applied_tcs else '' for i in range(num_tc)]
        rows.append(r_log)
        
    # Result
    r_type = ['Result', 'Type(N : Normal, A : Abnormal, B : Boundary)', '', '', ''] + tc_types
    rows.append(r_type)
    r_pf = ['', 'Passed/Failed', '', '', ''] + tc_status
    rows.append(r_pf)
    r_date = ['', 'Executed Date', '', '', ''] + [exec_date] * num_tc
    rows.append(r_date)
    r_def = ['', 'Defect ID', '', '', ''] + fc.get('defect_ids', [''] * num_tc)
    rows.append(r_def)
    
    return rows, {
        'code': fc['code'],
        'name': fc['name'],
        'loc': fc['loc'],
        'passed': passed_cnt,
        'failed': failed_cnt,
        'untested': untested_cnt,
        'n': n_cnt,
        'a': a_cnt,
        'b': b_cnt,
        'total': num_tc
    }
