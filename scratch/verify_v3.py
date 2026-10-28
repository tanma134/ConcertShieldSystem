ps_script = """
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\\BEN9\\3\\ConcertShieldSystem\\Report5_Unit Test Case_ConcertShield_v3.xlsx")
    Write-Host "Total Sheets: $($wb.Sheets.Count)"
    
    # 1. FunctionList
    $fl = $wb.Sheets.Item("FunctionList")
    Write-Host "--- FunctionList ---"
    Write-Host "Row 10 (Header): $($fl.Cells.Item(10, 1).Text) | $($fl.Cells.Item(10, 2).Text) | Fill=$($fl.Cells.Item(10, 2).Interior.Color)"
    Write-Host "Row 11 (UC 1): No=$($fl.Cells.Item(11, 1).Text), Req=$($fl.Cells.Item(11, 2).Text), Class=$($fl.Cells.Item(11, 3).Text), Sheet=$($fl.Cells.Item(11, 6).Text), Ht=$($fl.Rows.Item(11).RowHeight)"
    Write-Host "Row 37 (UC 27): No=$($fl.Cells.Item(37, 1).Text), Req=$($fl.Cells.Item(37, 2).Text), Class=$($fl.Cells.Item(37, 3).Text), Sheet=$($fl.Cells.Item(37, 6).Text), Ht=$($fl.Rows.Item(37).RowHeight)"
    Write-Host "Row 38 (Empty): '$($fl.Cells.Item(38, 1).Text)'"
    
    # 2. Test Report
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "--- Test Report ---"
    Write-Host "Row 12 (UC 1): $($tr.Cells.Item(12, 1).Text) | $($tr.Cells.Item(12, 2).Text) | P=$($tr.Cells.Item(12, 3).Text) | F=$($tr.Cells.Item(12, 4).Text) | N=$($tr.Cells.Item(12, 6).Text) | A=$($tr.Cells.Item(12, 7).Text) | B=$($tr.Cells.Item(12, 8).Text) | Tot=$($tr.Cells.Item(12, 9).Text)"
    Write-Host "Row 18 (UC 7): $($tr.Cells.Item(18, 1).Text) | $($tr.Cells.Item(18, 2).Text) | P=$($tr.Cells.Item(18, 3).Text) | F=$($tr.Cells.Item(18, 4).Text) | N=$($tr.Cells.Item(18, 6).Text) | A=$($tr.Cells.Item(18, 7).Text) | B=$($tr.Cells.Item(18, 8).Text) | Tot=$($tr.Cells.Item(18, 9).Text)"
    Write-Host "Row 38 (UC 27): $($tr.Cells.Item(38, 1).Text) | $($tr.Cells.Item(38, 2).Text) | P=$($tr.Cells.Item(38, 3).Text) | Tot=$($tr.Cells.Item(38, 9).Text)"
    Write-Host "Row 39 (Sub total): Text='$($tr.Cells.Item(39, 2).Text)' | P=$($tr.Cells.Item(39, 3).Text) | Tot=$($tr.Cells.Item(39, 9).Text) | Fml='$($tr.Cells.Item(39, 3).Formula)'"
    Write-Host "Row 40 (%): Text='$($tr.Cells.Item(40, 2).Text)' | P=$($tr.Cells.Item(40, 3).Text) | N=$($tr.Cells.Item(40, 6).Text) | A=$($tr.Cells.Item(40, 7).Text) | B=$($tr.Cells.Item(40, 8).Text)"
    Write-Host "Row 42 (Normal case): $($tr.Cells.Item(42, 2).Text) = $($tr.Cells.Item(42, 4).Text) %"
    Write-Host "Row 43 (Abnormal case): $($tr.Cells.Item(43, 2).Text) = $($tr.Cells.Item(43, 4).Text) %"
    Write-Host "Row 44 (Boundary case): $($tr.Cells.Item(44, 2).Text) = $($tr.Cells.Item(44, 4).Text) %"

    # Check charts
    Write-Host "Charts count: $($tr.ChartObjects().Count)"
    for ($ch = 1; $ch -le $tr.ChartObjects().Count; $ch++) {
        $cObj = $tr.ChartObjects().Item($ch)
        Write-Host "Chart $ch : Top=$($cObj.Top), Left=$($cObj.Left), Width=$($cObj.Width), Height=$($cObj.Height)"
    }
    
    # 3. UC_7.5_ViewOrgVouchers
    $fn1 = $wb.Sheets.Item("UC_7.5_ViewOrgVouchers")
    Write-Host "--- UC_7.5 Inspection ---"
    Write-Host "Row 2: C2='$($fn1.Range('C2').Text)', L2='$($fn1.Range('L2').Text)'"
    Write-Host "Row 7 Summary: Passed=$($fn1.Range('A7').Text), Failed=$($fn1.Range('C7').Text), Untested=$($fn1.Range('F7').Text), N=$($fn1.Range('L7').Text), A=$($fn1.Range('M7').Text), B=$($fn1.Range('N7').Text), Tot=$($fn1.Range('O7').Text)"
    Write-Host "Condition MergeArea: $($fn1.Range('A10').MergeArea.Address)"
    Write-Host "Row 9 UTCIDs: F9='$($fn1.Range('F9').Text)', O9='$($fn1.Range('O9').Text)', P9='$($fn1.Range('P9').Text)' (P9 should be empty)"
    Write-Host "Check X35: '$($fn1.Range('X35').Text)' (should be empty)"
    
    # Check row heights and border styling
    Write-Host "Row 10 Height: $($fn1.Rows.Item(10).RowHeight)"
    Write-Host "Row 11 Height: $($fn1.Rows.Item(11).RowHeight)"
    Write-Host "Row 44 (Executed Date) Height: $($fn1.Rows.Item(44).RowHeight)"
    Write-Host "Row 45 (Defect ID) Height: $($fn1.Rows.Item(45).RowHeight)"
    Write-Host "Executed Date F44: '$($fn1.Range('F44').Text)'"

    # 4. Check UC_8.7 (extra condition rows)
    $fn8 = $wb.Sheets.Item("UC_8.7_BulkRevokeTickets")
    Write-Host "--- UC_8.7 ---"
    Write-Host "Row 7 Summary: Passed=$($fn8.Range('A7').Text), Failed=$($fn8.Range('C7').Text), Tot=$($fn8.Range('O7').Text)"

    # 5. Check UC_14.6 (extra returns and exceptions)
    $fn18 = $wb.Sheets.Item("UC_14.6_SyncOffline")
    Write-Host "--- UC_14.6 ---"
    Write-Host "Row 7 Summary: Passed=$($fn18.Range('A7').Text), Failed=$($fn18.Range('C7').Text), Tot=$($fn18.Range('O7').Text)"

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
"""

with open(r'D:\BEN9\3\ConcertShieldSystem\scratch\run_verify_v3.ps1', 'w', encoding='utf-8') as f:
    f.write(ps_script)
print("Verify v3 script written.")
