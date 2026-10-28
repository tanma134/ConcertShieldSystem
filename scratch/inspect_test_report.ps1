$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$tr = $wb.Sheets.Item('Test Report')

Write-Host "Test Report Rows 1 to 30:"
for ($r = 1; $r -le 25; $r++) {
    $rowText = ""
    for ($c = 1; $c -le 10; $c++) {
        $v = $tr.Cells.Item($r, $c).Text
        $f = $tr.Cells.Item($r, $c).Formula
        if ($v -ne "") {
            if ($f -like "=*") { $rowText += "[$r,$c]=$v (f=$f)  " }
            else { $rowText += "[$r,$c]=$v  " }
        }
    }
    if ($rowText -ne "") { Write-Host $rowText }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
