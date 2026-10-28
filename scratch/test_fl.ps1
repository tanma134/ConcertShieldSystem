$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$fl = $wb.Sheets.Item("FunctionList")

try {
    Write-Host "Testing cell assignment..."
    $fl.Cells.Item(11, 1).Value = 1
    $fl.Cells.Item(11, 2).Value = "Test Req"
    $fl.Cells.Item(11, 3).Value = "Test Class"
    Write-Host "Cell assignment succeeded."

    Write-Host "Testing Hyperlink..."
    $c = $fl.Cells.Item(11, 6)
    $fl.Hyperlinks.Add($c, "", "'Cover'!A1", "", "CoverLink") | Out-Null
    Write-Host "Hyperlink succeeded."
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
