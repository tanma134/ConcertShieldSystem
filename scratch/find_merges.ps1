$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn1 = $wb.Sheets.Item(5)
    Write-Host "--- Merged cells in rows 10 to 45 ---"
    for ($r = 10; $r -le 45; $r++) {
        for ($c = 1; $c -le 20; $c++) {
            $cell = $fn1.Cells.Item($r, $c)
            if ($cell.MergeCells) {
                if ($cell.Address -eq $cell.MergeArea.Item(1,1).Address) {
                    Write-Host "Top-left of merge: $($cell.MergeArea.Address)"
                }
            }
        }
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
