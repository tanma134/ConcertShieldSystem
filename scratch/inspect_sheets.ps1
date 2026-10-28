$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')

Write-Host "Inspecting Sheet 5 (Function1_Đăng nhập):"
$s5 = $wb.Sheets.Item(5)
for ($r = 1; $r -le 10; $r++) {
    $rowText = ""
    for ($c = 1; $c -le 10; $c++) {
        $val = $s5.Cells.Item($r, $c).Text
        if ($val -ne "") { $rowText += "[$r,$c]=$val  " }
    }
    if ($rowText -ne "") { Write-Host $rowText }
}

Write-Host "`nInspecting Sheet 8 (Example):"
$s8 = $wb.Sheets.Item(8)
for ($r = 1; $r -le 10; $r++) {
    $rowText = ""
    for ($c = 1; $c -le 10; $c++) {
        $val = $s8.Cells.Item($r, $c).Text
        if ($val -ne "") { $rowText += "[$r,$c]=$val  " }
    }
    if ($rowText -ne "") { Write-Host $rowText }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
