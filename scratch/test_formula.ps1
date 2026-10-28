$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $sh = $wb.Sheets.Item(5)
    Write-Host "Current A7 Formula: $($sh.Range('A7').Formula)"
    Write-Host "Current A7 FormulaLocal: $($sh.Range('A7').FormulaLocal)"
    
    # Try assign US formula
    try {
        $sh.Range("A7").Formula = "=COUNTIF(F43:O43, ""P"")"
        Write-Host "Assign with comma succeeded!"
    } catch {
        Write-Host "Assign with comma failed: $_"
    }

    # Try assign FormulaLocal with semicolon
    try {
        $sh.Range("A7").FormulaLocal = "=COUNTIF(F43:O43; ""P"")"
        Write-Host "Assign FormulaLocal with semicolon succeeded!"
    } catch {
        Write-Host "Assign FormulaLocal with semicolon failed: $_"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
