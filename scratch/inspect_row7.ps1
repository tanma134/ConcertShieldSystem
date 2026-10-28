$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$s5 = $wb.Sheets.Item(5)

Write-Host "Row 2 and Row 7 formulas of Sheet 5:"
for ($c = 1; $c -le 20; $c++) {
    $f2 = $s5.Cells.Item(2, $c).Formula
    $v2 = $s5.Cells.Item(2, $c).Text
    $f7 = $s5.Cells.Item(7, $c).Formula
    $v7 = $s5.Cells.Item(7, $c).Text
    if ($v2 -ne "" -or $f2 -like "=*") { Write-Host ("C{0:D2} R2: val='{1}', formula='{2}'" -f $c, $v2, $f2) }
    if ($v7 -ne "" -or $f7 -like "=*") { Write-Host ("C{0:D2} R7: val='{1}', formula='{2}'" -f $c, $v7, $f7) }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
