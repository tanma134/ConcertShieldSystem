# -*- coding: utf-8 -*-
"""
Styled Excel Generator for ConcertShield Unit Test Specification.
Produces an exact match to the sample Excel template (Report5_Unit Test Case.xls / screenshots):
- Dark Blue (#002060) headers with white bold text
- Dark Blue vertical sidebars (#002060) for Condition, Confirm, Result
- Rotated 90 deg test case headers (UTCID01..UTCID10)
- Thin borders around all matrix cells
- Centered 'O' marks
- Hyperlinks between sheets in FunctionList and Test Report
- Subtotal row with formulas and dark blue fill
- Statistical metrics
"""

import os
import sys
sys.path.append(os.path.dirname(__file__))

from styled_xlsx_builder import StyledWorkbook, StyledSheet
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

def create_cover_sheet():
    sh = StyledSheet("Cover")
    sh.col_widths = {1: 4.0, 2: 18.0, 3: 25.0, 4: 10.0, 5: 18.0, 6: 25.0}

    # Title
    sh.row_heights[2] = 30.0
    sh.set_cell(2, 2, "UNIT TEST CASE", style=11)
    sh.merge_cells(2, 2, 2, 6)

    # Info Box
    for r in range(4, 8):
        sh.row_heights[r] = 22.0
    sh.set_cell(4, 2, "Project Name", style=15)
    sh.set_cell(4, 3, "ConcertShield System", style=1)
    sh.set_cell(4, 5, "Creator", style=15)
    sh.set_cell(4, 6, "Developer", style=1)

    sh.set_cell(5, 2, "Project Code", style=15)
    sh.set_cell(5, 3, "CSS", style=1)
    sh.set_cell(5, 5, "Reviewer/Approver", style=15)
    sh.set_cell(5, 6, "Technical Lead / QA Lead", style=1)

    sh.set_cell(6, 2, "Document Code", style=15)
    sh.set_cell(6, 3, "CSS_UnitTest_v1.0", style=1)
    sh.set_cell(6, 5, "Issue Date", style=15)
    sh.set_cell(6, 6, "28/10/2026", style=1)

    sh.set_cell(7, 5, "Version", style=15)
    sh.set_cell(7, 6, "1.0", style=1)

    # Record of change
    sh.set_cell(10, 2, "Record of change", style=3)
    sh.row_heights[11] = 24.0
    headers = ["Effective Date", "Version", "Change Item", "*A,D,M", "Change description", "Reference"]
    for c_idx, h in enumerate(headers, 1):
        sh.set_cell(11, c_idx, h, style=5)

    sh.row_heights[12] = 22.0
    row_vals = ["28/10/2026", "1.0", "All 27 Use Cases", "A", "Initial creation of Unit Test Case matrix for 27 functions", "SRS & Architecture Specification"]
    for c_idx, val in enumerate(row_vals, 1):
        sh.set_cell(12, c_idx, val, style=1)

    return sh

