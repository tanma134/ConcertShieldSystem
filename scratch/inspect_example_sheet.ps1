$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$ex = $wb.Sheets.Item('Example')

Write-Host "Example Sheet Layout:"
for ($r = 1; $r -le 46; $r++) {
    $c1 = $ex.Cells.Item($r, 1).Text
    $c2 = $ex.Cells.Item($r, 2).Text
    $c4 = $ex.Cells.Item($r, 4).Text
    $c6 = $ex.Cells.Item($r, 6).Text
    $c7 = $ex.Cells.Item($r, 7).Text
    if ($c1 -ne "" -or $c2 -ne "" -or $c4 -ne "" -or $c6 -ne "") {
        Write-Host ("R{0:D2} | A='{1}' | B='{2}' | D='{3}' | F='{4}' | G='{5}'" -f $r, $c1, $c2, $c4, $c6, $c7)
    }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
