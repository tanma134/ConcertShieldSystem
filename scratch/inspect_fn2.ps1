$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn2 = $wb.Sheets.Item("Function2")
    Write-Host "Function2 UsedRange: $($fn2.UsedRange.Address)"
    Write-Host "Function2 Row 9 (UTCIDs):"
    $r9 = for ($c = 6; $c -le 20; $c++) { $fn2.Cells.Item(9, $c).Text }
    Write-Host ($r9 -join " | ")
    Write-Host "Function2 Row 42 (Types):"
    $r42 = for ($c = 6; $c -le 20; $c++) { $fn2.Cells.Item(42, $c).Text }
    Write-Host ($r42 -join " | ")
    Write-Host "Function2 Row 43 (Status):"
    $r43 = for ($c = 6; $c -le 20; $c++) { $fn2.Cells.Item(43, $c).Text }
    Write-Host ($r43 -join " | ")

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