def create_function_list_sheet(functions):
    sh = StyledSheet("FunctionList")
    sh.col_widths = {
        1: 6.0,   # No
        2: 24.0,  # Requirement Name
        3: 28.0,  # Class Name
        4: 25.0,  # Function Name
        5: 25.0,  # Function Code
        6: 22.0,  # Sheet Name
        7: 35.0,  # Description
        8: 30.0   # Pre-Condition
    }

    # Title
    sh.row_heights[2] = 30.0
    sh.set_cell(2, 2, "UNIT TEST CASE LIST", style=11)
    sh.merge_cells(2, 2, 2, 7)

    # Info Box
    for r in range(4, 8):
        sh.row_heights[r] = 22.0
    sh.set_cell(4, 2, "Project Name", style=15)
    sh.set_cell(4, 4, "ConcertShield System", style=1)
    sh.merge_cells(4, 4, 4, 6)

    sh.set_cell(5, 2, "Project Code", style=15)
    sh.set_cell(5, 4, "CSS", style=1)
    sh.merge_cells(5, 4, 5, 6)

    sh.set_cell(6, 2, "Normal number of Test cases/KLOC", style=15)
    sh.set_cell(6, 4, 100.0, style=16)
    sh.merge_cells(6, 4, 6, 6)

    sh.set_cell(7, 2, "Test Environment Setup Description", style=15)
    sh.set_cell(7, 4, ".NET 8.0 SDK (C# 12) | xUnit 2.5+, Moq 4.20+, FluentAssertions | PostgreSQL 16 & EF Core InMemoryDatabase | WebApplicationFactory", style=1)
    sh.merge_cells(7, 4, 7, 8)

    # Table Header at Row 10
    sh.row_heights[10] = 28.0
    headers = ["No", "Requirement Name", "Class Name", "Function Name", "Function Code(Optional)", "Sheet Name", "Description", "Pre-Condition"]
    for c_idx, h in enumerate(headers, 1):
        sh.set_cell(10, c_idx, h, style=5)

    # Rows 11+
    for i, fc in enumerate(functions, 1):
        r = 10 + i
        sh.row_heights[r] = 22.0
        sh.set_cell(r, 1, float(i), style=2)
        sh.set_cell(r, 2, fc['req_name'], style=1)
        sh.set_cell(r, 3, fc['class_name'], style=1)
        sh.set_cell(r, 4, fc['name'], style=1)
        sh.set_cell(r, 5, fc['code'], style=1)
        # Hyperlink to sheet
        sh.set_cell(r, 6, fc['sheet_name'], style=9)
        sh.add_hyperlink(r, 6, fc['sheet_name'], fc['sheet_name'])
        sh.set_cell(r, 7, fc['test_req'], style=1)
        precond = fc['preconditions'][0][0] if fc['preconditions'] else ''
        sh.set_cell(r, 8, precond, style=1)

    # Footnote
    footer_row = 11 + len(functions) + 1
    sh.set_cell(footer_row, 5, "Tên hàm trong code", style=17)
    sh.set_cell(footer_row, 6, "Tên của sheet", style=17)

    return sh

