$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn1 = $wb.Sheets.Item(5)
    
    Write-Host "Initial Condition MergeArea: $($fn1.Range('A10').MergeArea.Address)"
    Write-Host "Initial Confirm MergeArea: $($fn1.Range('A34').MergeArea.Address)"
    Write-Host "Initial Result MergeArea: $($fn1.Range('A42').MergeArea.Address)"

    # Insert a row at row 20 (inside Condition)
    $fn1.Rows.Item(20).Copy()
    $fn1.Rows.Item(21).Insert(-4121)
    
    Write-Host "After insert at row 21:"
    Write-Host "Condition MergeArea: $($fn1.Range('A10').MergeArea.Address)"
    Write-Host "Confirm MergeArea: $($fn1.Range('A35').MergeArea.Address)"
    Write-Host "Result MergeArea: $($fn1.Range('A43').MergeArea.Address)"

    $wb.Close($false)
    Write-Host "Merge expansion test succeeded!"
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
