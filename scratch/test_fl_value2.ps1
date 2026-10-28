$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$fl = $wb.Sheets.Item("FunctionList")

try {
    for ($i = 0; $i -lt 3; $i++) {
        $r = 11 + $i
        $fl.Cells.Item($r, 1).Value2 = [double]($i + 1)
        $fl.Cells.Item($r, 2).Value2 = "Req $($i+1)"
        $fl.Cells.Item($r, 3).Value2 = "Class $($i+1)"
        $fl.Cells.Item($r, 4).Value2 = "Func $($i+1)"
        $fl.Cells.Item($r, 5).Value2 = "Code $($i+1)"
        
        $flCell = $fl.Cells.Item($r, 6)
        $fl.Hyperlinks.Add($flCell, "", "'Cover'!A1", "", "Cover") | Out-Null
        
        $fl.Cells.Item($r, 7).Value2 = "Description $($i+1)"
        $fl.Cells.Item($r, 8).Value2 = "Precondition $($i+1)"
        Write-Host "Row $r populated."
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