def create_test_report_sheet(functions, stats_list):
    sh = StyledSheet("Test Report")
    sh.col_widths = {
        1: 6.0,   # No
        2: 28.0,  # Function code
        3: 10.0,  # Passed
        4: 10.0,  # Failed
        5: 10.0,  # Untested
        6: 8.0,   # N
        7: 8.0,   # A
        8: 8.0,   # B
        9: 14.0   # Total Test Cases
    }

    # Info Box
    for r in range(1, 5):
        sh.row_heights[r] = 22.0
    sh.set_cell(1, 1, "Project Name", style=15)
    sh.set_cell(1, 2, "ConcertShield System", style=1)
    sh.set_cell(1, 5, "Creator", style=15)
    sh.set_cell(1, 6, "Developer", style=1)

    sh.set_cell(2, 1, "Project Code", style=15)
    sh.set_cell(2, 2, "CSS", style=1)
    sh.set_cell(2, 5, "Reviewer/Approver", style=15)
    sh.set_cell(2, 6, "Technical Lead / QA Lead", style=1)

    sh.set_cell(3, 1, "Document Code", style=15)
    sh.set_cell(3, 2, "CSS_Test Report_v1.0", style=1)
    sh.set_cell(3, 5, "Issue Date", style=15)
    sh.set_cell(3, 6, "28/10/2026", style=1)

    sh.set_cell(4, 1, "Notes", style=15)
    sh.set_cell(4, 2, "Release includes 6 Modules: Payment & Voucher, Dynamic QR & Wallet, Governance & Policies, Check-in Monitoring, QR Scanner, Notification Management", style=1)
    sh.merge_cells(4, 2, 4, 9)

    # Table Header at Row 7
    sh.row_heights[7] = 28.0
    headers = ["No", "Function code", "Passed", "Failed", "Untested", "N", "A", "B", "Total Test Cases"]
    for c_idx, h in enumerate(headers, 1):
        sh.set_cell(7, c_idx, h, style=5)

    # Rows 8+
    start_r = 8
    for i, (fc, st) in enumerate(zip(functions, stats_list), 1):
        r = start_r + i - 1
        sh.row_heights[r] = 22.0
        sh.set_cell(r, 1, float(i), style=2)
        # Function code with hyperlink
        sh.set_cell(r, 2, fc['code'], style=9)
        sh.add_hyperlink(r, 2, fc['sheet_name'], fc['code'])
        sh.set_cell(r, 3, float(st['passed']), style=2)
        sh.set_cell(r, 4, float(st['failed']), style=2)
        sh.set_cell(r, 5, float(st['untested']), style=2)
        sh.set_cell(r, 6, float(st['n']), style=2)
        sh.set_cell(r, 7, float(st['a']), style=2)
        sh.set_cell(r, 8, float(st['b']), style=2)
        sh.set_cell(r, 9, float(st['total']), style=2)

    end_r = start_r + len(functions) - 1
    subtotal_r = end_r + 1
    sh.row_heights[subtotal_r] = 26.0
    sh.set_cell(subtotal_r, 1, "Sub total", style=12)
    sh.merge_cells(subtotal_r, 1, subtotal_r, 2)
    sh.set_cell(subtotal_r, 3, f"=SUM(C{start_r}:C{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 4, f"=SUM(D{start_r}:D{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 5, f"=SUM(E{start_r}:E{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 6, f"=SUM(F{start_r}:F{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 7, f"=SUM(G{start_r}:G{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 8, f"=SUM(H{start_r}:H{end_r})", style=12, is_formula=True)
    sh.set_cell(subtotal_r, 9, f"=SUM(I{start_r}:I{end_r})", style=12, is_formula=True)

    # Statistical summaries
    stat_start = subtotal_r + 2
    metrics = [
        ("Test coverage", f"=((C{subtotal_r}+D{subtotal_r})/I{subtotal_r})*100"),
        ("Test successful coverage", f"=(C{subtotal_r}/I{subtotal_r})*100"),
        ("Normal case", f"=(F{subtotal_r}/I{subtotal_r})*100"),
        ("Abnormal case", f"=(G{subtotal_r}/I{subtotal_r})*100"),
        ("Boundary case", f"=(H{subtotal_r}/I{subtotal_r})*100")
    ]
    for idx, (label, formula) in enumerate(metrics):
        r = stat_start + idx
        sh.row_heights[r] = 22.0
        sh.set_cell(r, 2, label, style=3)
        sh.set_cell(r, 3, formula, style=4, is_formula=True)
        sh.set_cell(r, 4, "%", style=3)

    return sh

def create_function_sheet(fc):
    sh = StyledSheet(fc['sheet_name'])
    num_tc = fc['num_tc']
    tc_types = fc['tc_types']
    tc_status = fc['tc_status']
    exec_date = fc.get('executed_date', '28/10/2026')

    passed_cnt = sum(1 for s in tc_status if s == 'P')
    failed_cnt = sum(1 for s in tc_status if s == 'F')
    untested_cnt = sum(1 for s in tc_status if s not in ('P', 'F'))
    n_cnt = sum(1 for t in tc_types if t == 'N')
    a_cnt = sum(1 for t in tc_types if t == 'A')
    b_cnt = sum(1 for t in tc_types if t == 'B')

    # Column Widths
    col_w = {
        1: 5.0,   # Col A: Sidebar (Condition / Confirm / Result)
        2: 16.0,  # Col B: Precondition / Input name
        3: 2.5,   # Col C: spacer
        4: 38.0,  # Col D: Input value / expectation
        5: 2.5    # Col E: spacer
    }
    for c in range(6, 6 + num_tc):
        col_w[c] = 5.2  # Compact test case columns
    sh.col_widths = col_w

    # Row 1: Function Code & Function Name
    sh.row_heights[1] = 22.0
    sh.set_cell(1, 1, "Function Code", style=15)
    sh.merge_cells(1, 1, 1, 2)
    sh.set_cell(1, 3, fc['code'], style=1)
    sh.merge_cells(1, 3, 1, 5)
    sh.set_cell(1, 6, "Function Name", style=15)
    sh.merge_cells(1, 6, 1, 8)
    sh.set_cell(1, 9, fc['name'], style=1)
    sh.merge_cells(1, 9, 1, 5 + num_tc)

    # Row 2: Created By & Executed By
    sh.row_heights[2] = 22.0
    sh.set_cell(2, 1, "Created By", style=15)
    sh.merge_cells(2, 1, 2, 2)
    sh.set_cell(2, 3, "Developer", style=1)
    sh.merge_cells(2, 3, 2, 5)
    sh.set_cell(2, 6, "Executed By", style=15)
    sh.merge_cells(2, 6, 2, 8)
    sh.set_cell(2, 9, "QA Tester", style=1)
    sh.merge_cells(2, 9, 2, 5 + num_tc)

    # Row 3: Lines of code & Lack of test cases
    sh.row_heights[3] = 22.0
    sh.set_cell(3, 1, "Lines  of code", style=15)
    sh.merge_cells(3, 1, 3, 2)
    sh.set_cell(3, 3, float(fc['loc']), style=1)
    sh.merge_cells(3, 3, 3, 5)
    sh.set_cell(3, 6, "Lack of test cases", style=15)
    sh.merge_cells(3, 6, 3, 8)
    sh.set_cell(3, 9, 0.0, style=1)
    sh.merge_cells(3, 9, 3, 5 + num_tc)

    # Row 4: Test requirement
    sh.row_heights[4] = 24.0
    sh.set_cell(4, 1, "Test requirement", style=15)
    sh.merge_cells(4, 1, 4, 2)
    sh.set_cell(4, 3, fc['test_req'], style=1)
    sh.merge_cells(4, 3, 4, 5 + num_tc)

    # Row 5: Counts Header
    sh.row_heights[5] = 22.0
    sh.set_cell(5, 1, "Passed", style=15)
    sh.merge_cells(5, 1, 5, 2)
    sh.set_cell(5, 3, "Failed", style=15)
    sh.merge_cells(5, 3, 5, 5)
    sh.set_cell(5, 6, "Untested", style=15)
    sh.merge_cells(5, 6, 5, 7)
    sh.set_cell(5, 8, "N/A/B", style=15)
    sh.merge_cells(5, 8, 5, 10)
    sh.set_cell(5, 11, "Total Test Cases", style=15)
    sh.merge_cells(5, 11, 5, 5 + num_tc)

    # Row 6: Counts Values
    sh.row_heights[6] = 22.0
    sh.set_cell(6, 1, float(passed_cnt), style=2)
    sh.merge_cells(6, 1, 6, 2)
    sh.set_cell(6, 3, float(failed_cnt), style=2)
    sh.merge_cells(6, 3, 6, 5)
    sh.set_cell(6, 6, float(untested_cnt), style=2)
    sh.merge_cells(6, 6, 6, 7)
    sh.set_cell(6, 8, float(n_cnt), style=2)
    sh.set_cell(6, 9, float(a_cnt), style=2)
    sh.set_cell(6, 10, float(b_cnt), style=2)
    sh.set_cell(6, 11, float(num_tc), style=4)
    sh.merge_cells(6, 11, 6, 5 + num_tc)

    # Row 8: Matrix Header with UTCID01.. rotated
    sh.row_heights[8] = 48.0
    for c in range(1, 6):
        sh.set_cell(8, c, "", style=5)
    for i in range(num_tc):
        tc_id = f"UTCID{i+1:02d}"
        col = 6 + i
        # Red text for first 4 as in template screenshot, white for others
        st_hdr = 7 if i < 4 else 6
        sh.set_cell(8, col, tc_id, style=st_hdr)

    curr_row = 9

    # --- SECTION 1: CONDITION ---
    cond_start_row = curr_row

    # Precondition header
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Precondition", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for c in range(6, 6 + num_tc):
        sh.set_cell(curr_row, c, "", style=1)
    curr_row += 1

    # Precondition items
    for pre_text, applied_tcs in fc['preconditions']:
        sh.row_heights[curr_row] = 20.0
        sh.set_cell(curr_row, 2, "", style=1)
        sh.set_cell(curr_row, 3, "", style=1)
        sh.set_cell(curr_row, 4, pre_text, style=1)
        sh.set_cell(curr_row, 5, "", style=1)
        sh.merge_cells(curr_row, 3, curr_row, 5)
        for i in range(num_tc):
            mark = "O" if (i + 1) in applied_tcs else ""
            sh.set_cell(curr_row, 6 + i, mark, style=2)
        curr_row += 1

    # Input items
    for param_name, val_list in fc['inputs']:
        # Param header
        sh.row_heights[curr_row] = 22.0
        sh.set_cell(curr_row, 2, param_name, style=3)
        sh.merge_cells(curr_row, 2, curr_row, 5)
        for c in range(6, 6 + num_tc):
            sh.set_cell(curr_row, c, "", style=1)
        curr_row += 1

        for val_text, applied_tcs in val_list:
            sh.row_heights[curr_row] = 20.0
            sh.set_cell(curr_row, 2, "", style=1)
            sh.set_cell(curr_row, 3, "", style=1)
            sh.set_cell(curr_row, 4, val_text, style=1)
            sh.set_cell(curr_row, 5, "", style=1)
            sh.merge_cells(curr_row, 3, curr_row, 5)
            for i in range(num_tc):
                mark = "O" if (i + 1) in applied_tcs else ""
                sh.set_cell(curr_row, 6 + i, mark, style=2)
            curr_row += 1

    cond_end_row = curr_row - 1
    # Merge sidebar Condition
    sh.set_cell(cond_start_row, 1, "Condition", style=8)
    sh.merge_cells(cond_start_row, 1, cond_end_row, 1)

    # --- SECTION 2: CONFIRM ---
    confirm_start_row = curr_row

    # Return
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Return", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for c in range(6, 6 + num_tc):
        sh.set_cell(curr_row, c, "", style=1)
    curr_row += 1

    for ret_text, applied_tcs in fc['returns']:
        sh.row_heights[curr_row] = 20.0
        sh.set_cell(curr_row, 2, "", style=1)
        sh.set_cell(curr_row, 3, "", style=1)
        sh.set_cell(curr_row, 4, ret_text, style=1)
        sh.set_cell(curr_row, 5, "", style=1)
        sh.merge_cells(curr_row, 3, curr_row, 5)
        for i in range(num_tc):
            mark = "O" if (i + 1) in applied_tcs else ""
            sh.set_cell(curr_row, 6 + i, mark, style=2)
        curr_row += 1

    # Exception
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Exception", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for c in range(6, 6 + num_tc):
        sh.set_cell(curr_row, c, "", style=1)
    curr_row += 1

    for exc_text, applied_tcs in fc['exceptions']:
        sh.row_heights[curr_row] = 20.0
        sh.set_cell(curr_row, 2, "", style=1)
        sh.set_cell(curr_row, 3, "", style=1)
        sh.set_cell(curr_row, 4, exc_text, style=1)
        sh.set_cell(curr_row, 5, "", style=1)
        sh.merge_cells(curr_row, 3, curr_row, 5)
        for i in range(num_tc):
            mark = "O" if (i + 1) in applied_tcs else ""
            sh.set_cell(curr_row, 6 + i, mark, style=2)
        curr_row += 1

    # Log message
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Log message", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for c in range(6, 6 + num_tc):
        sh.set_cell(curr_row, c, "", style=1)
    curr_row += 1

    for log_text, applied_tcs in fc['logs']:
        sh.row_heights[curr_row] = 20.0
        sh.set_cell(curr_row, 2, "", style=1)
        sh.set_cell(curr_row, 3, "", style=1)
        sh.set_cell(curr_row, 4, log_text, style=1)
        sh.set_cell(curr_row, 5, "", style=1)
        sh.merge_cells(curr_row, 3, curr_row, 5)
        for i in range(num_tc):
            mark = "O" if (i + 1) in applied_tcs else ""
            sh.set_cell(curr_row, 6 + i, mark, style=2)
        curr_row += 1

    confirm_end_row = curr_row - 1
    sh.set_cell(confirm_start_row, 1, "Confirm", style=8)
    sh.merge_cells(confirm_start_row, 1, confirm_end_row, 1)

    # --- SECTION 3: RESULT ---
    result_start_row = curr_row

    # Type
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Type(N : Normal, A : Abnormal, B : Boundary)", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for i in range(num_tc):
        sh.set_cell(curr_row, 6 + i, tc_types[i], style=2)
    curr_row += 1

    # Passed/Failed
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Passed/Failed", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for i in range(num_tc):
        sh.set_cell(curr_row, 6 + i, tc_status[i], style=2)
    curr_row += 1

    # Executed Date
    sh.row_heights[curr_row] = 22.0
    sh.set_cell(curr_row, 2, "Executed Date", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    for i in range(num_tc):
        sh.set_cell(curr_row, 6 + i, exec_date, style=2)
    curr_row += 1

    # Defect ID
    sh.row_heights[curr_row] = 45.0
    sh.set_cell(curr_row, 2, "Defect ID", style=3)
    sh.merge_cells(curr_row, 2, curr_row, 5)
    defects = fc.get('defect_ids', [''] * num_tc)
    for i in range(num_tc):
        sh.set_cell(curr_row, 6 + i, defects[i], style=14)
    curr_row += 1

    result_end_row = curr_row - 1
    sh.set_cell(result_start_row, 1, "Result", style=8)
    sh.merge_cells(result_start_row, 1, result_end_row, 1)

    return sh, {
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

def create_guideline_sheet():
    sh = StyledSheet("Guideline")
    sh.col_widths = {1: 8.0, 2: 90.0}
    sh.set_cell(2, 2, "Guideline to make and understand Unit Test Case", style=11)
    
    guideline_lines = [
        "",
        "1. Overview",
        " - In the template, Unit test cases are based on functions. Each sheet presents test cases for one function.",
        " - Cover: General information of the project and Unit Test cases",
        " - FunctionList: The list of Classes and Functions in the document.",
        "     + To control that the number of Unit TC meets customer requirement or the norm, user should fill value for Normal number of Test cases/KLOC.",
        "     + Click on Function link to open the related Test cases of the function.",
        " - Test Report: provide the overview results of Functions Unit test: Test coverage, Test successful coverage (Summary, for normal/abnormal/boundary cases)",
        "",
        "2. Content in Test function sheet",
        "2.1 Combination of test cases.",
        " - To verify that number of Unit TC meets customer requirement or not. User has to fill number LOC of tested function and fill value of Normal number test cases/KLOC item in FunctionList sheet.",
        " 2.2 Condition and confirmation of Test cases.",
        " Each test case is the combination of condition and confirmation.",
        "a. Condition:",
        "        - Condition is combination of precondition and values of inputs.",
        "        - Precondition: it is setting condition that must exist before execution of the test case.",
        "        - Values of inputs: it includes 3 types of values: normal, boundary and abnormal.",
        "b. Confirmation:",
        "        - Output result of the function, exceptions, or log messages.",
        "c. Type of test cases and result:",
        "        - Type of test case: Normal (N), Boundary (B), Abnormal (A).",
        "        - Test case result: P for Passed and F for Failed cases."
    ]
    for r_idx, line in enumerate(guideline_lines, 4):
        style = 3 if line.startswith(("1.", "2.", "a.", "b.", "c.")) else 1
        sh.set_cell(r_idx, 2, line, style=style)
    return sh

def main():
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    wb = StyledWorkbook()

    # 1. Guideline Sheet
    guide_sh = create_guideline_sheet()
    wb.add_sheet(guide_sh)

    # 2. Cover Sheet
    cover_sh = create_cover_sheet()
    wb.add_sheet(cover_sh)

    # 3. FunctionList Sheet
    fn_list_sh = create_function_list_sheet(ALL_FUNCTIONS)
    wb.add_sheet(fn_list_sh)

    # 4. Generate all 27 Function Sheets
    stats_list = []
    for fc in ALL_FUNCTIONS:
        sh, st = create_function_sheet(fc)
        wb.add_sheet(sh)
        stats_list.append(st)

    # 5. Test Report Sheet
    report_sh = create_test_report_sheet(ALL_FUNCTIONS, stats_list)
    wb.add_sheet(report_sh)

    # Save styled workbook
    out_path = os.path.join(base_dir, 'Report5_UnitTest_ConcertShield.xlsx')
    wb.save(out_path)
    print("Master styled workbook generated:", out_path)
    
    # Try saving to UnitTest_ConcertShield_Specification.xlsx if not locked
    alt_path = os.path.join(base_dir, 'UnitTest_ConcertShield_Specification_v2.xlsx')
    wb.save(alt_path)
    print("Alternative styled workbook generated:", alt_path)

if __name__ == '__main__':
    main()
