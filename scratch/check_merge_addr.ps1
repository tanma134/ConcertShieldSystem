$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn1 = $wb.Sheets.Item(5)
    for ($r = 10; $r -le 25; $r++) {
        Write-Host "Row $r : B MergeArea=$($fn1.Cells.Item($r, 2).MergeArea.Address(0,0)) | D MergeArea=$($fn1.Cells.Item($r, 4).MergeArea.Address(0,0))"
    }
    for ($r = 34; $r -le 45; $r++) {
        Write-Host "Row $r : B MergeArea=$($fn1.Cells.Item($r, 2).MergeArea.Address(0,0)) | D MergeArea=$($fn1.Cells.Item($r, 4).MergeArea.Address(0,0))"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
