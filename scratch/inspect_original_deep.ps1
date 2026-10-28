$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
try {
    $wb = $excel.Workbooks.Open("D:\BEN9\Report5_Unit Test Case.xls")
    
    Write-Host "================= FUNCTIONLIST ================="
    $fl = $wb.Sheets.Item("FunctionList")
    Write-Host "FunctionList UsedRange: $($fl.UsedRange.Address)"
    for ($r = 8; $r -le 16; $r++) {
        $rowTxt = @()
        for ($c = 1; $c -le 8; $c++) {
            $cell = $fl.Cells.Item($r, $c)
            $rowTxt += "[$($cell.Address)='$($cell.Text)', Fill=$($cell.Interior.Color), Font=$($cell.Font.Name)-$($cell.Font.Size), Bold=$($cell.Font.Bold), Align=$($cell.HorizontalAlignment)]"
        }
        Write-Host "Row $r : $($rowTxt -join ' ')"
    }

    Write-Host "`n================= TEST REPORT ================="
    $tr = $wb.Sheets.Item("Test Report")
    Write-Host "Test Report UsedRange: $($tr.UsedRange.Address)"
    for ($r = 10; $r -le 24; $r++) {
        $rowTxt = @()
        for ($c = 1; $c -le 9; $c++) {
            $cell = $tr.Cells.Item($r, $c)
            $rowTxt += "[$($cell.Address)='$($cell.Text)', Fml='$($cell.Formula)', Fill=$($cell.Interior.Color), Font=$($cell.Font.Name)-$($cell.Font.Size), Bold=$($cell.Font.Bold), NumFmt='$($cell.NumberFormat)']"
        }
        Write-Host "Row $r : $($rowTxt -join ' ')"
    }

    Write-Host "`n================= FUNCTION1_ĐĂNG NHẬP ================="
    $fn1 = $wb.Sheets.Item(5)
    Write-Host "Function1 Name: $($fn1.Name)"
    Write-Host "Function1 UsedRange: $($fn1.UsedRange.Address)"
    Write-Host "--- Column Widths A to U ---"
    for ($c = 1; $c -le 21; $c++) {
        Write-Host "Col $c ($([char](64+$c))): Width=$($fn1.Columns.Item($c).ColumnWidth)"
    }
    
    Write-Host "--- Rows 9 to 46 Layout ---"
    for ($r = 9; $r -le 46; $r++) {
        $cellA = $fn1.Cells.Item($r, 1)
        $cellB = $fn1.Cells.Item($r, 2)
        $cellC = $fn1.Cells.Item($r, 3)
        $cellD = $fn1.Cells.Item($r, 4)
        $cellE = $fn1.Cells.Item($r, 5)
        $cellF = $fn1.Cells.Item($r, 6)
        
        Write-Host ("Row {0:D2} (Ht={1:N1}): A=[{2}, MA={3}, Fill={4}], B=[{5}, MA={6}], C=[{7}], D=[{8}], E=[{9}], F=[{10}, Fill={11}, TopB={12}, BtmB={13}, InB={14}]" -f `
            $r, $fn1.Rows.Item($r).RowHeight, `
            $cellA.Text, $cellA.MergeArea.Address, $cellA.Interior.Color, `
            $cellB.Text, $cellB.MergeArea.Address, `
            $cellC.Text, $cellD.Text, $cellE.Text, `
            $cellF.Text, $cellF.Interior.Color, `
            $cellF.Borders.Item(8).LineStyle, $cellF.Borders.Item(9).LineStyle, $cellF.Borders.Item(11).LineStyle)
    }

    Write-Host "`n--- Check where note 'Nhập số từ 5 - 20' is ---"
    for ($r = 30; $r -le 42; $r++) {
        for ($c = 20; $c -le 30; $c++) {
            $txt = $fn1.Cells.Item($r, $c).Text
            if ($txt) {
                Write-Host "Cell ($r, $c) [$($fn1.Cells.Item($r, $c).Address)]: '$txt'"
            }
        }
    }

    $wb.Close($false)
} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
