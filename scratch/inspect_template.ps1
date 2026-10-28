$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    Write-Host "Original Sheets:"
    for ($i = 1; $i -le $wb.Sheets.Count; $i++) {
        Write-Host $wb.Sheets.Item($i).Name
    }
    
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "--- Original Test Report rows 10 to 25 ---"
    for ($r = 10; $r -le 25; $r++) {
        $rowVals = @()
        for ($c = 1; $c -le 10; $c++) {
            $cell = $tr.Cells.Item($r, $c)
            $rowVals += ("[$($cell.Address): Text='$($cell.Text)', Fml='$($cell.Formula)', NumberFormat='$($cell.NumberFormat)']")
        }
        Write-Host ("Row $r : " + ($rowVals -join " "))
    }

    $fn = $wb.Sheets.Item("Function1_Đăng nhập")
    Write-Host "--- Original Function1_Đăng nhập used range ---"
    Write-Host "UsedRange: $($fn.UsedRange.Address)"
    Write-Host "--- Check right side of Function1 (e.g. cols T to Z, rows 10 to 45) ---"
    for ($r = 1; $r -le 50; $r++) {
        for ($c = 15; $c -le 40; $c++) {
            $txt = $fn.Cells.Item($r, $c).Text
            if ($txt) {
                Write-Host "Row $r Col $c ($($fn.Cells.Item($r, $c).Address)): $txt"
            }
        }
    }
    
    Write-Host "--- Check rows 1 to 50 col A..E in Function1 ---"
    for ($r = 9; $r -le 48; $r++) {
        $a = $fn.Cells.Item($r, 1).Text
        $b = $fn.Cells.Item($r, 2).Text
        $c = $fn.Cells.Item($r, 3).Text
        $d = $fn.Cells.Item($r, 4).Text
        $e = $fn.Cells.Item($r, 5).Text
        $f = $fn.Cells.Item($r, 6).Text
        Write-Host "Row $r : A='$a' | B='$b' | C='$c' | D='$d' | E='$e' | F='$f' | MergeArea=$($fn.Cells.Item($r, 1).MergeArea.Address)"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
