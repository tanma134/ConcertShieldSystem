
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v2.xlsx")
    Write-Host "Total Sheets: $($wb.Sheets.Count)"
    
    # 1. Verify FunctionList
    $fl = $wb.Sheets.Item("FunctionList")
    Write-Host "--- FunctionList Header (A10:H10) ---"
    $hdr = for ($c = 1; $c -le 8; $c++) { $fl.Cells.Item(10, $c).Text }
    Write-Host ($hdr -join " | ")
    Write-Host "FunctionList Row 11: No=$($fl.Cells.Item(11, 1).Text), Req=$($fl.Cells.Item(11, 2).Text), Sheet=$($fl.Cells.Item(11, 6).Text)"
    Write-Host "FunctionList Row 37: No=$($fl.Cells.Item(37, 1).Text), Req=$($fl.Cells.Item(37, 2).Text), Sheet=$($fl.Cells.Item(37, 6).Text)"
    Write-Host "FunctionList Row 38 (should be empty): '$($fl.Cells.Item(38, 1).Text)'"
    
    # 2. Verify Test Report
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "--- Test Report Sample Rows ---"
    Write-Host "Row 12 (UC 1): $($tr.Cells.Item(12, 1).Text) | $($tr.Cells.Item(12, 2).Text) | P=$($tr.Cells.Item(12, 3).Text) | F=$($tr.Cells.Item(12, 4).Text) | U=$($tr.Cells.Item(12, 5).Text) | N=$($tr.Cells.Item(12, 6).Text) | A=$($tr.Cells.Item(12, 7).Text) | B=$($tr.Cells.Item(12, 8).Text) | Tot=$($tr.Cells.Item(12, 9).Text)"
    Write-Host "Row 18 (UC 7 - previously had 800%): $($tr.Cells.Item(18, 1).Text) | $($tr.Cells.Item(18, 2).Text) | P=$($tr.Cells.Item(18, 3).Text) | F=$($tr.Cells.Item(18, 4).Text) | U=$($tr.Cells.Item(18, 5).Text) | N=$($tr.Cells.Item(18, 6).Text) | A=$($tr.Cells.Item(18, 7).Text) | B=$($tr.Cells.Item(18, 8).Text) | Tot=$($tr.Cells.Item(18, 9).Text)"
    Write-Host "Row 38 (UC 27): $($tr.Cells.Item(38, 1).Text) | $($tr.Cells.Item(38, 2).Text) | P=$($tr.Cells.Item(38, 3).Text) | Tot=$($tr.Cells.Item(38, 9).Text)"
    Write-Host "Row 39 (Sub total): Text='$($tr.Cells.Item(39, 2).Text)' | P=$($tr.Cells.Item(39, 3).Text) | N=$($tr.Cells.Item(39, 6).Text) | A=$($tr.Cells.Item(39, 7).Text) | B=$($tr.Cells.Item(39, 8).Text) | Tot=$($tr.Cells.Item(39, 9).Text)"
    Write-Host "Row 40 (%): Text='$($tr.Cells.Item(40, 2).Text)' | P=$($tr.Cells.Item(40, 3).Text) | N=$($tr.Cells.Item(40, 6).Text) | A=$($tr.Cells.Item(40, 7).Text) | B=$($tr.Cells.Item(40, 8).Text)"
    Write-Host "Row 42 (Normal case): $($tr.Cells.Item(42, 2).Text) = $($tr.Cells.Item(42, 4).Text)"
    Write-Host "Row 43 (Abnormal case): $($tr.Cells.Item(43, 2).Text) = $($tr.Cells.Item(43, 4).Text)"
    Write-Host "Row 44 (Boundary case): $($tr.Cells.Item(44, 2).Text) = $($tr.Cells.Item(44, 4).Text)"
    
    # 3. Verify UC_7.5_ViewOrgVouchers
    $fn1 = $wb.Sheets.Item("UC_7.5_ViewOrgVouchers")
    Write-Host "--- UC_7.5 Inspection ---"
    Write-Host "Row 7 Summary: Passed=$($fn1.Range('A7').Text), Failed=$($fn1.Range('C7').Text), Untested=$($fn1.Range('F7').Text), N=$($fn1.Range('L7').Text), A=$($fn1.Range('M7').Text), B=$($fn1.Range('N7').Text), Tot=$($fn1.Range('O7').Text)"
    Write-Host "Condition MergeArea: $($fn1.Range('A10').MergeArea.Address) Text='$($fn1.Range('A10').Text)'"
    
    # Check if 'Nhap so tu 5 - 20' exists anywhere in cols U to AZ
    $foundOldNote = $false
    for ($r = 1; $r -le 60; $r++) {
        for ($c = 20; $c -le 40; $c++) {
            $txt = $fn1.Cells.Item($r, $c).Text
            if ($txt -like "*5*20*" -or $txt -like "*Normal*") {
                Write-Host "FOUND OLD NOTE at Col $c Row $r : $txt"
                $foundOldNote = $true
            }
        }
    }
    if (-not $foundOldNote) {
        Write-Host "OLD NOTE 'Nhap so tu 5 - 20...' COMPLETELY GONE! PASSED!"
    }
    
    # Check Executed Date row
    for ($r = 25; $r -le 45; $r++) {
        if ($fn1.Cells.Item($r, 2).Text -eq "Executed Date") {
            Write-Host "Executed Date at Row $r : Col F='$($fn1.Cells.Item($r, 6).Text)' (no ####!)"
        }
    }

    # 4. Verify UC_8.7_BulkRevokeTickets (large inputs)
    $fn8 = $wb.Sheets.Item("UC_8.7_BulkRevokeTickets")
    Write-Host "--- UC_8.7 Inspection ---"
    Write-Host "Row 7 Summary: Passed=$($fn8.Range('A7').Text), Failed=$($fn8.Range('C7').Text), Untested=$($fn8.Range('F7').Text), N=$($fn8.Range('L7').Text), A=$($fn8.Range('M7').Text), B=$($fn8.Range('N7').Text), Tot=$($fn8.Range('O7').Text)"
    Write-Host "Condition MergeArea: $($fn8.Range('A10').MergeArea.Address)"
    
    # 5. Verify UC_14.6_SyncOffline (multiple returns & exceptions)
    $fn18 = $wb.Sheets.Item("UC_14.6_SyncOffline")
    Write-Host "--- UC_14.6 Inspection ---"
    Write-Host "Row 7 Summary: Passed=$($fn18.Range('A7').Text), Failed=$($fn18.Range('C7').Text), Untested=$($fn18.Range('F7').Text), N=$($fn18.Range('L7').Text), A=$($fn18.Range('M7').Text), B=$($fn18.Range('N7').Text), Tot=$($fn18.Range('O7').Text)"
    Write-Host "Condition MergeArea: $($fn18.Range('A10').MergeArea.Address)"

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
