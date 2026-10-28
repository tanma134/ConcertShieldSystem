$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield.xlsx")
    $fn = $wb.Sheets.Item("UC_7.5_ViewOrgVouchers")
    Write-Host "--- Inspecting UC_7.5_ViewOrgVouchers rows 9 to 46 ---"
    for ($r = 9; $r -le 46; $r++) {
        $a = $fn.Cells.Item($r, 1).Text
        $b = $fn.Cells.Item($r, 2).Text
        $d = $fn.Cells.Item($r, 4).Text
        $f = $fn.Cells.Item($r, 6).Text
        $g = $fn.Cells.Item($r, 7).Text
        $t = $fn.Cells.Item($r, 20).Text
        Write-Host "Row $r : A='$a' | B='$b' | D='$d' | F='$f' | G='$g' | T='$t'"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
