$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    $sh = $wb.Sheets.Item(5)

    $tests = @(
        @{ Name="A7"; Formula='=COUNTIF(F43:O43;"P")' },
        @{ Name="C7"; Formula='=COUNTIF(F43:O43;"F")' },
        @{ Name="F7"; Formula='=SUM(O7;-A7;-C7)' },
        @{ Name="L7"; Formula='=COUNTIF(F42:O42;"N")' },
        @{ Name="M7"; Formula='=COUNTIF(F42:O42;"A")' },
        @{ Name="N7"; Formula='=COUNTIF(F42:O42;"B")' },
        @{ Name="O7"; Formula='=COUNTA(F9:O9)' }
    )

    foreach ($t in $tests) {
        try {
            $sh.Range($t.Name).FormulaLocal = $t.Formula
            Write-Host "$($t.Name) SUCCESS: $($sh.Range($t.Name).Text)"
        } catch {
            Write-Host "$($t.Name) FAILED: $_"
        }
    }

    # Now test Test Report formulas
    $tr = $wb.Sheets.Item("Test Report")
    $trTests = @(
        @{ Cell="C12"; Formula="='Function1_Đăng nhập'!A7" },
        @{ Cell="C39"; Formula="=SUM(C12:C38)" },
        @{ Cell="C40"; Formula="=C39/I39" },
        @{ Cell="D42"; Formula="=F39/I39" }
    )
    foreach ($t in $trTests) {
        try {
            $tr.Range($t.Cell).FormulaLocal = $t.Formula
            Write-Host "TR $($t.Cell) SUCCESS: $($tr.Range($t.Cell).Text)"
        } catch {
            Write-Host "TR $($t.Cell) FAILED: $_"
        }
    }

    $excel.Calculate()
    Write-Host "Calculate SUCCESS"

    $tmpXls = "D:\BEN9\3\ConcertShieldSystem\scratch\test_save.xls"
    $tmpXlsx = "D:\BEN9\3\ConcertShieldSystem\scratch\test_save.xlsx"
    if (Test-Path $tmpXls) { Remove-Item $tmpXls -Force }
    if (Test-Path $tmpXlsx) { Remove-Item $tmpXlsx -Force }
    
    try {
        $wb.SaveAs($tmpXlsx, 51)
        Write-Host "SaveAs XLSX SUCCESS"
    } catch {
        Write-Host "SaveAs XLSX FAILED: $_"
    }

    try {
        $wb.SaveAs($tmpXls, 56)
        Write-Host "SaveAs XLS SUCCESS"
    } catch {
        Write-Host "SaveAs XLS FAILED: $_"
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
