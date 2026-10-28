$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield.xlsx")
    $fl = $wb.Sheets.Item("FunctionList")
    Write-Host "FunctionList B2: $($fl.Range('B2').Text)"
    for ($r = 8; $r -le 14; $r++) {
        $rowText = @()
        for ($c = 1; $c -le 8; $c++) { $rowText += $fl.Cells.Item($r, $c).Text }
        Write-Host "Row $r : $($rowText -join ' | ')"
    }
    Write-Host "..."
    for ($r = 30; $r -le 34; $r++) {
        $rowText = @()
        for ($c = 1; $c -le 8; $c++) { $rowText += $fl.Cells.Item($r, $c).Text }
        Write-Host "Row $r : $($rowText -join ' | ')"
    }

    Write-Host "Hyperlink count on FunctionList: $($fl.Hyperlinks.Count)"
    if ($fl.Hyperlinks.Count -gt 0) {
        Write-Host "Hyperlink 1 SubAddress: $($fl.Hyperlinks.Item(1).SubAddress) | TextToDisplay: $($fl.Hyperlinks.Item(1).TextToDisplay)"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
