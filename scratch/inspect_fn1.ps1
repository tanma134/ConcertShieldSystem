$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $fn = $wb.Sheets.Item(5)
    Write-Host "Sheet 5 Name: $($fn.Name)"
    Write-Host "UsedRange: $($fn.UsedRange.Address)"
    
    # Check borders and layout of Function1_...
    Write-Host "--- Header rows 9 to 12 ---"
    for ($r = 9; $r -le 12; $r++) {
        $vals = @()
        for ($c = 1; $c -le 25; $c++) {
            $vals += $fn.Cells.Item($r, $c).Text
        }
        Write-Host "Row $r : $($vals -join ' | ')"
    }
    
    Write-Host "--- All rows 1 to 50 in Col A to F ---"
    for ($r = 1; $r -le 50; $r++) {
        $txtA = $fn.Cells.Item($r, 1).Text
        $txtB = $fn.Cells.Item($r, 2).Text
        $txtC = $fn.Cells.Item($r, 3).Text
        $txtD = $fn.Cells.Item($r, 4).Text
        $txtE = $fn.Cells.Item($r, 5).Text
        $txtF = $fn.Cells.Item($r, 6).Text
        if ($txtA -or $txtB -or $txtC -or $txtD -or $txtE -or $txtF) {
            Write-Host "Row $r : A='$txtA' | B='$txtB' | C='$txtC' | D='$txtD' | E='$txtE' | F='$txtF'"
        }
    }
    
    Write-Host "--- Check any text in columns G to Z ---"
    for ($r = 1; $r -le 60; $r++) {
        for ($c = 7; $c -le 35; $c++) {
            $txt = $fn.Cells.Item($r, $c).Text
            if ($txt) {
                Write-Host "Row $r Col $c ($($fn.Cells.Item($r, $c).Address)): '$txt'"
            }
        }
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
