$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v2.xlsx")
    $fn1 = $wb.Sheets.Item("UC_7.5_ViewOrgVouchers")
    Write-Host "A7 Formula: $($fn1.Range('A7').Formula)"
    Write-Host "A7 FormulaLocal: $($fn1.Range('A7').FormulaLocal)"
    Write-Host "O7 Formula: $($fn1.Range('O7').Formula)"
    Write-Host "O7 FormulaLocal: $($fn1.Range('O7').FormulaLocal)"

    # Check rows around resRow
    for ($r = 25; $r -le 45; $r++) {
        $b = $fn1.Cells.Item($r, 2).Text
        $f = $fn1.Cells.Item($r, 6).Text
        if ($b -or $f) {
            Write-Host "Row $r : B='$b' | F='$f' | Address=$($fn1.Cells.Item($r, 2).Address)"
        }
    }
    
    # Check Row 9 (UTCIDs)
    for ($c = 5; $c -le 16; $c++) {
        Write-Host "Row 9 Col $c : '$($fn1.Cells.Item(9, $c).Text)'"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
