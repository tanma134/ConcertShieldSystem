# -*- coding: utf-8 -*-
"""
Master Unit Test Builder:
Compiles all 27 use cases into:
1. UnitTest_ConcertShield_Specification.xlsx (Complete Excel workbook)
2. UnitTest_ConcertShield_All.csv (Concatenated CSV export matching user template)
3. unit_tests_csv/*.csv (Individual CSV files for each function)
"""

import os
import io
import csv
import sys
sys.path.append(os.path.dirname(__file__))

from simple_xlsx import SimpleWorkbook
from tc_sheet_builder import make_tc_sheet

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

def build_cover_sheet():
    return [
        ['', 'UNIT TEST CASE', '', '', ''],
        [],
        ['Project Name', 'ConcertShield System', '', '', 'Creator', 'Developer'],
        ['Project Code', 'CSS', '', '', 'Reviewer/Approver', 'Technical Lead / QA Lead'],
        ['Document Code', 'CSS_UnitTest_v1.0', '', '', 'Issue Date', '26/10/2026'],
        ['', '', '', '', 'Version', '1.0'],
        [],
        [],
        ['Record of change'],
        ['Effective Date', 'Version', 'Change Item', '*A,D,M', 'Change description', 'Reference'],
        ['26/10/2026', '1.0', 'All 27 Use Cases', 'A', 'Initial creation of complete Unit Test Case matrix for 27 functions', 'Architecture & SRS Specification']
    ]

def build_function_list_sheet(functions):
    rows = [
        ['', '', '', '', 'UNIT TEST CASE LIST'],
        [],
        ['Project Name', '', '', '', 'ConcertShield System'],
        ['Project Code', '', '', '', 'CSS'],
        ['Normal number of Test cases/KLOC ', '', '', '', 100.0],
        ['Test Environment Setup Description', '', '', '', '1. Runtime: .NET 8.0 SDK (C# 12)\n2. Test Frameworks: xUnit 2.5+, Moq 4.20+, FluentAssertions 6.12+\n3. Database: PostgreSQL 16 & EF Core InMemoryDatabase\n4. Integration: WebApplicationFactory, TestServer'],
        [],
        [],
        ['No', 'Requirement Name', 'Class Name', 'Function Name', 'Function Code(Optional)', 'Sheet Name', 'Description', 'Pre-Condition']
    ]
    for i, fc in enumerate(functions, 1):
        rows.append([
            float(i),
            fc['req_name'],
            fc['class_name'],
            fc['name'],
            fc['code'],
            fc['sheet_name'],
            fc['test_req'],
            fc['preconditions'][0][0] if fc['preconditions'] else ''
        ])
    return rows

def build_test_report_sheet(stats_list):
    rows = [
        ['UNIT TEST REPORT'],
        [],
        ['Project Name', 'ConcertShield System', '', 'Creator', 'Developer'],
        ['Project Code', 'CSS', '', 'Reviewer/Approver', 'Technical Lead / QA Lead'],
        ['Document Code', 'CSS_Test Report_v1.0', '', 'Issue Date', '26/10/2026'],
        ['Notes', 'Release includes 6 Modules: Payment & Voucher, Dynamic QR & Wallet, Governance & Policies, Check-in Monitoring, QR Scanner, Notification Management'],
        [],
        [],
        [],
        ['No', 'Function code', 'Passed', 'Failed', 'Untested', 'N', 'A', 'B', 'Total Test Cases']
    ]
    
    total_passed = 0
    total_failed = 0
    total_untested = 0
    total_n = 0
    total_a = 0
    total_b = 0
    total_all = 0

    for i, st in enumerate(stats_list, 1):
        total_passed += st['passed']
        total_failed += st['failed']
        total_untested += st['untested']
        total_n += st['n']
        total_a += st['a']
        total_b += st['b']
        total_all += st['total']
        rows.append([
            float(i),
            st['code'],
            float(st['passed']),
            float(st['failed']),
            float(st['untested']),
            float(st['n']),
            float(st['a']),
            float(st['b']),
            float(st['total'])
        ])

    rows.append([])
    rows.append(['', 'Sub total', float(total_passed), float(total_failed), float(total_untested), float(total_n), float(total_a), float(total_b), float(total_all)])
    rows.append([])
    
    cov = ((total_passed + total_failed) / total_all * 100.0) if total_all else 0.0
    succ_cov = (total_passed / total_all * 100.0) if total_all else 0.0
    n_pct = (total_n / total_all * 100.0) if total_all else 0.0
    a_pct = (total_a / total_all * 100.0) if total_all else 0.0
    b_pct = (total_b / total_all * 100.0) if total_all else 0.0

    rows.append(['', 'Test coverage', '', cov, '%'])
    rows.append(['', 'Test successful coverage', '', succ_cov, '%'])
    rows.append(['', 'Normal case', '', n_pct, '%'])
    rows.append(['', 'Abnormal case', '', a_pct, '%'])
    rows.append(['', 'Boundary case', '', b_pct, '%'])
    return rows

