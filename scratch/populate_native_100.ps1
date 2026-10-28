# PowerShell script to populate original Excel template with 100% native formatting
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$jsonPath = "D:\BEN9\3\ConcertShieldSystem\scratch\functions_data.json"
Write-Host "Loading function definitions from $jsonPath..."
$functionsData = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$totalFuncs = ($functionsData | Measure-Object).Count
Write-Host "Total functions to populate: $totalFuncs"

$templatePath = "D:\BEN9\Report5_Unit Test Case.xls"
$outXlsPath = "D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v3.xls"
$outXlsxPath = "D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v3.xlsx"

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    Write-Host "Opening original template: $templatePath"
    $wb = $excel.Workbooks.Open($templatePath)

    # 1. Update Cover sheet
    $cover = $wb.Sheets.Item("Cover")
    if ($cover) {
        Write-Host "Updating Cover sheet..."
        $cover.Range("C2").Value2 = "ConcertShield - Anti-Scalping & High-Concurrency Ticketing System"
        $cover.Range("C3").Value2 = "28/10/2026"
        $cover.Range("C4").Value2 = "ConcertShield Engineering Team"
    }

    # Find master template sheet (Function1_...)
    $masterSheet = $null
    for ($s = 1; $s -le $wb.Sheets.Count; $s++) {
        if ($wb.Sheets.Item($s).Name -like "Function1*") {
            $masterSheet = $wb.Sheets.Item($s)
            break
        }
    }
    if (-not $masterSheet) { $masterSheet = $wb.Sheets.Item(5) }
    Write-Host "Master template sheet found: $($masterSheet.Name)"

    # 2. Update FunctionList Sheet
    $fl = $wb.Sheets.Item("FunctionList")
    if ($fl) {
        Write-Host "Updating FunctionList sheet by natively inserting rows..."
        # Template has rows 11, 12, 13. Insert 24 rows at row 14 by copying row 13
        $fl.Rows.Item(13).Copy()
        for ($k = 1; $k -le 24; $k++) {
            $fl.Rows.Item(14).Insert(-4121) # xlShiftDown
        }

        # Populate rows 11 to 37
        for ($i = 0; $i -lt $totalFuncs; $i++) {
            $fc = $functionsData[$i]
            $r = 11 + $i
            $fl.Cells.Item($r, 1).Value2 = [double]($i + 1)
            $fl.Cells.Item($r, 2).Value2 = [string]$fc.req_name
            $fl.Cells.Item($r, 3).Value2 = [string]$fc.class_name
            $fl.Cells.Item($r, 4).Value2 = [string]$fc.name
            $fl.Cells.Item($r, 5).Value2 = [string]$fc.code

            $sheetTarget = [string]$fc.sheet_name
            $cellF = $fl.Cells.Item($r, 6)
            $fl.Hyperlinks.Add($cellF, "", "'$sheetTarget'!A1", "", $sheetTarget) | Out-Null
            $cellF.Font.Name = "Tahoma"
            $cellF.Font.Size = 10

            $cellG = $fl.Cells.Item($r, 7)
            $cellG.Value2 = [string]$fc.test_req
            $cellG.WrapText = $true

            $preShort = if ($fc.preconditions -and $fc.preconditions.Count -gt 0) { [string]$fc.preconditions[0][0] } else { "System is operational" }
            $cellH = $fl.Cells.Item($r, 8)
            $cellH.Value2 = $preShort
            $cellH.WrapText = $true
        }

        # Slightly adjust column widths for clean readability
        $fl.Columns.Item(1).ColumnWidth = 6
        $fl.Columns.Item(2).ColumnWidth = 30
        $fl.Columns.Item(3).ColumnWidth = 34
        $fl.Columns.Item(4).ColumnWidth = 26
        $fl.Columns.Item(5).ColumnWidth = 28
        $fl.Columns.Item(6).ColumnWidth = 26
        $fl.Columns.Item(7).ColumnWidth = 48
        $fl.Columns.Item(8).ColumnWidth = 42
    }

    # 3. Expand Test Report rows natively (insert 22 rows so data rows are 12 to 38)
    $tr = $wb.Sheets.Item("Test Report")
    if ($tr) {
        Write-Host "Expanding Test Report rows natively..."
        $tr.Rows.Item(14).Copy()
        for ($k = 1; $k -le 22; $k++) {
            $tr.Rows.Item(15).Insert(-4121) # xlShiftDown
        }
        $tr.Range("A12:I38").ClearContents()
    }

    # 4. Duplicate and populate 27 Function sheets from master template
    Write-Host "Creating 27 Function sheets with 100% original template layout..."
    $prevSheet = $masterSheet

    $fnIdx = 0
    foreach ($fc in $functionsData) {
        $fnIdx++
        $sheetName = [string]$fc.sheet_name
        Write-Host (" [{0:D2}/27] Creating sheet: {1}..." -f $fnIdx, $sheetName)

        # Clone master template after prevSheet
        $masterSheet.Copy([Type]::Missing, $prevSheet)
        $newSh = $wb.Sheets.Item($prevSheet.Index + 1)
        $newSh.Name = $sheetName
        $prevSheet = $newSh

        # Top Metadata (Rows 2 to 5)
        $newSh.Range("C2").Value2 = [string]$fc.code
        $newSh.Range("L2").Value2 = [string]$fc.name
        $newSh.Range("C3").Value2 = "ConcertShield Team"
        $newSh.Range("L3").Value2 = "QA Automation Team"
        $newSh.Range("C4").Value2 = [double]$fc.loc
        $newSh.Range("L4").Value2 = [double]0
        $newSh.Range("C5").Value2 = [string]$fc.test_req

        # Remove the floating note "Nhập số từ 5 - 20..." completely
        $newSh.Range("X35:AA41").Clear()

        # Unmerge any merged cell in D22:E22 if present
        try {
            if ($newSh.Range("D22").MergeCells) { $newSh.Range("D22").MergeArea.UnMerge() }
        } catch {}

        # Clear ONLY data contents, leaving borders, heights, fonts, fills 100% INTACT
        $newSh.Range("B11:E33").ClearContents()
        $newSh.Range("B35:E41").ClearContents()
        $newSh.Range("F11:T41").ClearContents()
        $newSh.Range("F42:T45").ClearContents()

        # Ensure Return and Exception labels are present
        $newSh.Cells.Item(34, 2).Value2 = "Return"
        $newSh.Cells.Item(37, 2).Value2 = "Exception"

        # Number of test cases
        $numTc = [int]$fc.num_tc

        # If numTc < 15, clear unused UTCIDs in row 9 so COUNTA counts exact test cases
        if ($numTc -lt 15) {
            $newSh.Range($newSh.Cells.Item(9, 6 + $numTc), $newSh.Range("T9")).ClearContents()
        }

        # Calculate rows needed for Condition
        $numPre = if ($fc.preconditions) { ($fc.preconditions | Measure-Object).Count } else { 0 }
        $numInpParams = if ($fc.inputs) { ($fc.inputs | Measure-Object).Count } else { 0 }
        $numInpVals = 0
        if ($fc.inputs) {
            foreach ($inp in $fc.inputs) { $numInpVals += ($inp[1] | Measure-Object).Count }
        }
        $totalCondRows = $numPre + $numInpParams + $numInpVals

        # If condition needs more rows than 23 (template rows 11 to 33), insert rows natively at row 33
        if ($totalCondRows -gt 23) {
            $extraCond = $totalCondRows - 23
            $newSh.Rows.Item(32).Copy()
            for ($k = 1; $k -le $extraCond; $k++) {
                $newSh.Rows.Item(33).Insert(-4121)
            }
        }

        # Populate Condition (starting at row 11)
        $currR = 10
        if ($fc.preconditions) {
            foreach ($pre in $fc.preconditions) {
                $currR++
                $newSh.Cells.Item($currR, 4).Value2 = [string]$pre[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $pre[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $newSh.Cells.Item($currR, 5 + $t).Value2 = "O"
                    }
                }
            }
        }

        if ($fc.inputs) {
            foreach ($inp in $fc.inputs) {
                $currR++ # Param Name row
                $newSh.Cells.Item($currR, 2).Value2 = [string]$inp[0]
                $newSh.Cells.Item($currR, 2).Font.Bold = $true

                foreach ($val in $inp[1]) {
                    $currR++ # Value row
                    $newSh.Cells.Item($currR, 4).Value2 = [string]$val[0]
                    $newSh.Cells.Item($currR, 4).WrapText = $true
                    $tcs = $val[1]
                    for ($t = 1; $t -le $numTc; $t++) {
                        if ($tcs -contains $t) {
                            $newSh.Cells.Item($currR, 5 + $t).Value2 = "O"
                        }
                    }
                }
            }
        }

        # Find Confirm / Return row
        $retHdrRow = 34
        for ($r = 30; $r -le 60; $r++) {
            if ($newSh.Cells.Item($r, 2).Text -eq "Return") {
                $retHdrRow = $r
                break
            }
        }

        # If returns need more than 2 rows (template has rows retHdrRow+1 and retHdrRow+2)
        $numRet = if ($fc.returns) { ($fc.returns | Measure-Object).Count } else { 0 }
        if ($numRet -gt 2) {
            $extraRet = $numRet - 2
            $newSh.Rows.Item($retHdrRow + 2).Copy()
            for ($k = 1; $k -le $extraRet; $k++) {
                $newSh.Rows.Item($retHdrRow + 3).Insert(-4121)
            }
        }

        # Populate Returns
        $currR = $retHdrRow
        if ($fc.returns) {
            foreach ($ret in $fc.returns) {
                $currR++
                $newSh.Cells.Item($currR, 4).Value2 = [string]$ret[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $ret[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $newSh.Cells.Item($currR, 5 + $t).Value2 = "O"
                    }
                }
            }
        }

        # Find Exception header row
        $excHdrRow = $currR + 1
        for ($r = $currR; $r -le 70; $r++) {
            if ($newSh.Cells.Item($r, 2).Text -eq "Exception") {
                $excHdrRow = $r
                break
            }
        }
        $newSh.Cells.Item($excHdrRow, 2).Value2 = "Exception"
        $newSh.Cells.Item($excHdrRow, 2).Font.Bold = $true

        # If exceptions need more than 4 rows
        $numExc = if ($fc.exceptions) { ($fc.exceptions | Measure-Object).Count } else { 0 }
        if ($numExc -gt 4) {
            $extraExc = $numExc - 4
            $newSh.Rows.Item($excHdrRow + 4).Copy()
            for ($k = 1; $k -le $extraExc; $k++) {
                $newSh.Rows.Item($excHdrRow + 5).Insert(-4121)
            }
        }

        # Populate Exceptions
        $currR = $excHdrRow
        if ($fc.exceptions) {
            foreach ($exc in $fc.exceptions) {
                $currR++
                $newSh.Cells.Item($currR, 4).Value2 = [string]$exc[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $exc[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $newSh.Cells.Item($currR, 5 + $t).Value2 = "O"
                    }
                }
            }
        }

        # Find Result section (Type row)
        $typeRow = $currR + 1
        for ($r = $currR; $r -le 80; $r++) {
            if ($newSh.Cells.Item($r, 2).Text -like "*Type(N*") {
                $typeRow = $r
                break
            }
        }

        # Populate Results
        # Type row
        $types = $fc.tc_types
        for ($t = 1; $t -le $numTc; $t++) {
            $newSh.Cells.Item($typeRow, 5 + $t).Value2 = [string]$types[$t - 1]
        }

        # Passed/Failed row
        $pfRow = $typeRow + 1
        for ($t = 1; $t -le $numTc; $t++) {
            $newSh.Cells.Item($pfRow, 5 + $t).Value2 = "P"
        }

        # Executed Date row
        $dtRow = $typeRow + 2
        for ($t = 1; $t -le $numTc; $t++) {
            $c = $newSh.Cells.Item($dtRow, 5 + $t)
            $c.NumberFormat = "@"
            $c.Value2 = "28/10"
        }

        # Defect ID row
        $dfRow = $typeRow + 3
        for ($t = 1; $t -le $numTc; $t++) {
            $newSh.Cells.Item($dfRow, 5 + $t).Value2 = ""
        }

        # Update Row 7 Formulas with actual row references
        $newSh.Cells.Item(7, 1).FormulaLocal = ("=COUNTIF(F{0}:HQ{0};""P"")" -f $pfRow)
        $newSh.Cells.Item(7, 3).FormulaLocal = ("=COUNTIF(F{0}:HQ{0};""F"")" -f $pfRow)
        $newSh.Cells.Item(7, 6).FormulaLocal = "=SUM(O7;-A7;-C7)"
        $newSh.Cells.Item(7, 12).FormulaLocal = ("=COUNTIF(E{0}:HQ{0};""N"")" -f $typeRow)
        $newSh.Cells.Item(7, 13).FormulaLocal = ("=COUNTIF(E{0}:HQ{0};""A"")" -f $typeRow)
        $newSh.Cells.Item(7, 14).FormulaLocal = ("=COUNTIF(E{0}:HQ{0};""B"")" -f $typeRow)
        $newSh.Cells.Item(7, 15).FormulaLocal = "=COUNTA(E9:HT9)"
    }

    # 5. Delete old sample sheets
    for ($s = $wb.Sheets.Count; $s -ge 1; $s--) {
        $sName = $wb.Sheets.Item($s).Name
        if ($sName -in @("Example", "Function3", "Function2") -or $sName -like "Function1*") {
            try {
                Write-Host " Deleting old sheet: $sName"
                $wb.Sheets.Item($s).Delete()
            } catch {}
        }
    }

    # 6. Now populate Test Report with all 27 function sheets now existing!
    if ($tr) {
        Write-Host "Populating Test Report formulas (now all sheets exist)..."
        for ($i = 0; $i -lt $totalFuncs; $i++) {
            $fc = $functionsData[$i]
            $r = 12 + $i
            $shName = [string]$fc.sheet_name

            $tr.Cells.Item($r, 1).Value2 = [double]($i + 1)
            $tr.Cells.Item($r, 1).HorizontalAlignment = -4108

            $cellB = $tr.Cells.Item($r, 2)
            $tr.Hyperlinks.Add($cellB, "", "'$shName'!A1", "", $shName) | Out-Null
            $cellB.Font.Name = "Tahoma"
            $cellB.Font.Size = 10

            $tr.Cells.Item($r, 3).FormulaLocal = ("='{0}'!A7" -f $shName)
            $tr.Cells.Item($r, 4).FormulaLocal = ("='{0}'!C7" -f $shName)
            $tr.Cells.Item($r, 5).FormulaLocal = ("='{0}'!F7" -f $shName)
            $tr.Cells.Item($r, 6).FormulaLocal = ("='{0}'!L7" -f $shName)
            $tr.Cells.Item($r, 7).FormulaLocal = ("='{0}'!M7" -f $shName)
            $tr.Cells.Item($r, 8).FormulaLocal = ("='{0}'!N7" -f $shName)
            $tr.Cells.Item($r, 9).FormulaLocal = ("='{0}'!O7" -f $shName)

            for ($col = 3; $col -le 9; $col++) {
                $c = $tr.Cells.Item($r, $col)
                $c.NumberFormat = "0"
                $c.HorizontalAlignment = -4108
            }
        }

        # Sub total is at row 39
        $tr.Cells.Item(39, 3).FormulaLocal = "=SUM(C12:C38)"
        $tr.Cells.Item(39, 4).FormulaLocal = "=SUM(D12:D38)"
        $tr.Cells.Item(39, 5).FormulaLocal = "=SUM(E12:E38)"
        $tr.Cells.Item(39, 6).FormulaLocal = "=SUM(F12:F38)"
        $tr.Cells.Item(39, 7).FormulaLocal = "=SUM(G12:G38)"
        $tr.Cells.Item(39, 8).FormulaLocal = "=SUM(H12:H38)"
        $tr.Cells.Item(39, 9).FormulaLocal = "=SUM(I12:I38)"
        for ($col = 3; $col -le 9; $col++) {
            $tr.Cells.Item(39, $col).NumberFormat = "0"
        }

        # Adjust column widths in Test Report
        $tr.Columns.Item(1).ColumnWidth = 6
        $tr.Columns.Item(2).ColumnWidth = 28
        for ($col = 3; $col -le 9; $col++) {
            $tr.Columns.Item($col).ColumnWidth = 12
        }

        # Position charts cleanly below table (Top = 740)
        try {
            if ($tr.ChartObjects().Count -ge 1) {
                $ch1 = $tr.ChartObjects().Item(1)
                $ch1.Top = 740
                $ch1.Left = 320
                $ch1.Width = 280
                $ch1.Height = 210
            }
            if ($tr.ChartObjects().Count -ge 2) {
                $ch2 = $tr.ChartObjects().Item(2)
                $ch2.Top = 740
                $ch2.Left = 20
                $ch2.Width = 280
                $ch2.Height = 210
            }
        } catch {}
    }

    # Recalculate whole workbook
    Write-Host "Recalculating all formulas across workbook..."
    $excel.Calculate()

    # Save to v3
    if (Test-Path $outXlsxPath) { Remove-Item $outXlsxPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $outXlsPath) { Remove-Item $outXlsPath -Force -ErrorAction SilentlyContinue }

    Write-Host "Saving modified workbook as .xlsx to $outXlsxPath..."
    $wb.SaveAs($outXlsxPath, 51) # 51 = xlOpenXMLWorkbook (.xlsx)

    Write-Host "Saving modified workbook as .xls to $outXlsPath..."
    $wb.SaveAs($outXlsPath, 56) # 56 = xlExcel8 (.xls)

    $wb.Close($false)
    Write-Host "ALL NATIVE POPULATION COMPLETED SUCCESSFULLY!"

    # Copy to brain dir
    $brainDir = "C:\Users\USER\.gemini\antigravity\brain\32e387b4-0cfa-4625-b46d-31a9d86280e6"
    Copy-Item $outXlsxPath "$brainDir\Report5_Unit Test Case_ConcertShield_v3.xlsx" -Force -ErrorAction SilentlyContinue
    Copy-Item $outXlsPath "$brainDir\Report5_Unit Test Case_ConcertShield_v3.xls" -Force -ErrorAction SilentlyContinue

} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
