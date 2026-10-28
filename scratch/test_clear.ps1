$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn1 = $wb.Sheets.Item(5)
    
    $fn1.Range("B11:E33").ClearContents()
    $fn1.Range("B35:E41").ClearContents()
    $fn1.Range("F11:T41").ClearContents()
    $fn1.Range("F42:T45").ClearContents()
    $fn1.Range("X35:AA41").Clear()
    Write-Host "ClearContents succeeded with ZERO errors!"

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
