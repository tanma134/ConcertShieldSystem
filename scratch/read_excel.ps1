try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $wb = $excel.Workbooks.Open('D:\BEN9\Report5_Unit Test Case.xls')
    Write-Host "Excel COM opened successfully! Sheets in Report5_Unit Test Case.xls:"
    for ($i = 1; $i -le $wb.Sheets.Count; $i++) {
        $sheet = $wb.Sheets.Item($i)
        Write-Host " Sheet $i : $($sheet.Name)"
    }
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
