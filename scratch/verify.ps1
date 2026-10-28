$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield.xlsx")
    Write-Host "Total Sheets: $($wb.Sheets.Count)"
    for ($i = 1; $i -le [Math]::Min(6, $wb.Sheets.Count); $i++) {
        Write-Host ("Sheet $i : " + $wb.Sheets.Item($i).Name)
    }
    Write-Host "..."
    Write-Host ("Last Sheet: " + $wb.Sheets.Item($wb.Sheets.Count).Name)
    
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "Test Report B2: $($tr.Range('B2').Text)"
    
    $r12 = @()
    for ($c = 1; $c -le 9; $c++) { $r12 += $tr.Cells.Item(12, $c).Text }
    Write-Host "Row 12: $($r12 -join ' | ')"
    
    $r38 = @()
    for ($c = 1; $c -le 9; $c++) { $r38 += $tr.Cells.Item(38, $c).Text }
    Write-Host "Row 38: $($r38 -join ' | ')"

    $r39 = @()
    for ($c = 1; $c -le 9; $c++) { $r39 += $tr.Cells.Item(39, $c).Text }
    Write-Host "Row 39 (Sub total): $($r39 -join ' | ')"
    Write-Host "Row 39 Formula C39: $($tr.Range('C39').Formula)"
    Write-Host "Row 39 Formula F39: $($tr.Range('F39').Formula)"
    Write-Host "Row 39 Formula G39: $($tr.Range('G39').Formula)"
    Write-Host "Row 39 Formula H39: $($tr.Range('H39').Formula)"
    Write-Host "Row 39 Formula I39: $($tr.Range('I39').Formula)"

    Write-Host "Charts count in Test Report: $($tr.ChartObjects().Count)"
    for ($ch = 1; $ch -le $tr.ChartObjects().Count; $ch++) {
        $chartObj = $tr.ChartObjects().Item($ch)
        Write-Host "Chart $ch Top=$($chartObj.Top) Left=$($chartObj.Left) Width=$($chartObj.Width) Height=$($chartObj.Height)"
    }

    $fn1 = $wb.Sheets.Item("UC_7.5_ViewOrgVouchers")
    Write-Host "UC_7.5_ViewOrgVouchers C2: $($fn1.Range('C2').Text) | L2: $($fn1.Range('L2').Text)"
    
    $r7 = @()
    for ($c = 2; $c -le 10; $c++) { $r7 += ($fn1.Cells.Item(7, $c).Formula + "=" + $fn1.Cells.Item(7, $c).Text) }
    Write-Host "UC_7.5 Row 7: $($r7 -join ' | ')"

    $r9 = @()
    for ($c = 5; $c -le 14; $c++) { $r9 += $fn1.Cells.Item(9, $c).Text }
    Write-Host "UC_7.5 Row 9 (UTCIDs): $($r9 -join ' | ')"

    Write-Host "UC_7.5 Row 11: B11=$($fn1.Range('B11').Text) | D11=$($fn1.Range('D11').Text) | F11=$($fn1.Range('F11').Text)"
    Write-Host "UC_7.5 Row 34: B34=$($fn1.Range('B34').Text) | D34=$($fn1.Range('D34').Text) | F34=$($fn1.Range('F34').Text)"
    Write-Host "UC_7.5 Row 42: B42=$($fn1.Range('B42').Text) | F42=$($fn1.Range('F42').Text)"
    Write-Host "UC_7.5 Row 43: B43=$($fn1.Range('B43').Text) | F43=$($fn1.Range('F43').Text)"
    
    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
