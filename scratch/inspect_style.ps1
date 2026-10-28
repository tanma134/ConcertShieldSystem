$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn = $wb.Sheets.Item(5) # Function1
    Write-Host "--- Inspecting Sidebar cells in Function1 ---"
    foreach ($r in @(10, 34, 42)) {
        $c = $fn.Cells.Item($r, 1)
        Write-Host "Row $r : Text='$($c.Text)' | MergeArea=$($c.MergeArea.Address) | Orientation=$($c.Orientation) | FontColor=$($c.Font.Color) | Bold=$($c.Font.Bold) | InteriorColor=$($c.Interior.Color) | HorizAlign=$($c.HorizontalAlignment) | VertAlign=$($c.VerticalAlignment)"
    }
    
    Write-Host "--- Inspecting Header row 9 in Function1 ---"
    for ($col = 1; $col -le 10; $col++) {
        $c = $fn.Cells.Item(9, $col)
        Write-Host "Col $col ($($c.Address)): Text='$($c.Text)' | Orientation=$($c.Orientation) | FontColor=$($c.Font.Color) | InteriorColor=$($c.Interior.Color)"
    }

    Write-Host "--- Inspecting Row 7 formulas in Function1 ---"
    for ($col = 1; $col -le 16; $col++) {
        $c = $fn.Cells.Item(7, $col)
        if ($c.Formula) {
            Write-Host "Col $col ($($c.Address)): Formula='$($c.Formula)' | Text='$($c.Text)'"
        }
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
