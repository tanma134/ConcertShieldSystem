$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$s5 = $wb.Sheets.Item(5)

Write-Host "Rows 10 to 50 of Sheet 5:"
for ($r = 10; $r -le 48; $r++) {
    $c1 = $s5.Cells.Item($r, 1).Text
    $c2 = $s5.Cells.Item($r, 2).Text
    $c3 = $s5.Cells.Item($r, 3).Text
    $c4 = $s5.Cells.Item($r, 4).Text
    $tcs = ""
    for ($c = 6; $c -le 20; $c++) {
        $v = $s5.Cells.Item($r, $c).Text
        if ($v -ne "") { $tcs += " c$c=$v" }
    }
    Write-Host ("R{0:D2} | col1='{1}' | col2='{2}' | col3='{3}' | col4='{4}' | {5}" -f $r, $c1, $c2, $c3, $c4, $tcs)
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