def build_guideline_sheet():
    return [
        ['Guideline to make and understand Unit Test Case'],
        [],
        ['1. Overview'],
        [' - In the template, Unit test cases are based on functions. Each sheet presents test cases for one function.'],
        [' - Cover: General information of the project and Unit Test cases'],
        [' - FunctionList: The list of Classes and Functions in the document.'],
        ['     + To control that the number of Unit TC meets customer requirement or the norm, user should fill value for Normal number of Test cases/KLOC.'],
        ['     + Click on Function link to open the related Test cases of the function.'],
        [' - Test Report: provide the overview results of Functions Unit test: Test coverage, Test successful coverage (Summary, for normal/abnormal/boundary cases)'],
        [],
        ['2. Content in Test function sheet'],
        ['2.1 Combination of test cases.'],
        [' - To verify that number of Unit TC meets customer requirement or not. User has to fill number LOC of tested function and fill value of Normal number test cases/KLOC item in FunctionList sheet.'],
        [' 2.2 Condition and confirmation of Test cases.'],
        [' Each test case is the combination of condition and confirmation.'],
        ['a. Condition:'],
        ['        - Condition is combination of precondition and values of inputs.'],
        ['        - Precondition: it is setting condition that must exist before execution of the test case.'],
        ['        - Values of inputs: it includes 3 types of values: normal, boundary and abnormal.'],
        ['b. Confirmation:'],
        ['        - Output result of the function, exceptions, or log messages.'],
        ['c. Type of test cases and result:'],
        ['        - Type of test case: Normal (N), Boundary (B), Abnormal (A).'],
        ['        - Test case result: P for Passed and F for Failed cases.']
    ]

def rows_to_csv_string(rows):
    out = io.StringIO()
    writer = csv.writer(out, quoting=csv.QUOTE_MINIMAL)
    for r in rows:
        writer.writerow(r)
    return out.getvalue()

def main():
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    csv_dir = os.path.join(base_dir, 'unit_tests_csv')
    os.makedirs(csv_dir, exist_ok=True)

    wb = SimpleWorkbook()

    # 1. Cover Sheet
    cover_rows = build_cover_sheet()
    wb.add_sheet('Cover', cover_rows)

    # 2. Function List Sheet
    fn_list_rows = build_function_list_sheet(ALL_FUNCTIONS)
    wb.add_sheet('FunctionList', fn_list_rows)

    # Build individual function sheets
    function_sheets_rows = []
    stats_list = []
    for fc in ALL_FUNCTIONS:
        sheet_rows, stats = make_tc_sheet(fc)
        wb.add_sheet(fc['sheet_name'], sheet_rows)
        function_sheets_rows.append((fc, sheet_rows))
        stats_list.append(stats)
        
        # Save individual CSV
        csv_filename = f"{fc['stt']:03d}_{fc['sheet_name']}.csv"
        csv_path = os.path.join(csv_dir, csv_filename)
        with open(csv_path, 'w', encoding='utf-8', newline='') as f:
            f.write(rows_to_csv_string(sheet_rows))

    # 3. Test Report Sheet
    report_rows = build_test_report_sheet(stats_list)
    wb.add_sheet('Test Report', report_rows)

    # 4. Guideline Sheet
    guide_rows = build_guideline_sheet()
    wb.add_sheet('Guideline', guide_rows)

    # Save Excel workbook
    xlsx_path = os.path.join(base_dir, 'UnitTest_ConcertShield_Specification.xlsx')
    wb.save(xlsx_path)

    # Save concatenated CSV matching user template
    all_csv_path = os.path.join(base_dir, 'UnitTest_ConcertShield_All.csv')
    with open(all_csv_path, 'w', encoding='utf-8', newline='') as f:
        # First write Cover
        f.write(rows_to_csv_string(cover_rows))
        f.write('\n' + '='*40 + '\n')
        # Then FunctionList
        f.write(rows_to_csv_string(fn_list_rows))
        f.write('\n' + '='*40 + '\n')
        # Then Test Report
        f.write(rows_to_csv_string(report_rows))
        f.write('\n' + '='*40 + '\n')
        # Then all function sheets
        for fc, s_rows in function_sheets_rows:
            f.write(rows_to_csv_string(s_rows))
            f.write('\n' + '-'*30 + '\n')
        # Then Guideline
        f.write(rows_to_csv_string(guide_rows))

    print(f"Master CSV saved: {all_csv_path} ({os.path.getsize(all_csv_path)} bytes)")
    print(f"Individual CSVs saved to {csv_dir} ({len(ALL_FUNCTIONS)} files)")
    print(f"Workbook saved: {xlsx_path} ({os.path.getsize(xlsx_path)} bytes)")

if __name__ == '__main__':
    main()
