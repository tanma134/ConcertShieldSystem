$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fl = $wb.Sheets.Item("FunctionList")
    Write-Host "Original rows in FL: $($fl.UsedRange.Rows.Count)"
    
    # Copy row 13 and insert at row 14
    $fl.Rows.Item(13).Copy()
    $fl.Rows.Item(14).Insert(-4121)
    $excel.CutCopyMode = $false
    Write-Host "After insert, row 14 text: '$($fl.Cells.Item(14, 1).Text)' Fill: $($fl.Cells.Item(14, 1).Interior.Color)"

    $wb.Close($false)
    Write-Host "Insert test succeeded!"
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
