$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "--- Test Report original rows 16 to 24 ---"
    for ($r = 16; $r -le 24; $r++) {
        $rowTxt = @()
        for ($c = 1; $c -le 9; $c++) {
            $cell = $tr.Cells.Item($r, $c)
            $rowTxt += "[$($cell.Address)='$($cell.Text)' Fml='$($cell.Formula)' NumFmt='$($cell.NumberFormat)']"
        }
        Write-Host "Row $r : $($rowTxt -join ' ')"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
