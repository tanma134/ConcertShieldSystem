$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')
$tr = $wb.Sheets.Item('Test Report')

Write-Host "Charts in Test Report:"
for ($i = 1; $i -le $tr.ChartObjects().Count; $i++) {
    $co = $tr.ChartObjects().Item($i)
    Write-Host ("Chart {0}: Name='{1}', Top={2}, Left={3}, Width={4}, Height={5}" -f $i, $co.Name, $co.Top, $co.Left, $co.Width, $co.Height)
    $ch = $co.Chart
    Write-Host ("  Title='{0}'" -f $ch.ChartTitle.Text)
    for ($s = 1; $s -le $ch.SeriesCollection().Count; $s++) {
        $ser = $ch.SeriesCollection().Item($s)
        Write-Host ("  Series {0}: Formula='{1}'" -f $s, $ser.Formula)
    }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
