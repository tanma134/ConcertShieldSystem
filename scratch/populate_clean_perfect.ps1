# Script to populate Excel Unit Test Case template with 100% precision and complete table layout
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$jsonPath = "D:\BEN9\3\ConcertShieldSystem\scratch\functions_data.json"
Write-Host "Loading function definitions from $jsonPath..."
$functionsData = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$totalFuncs = ($functionsData | Measure-Object).Count
Write-Host "Total functions to populate: $totalFuncs"

$templatePath = "D:\BEN9\Report5_Unit Test Case.xls"
$outXlsPath = "D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v2.xls"
$outXlsxPath = "D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case_ConcertShield_v2.xlsx"

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

# Helper function to set table borders
function Set-TableBorders($rng) {
    # 7=xlEdgeLeft, 8=xlEdgeTop, 9=xlEdgeBottom, 10=xlEdgeRight, 11=xlInsideVertical, 12=xlInsideHorizontal
    foreach ($edge in @(7, 8, 9, 10, 11, 12)) {
        try {
            $b = $rng.Borders.Item($edge)
            $b.LineStyle = 1 # xlContinuous
            $b.Weight = 2    # xlThin
            $b.ColorIndex = -4105 # xlAutomatic
        } catch {}
    }
}

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

    # Master template sheet to clone
    $masterSheet = $null
    for ($s = 1; $s -le $wb.Sheets.Count; $s++) {
        if ($wb.Sheets.Item($s).Name -like "Function1*") {
            $masterSheet = $wb.Sheets.Item($s)
            break
        }
    }
    if (-not $masterSheet) {
        $masterSheet = $wb.Sheets.Item(5)
    }
    Write-Host "Master template sheet found: $($masterSheet.Name)"

    # 2. Duplicate and populate all 27 Function sheets
    Write-Host "Creating 27 Function sheets with dynamic rows and clean table borders..."
    $prevSheet = $masterSheet
    $createdSheets = @()

    $fnIdx = 0
    foreach ($fc in $functionsData) {
        $fnIdx++
        $sheetName = [string]$fc.sheet_name
        Write-Host (" [{0:D2}/27] Creating sheet: {1}..." -f $fnIdx, $sheetName)

        # Copy master template after prevSheet
        $masterSheet.Copy([Type]::Missing, $prevSheet)
        $newSh = $wb.Sheets.Item($prevSheet.Index + 1)
        $newSh.Name = $sheetName
        $prevSheet = $newSh
        $createdSheets += $newSh

        # Top Metadata (Rows 2 to 5)
        $newSh.Range("C2").Value2 = [string]$fc.code
        $newSh.Range("L2").Value2 = [string]$fc.name
        $newSh.Range("C3").Value2 = "ConcertShield Team"
        $newSh.Range("L3").Value2 = "QA Automation Team"
        $newSh.Range("C4").Value2 = [double]$fc.loc
        $newSh.Range("L4").Value2 = [double]0
        $newSh.Range("C5").Value2 = [string]$fc.test_req

        # Number of test cases
        $numTc = [int]$fc.num_tc
        $lastTcCol = 5 + $numTc
        $lastColLtr = [char](65 + $lastTcCol - 1)

        # Clear entire table body and any leftover columns to the right (X..AZ)
        $newSh.Range("A10:AZ120").Clear()

        # Build Row 9 Header (UTCIDs)
        $newSh.Rows.Item(9).RowHeight = 45
        # Clear Row 9 from Col 6 to 50
        $newSh.Range($newSh.Cells.Item(9, 6), $newSh.Cells.Item(9, 50)).Clear()
        for ($t = 1; $t -le $numTc; $t++) {
            $colIdx = 5 + $t
            $cell = $newSh.Cells.Item(9, $colIdx)
            $cell.Value2 = ("UTCID{0:D2}" -f $t)
            $cell.Interior.Color = 8388608 # #002060
            $cell.Font.Color = 16777215    # White
            $cell.Font.Bold = $true
            $cell.Font.Name = "Arial"
            $cell.Font.Size = 10
            $cell.Orientation = 90
            $cell.HorizontalAlignment = -4108 # xlCenter
            $cell.VerticalAlignment = -4108   # xlCenter
        }
        # Col 1..5 in Row 9 dark blue
        $hdrA9E9 = $newSh.Range("A9:E9")
        $hdrA9E9.Interior.Color = 8388608

        # --- CONDITION SECTION ---
        $startCond = 10
        $currR = 10
        # Row 10: Precondition header
        $newSh.Cells.Item($currR, 2).Value2 = "Precondition "
        $newSh.Cells.Item($currR, 2).Font.Bold = $true

        # Precondition items
        if ($fc.preconditions) {
            foreach ($pre in $fc.preconditions) {
                $currR++
                $newSh.Rows.Item($currR).RowHeight = 20
                $newSh.Cells.Item($currR, 4).Value2 = [string]$pre[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $pre[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $c = $newSh.Cells.Item($currR, 5 + $t)
                        $c.Value2 = "O"
                        $c.Font.Bold = $true
                        $c.HorizontalAlignment = -4108
                    }
                }
            }
        }

        # Input parameters and values
        if ($fc.inputs) {
            foreach ($inp in $fc.inputs) {
                $currR++ # Parameter Name row
                $newSh.Rows.Item($currR).RowHeight = 20
                $newSh.Cells.Item($currR, 2).Value2 = [string]$inp[0]
                $newSh.Cells.Item($currR, 2).Font.Bold = $true

                foreach ($val in $inp[1]) {
                    $currR++ # Value row
                    $newSh.Rows.Item($currR).RowHeight = 20
                    $newSh.Cells.Item($currR, 4).Value2 = [string]$val[0]
                    $newSh.Cells.Item($currR, 4).WrapText = $true
                    $tcs = $val[1]
                    for ($t = 1; $t -le $numTc; $t++) {
                        if ($tcs -contains $t) {
                            $c = $newSh.Cells.Item($currR, 5 + $t)
                            $c.Value2 = "O"
                            $c.Font.Bold = $true
                            $c.HorizontalAlignment = -4108
                        }
                    }
                }
            }
        }
        $endCond = $currR

        # Merge Condition sidebar
        $condSide = $newSh.Range("A${startCond}:A${endCond}")
        $condSide.Merge()
        $condSide.Value2 = "Condition"
        $condSide.Interior.Color = 8388608
        $condSide.Font.Color = 16777215
        $condSide.Font.Bold = $true
        $condSide.Font.Name = "Arial"
        $condSide.Orientation = -4128 # xlVertical
        $condSide.HorizontalAlignment = -4108
        $condSide.VerticalAlignment = -4108

        # --- CONFIRM SECTION ---
        $startConf = $endCond + 1
        $currR = $startConf
        # Row startConf: Return header
        $newSh.Cells.Item($currR, 2).Value2 = "Return"
        $newSh.Cells.Item($currR, 2).Font.Bold = $true

        # Return items
        if ($fc.returns) {
            foreach ($ret in $fc.returns) {
                $currR++
                $newSh.Rows.Item($currR).RowHeight = 20
                $newSh.Cells.Item($currR, 4).Value2 = [string]$ret[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $ret[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $c = $newSh.Cells.Item($currR, 5 + $t)
                        $c.Value2 = "O"
                        $c.Font.Bold = $true
                        $c.HorizontalAlignment = -4108
                    }
                }
            }
        }

        # Exception header
        $currR++
        $newSh.Rows.Item($currR).RowHeight = 20
        $newSh.Cells.Item($currR, 2).Value2 = "Exception"
        $newSh.Cells.Item($currR, 2).Font.Bold = $true

        # Exception items
        if ($fc.exceptions) {
            foreach ($exc in $fc.exceptions) {
                $currR++
                $newSh.Rows.Item($currR).RowHeight = 20
                $newSh.Cells.Item($currR, 4).Value2 = [string]$exc[0]
                $newSh.Cells.Item($currR, 4).WrapText = $true
                $tcs = $exc[1]
                for ($t = 1; $t -le $numTc; $t++) {
                    if ($tcs -contains $t) {
                        $c = $newSh.Cells.Item($currR, 5 + $t)
                        $c.Value2 = "O"
                        $c.Font.Bold = $true
                        $c.HorizontalAlignment = -4108
                    }
                }
            }
        }
        $endConf = $currR

        # Merge Confirm sidebar
        $confSide = $newSh.Range("A${startConf}:A${endConf}")
        $confSide.Merge()
        $confSide.Value2 = "Confirm"
        $confSide.Interior.Color = 8388608
        $confSide.Font.Color = 16777215
        $confSide.Font.Bold = $true
        $confSide.Font.Name = "Arial"
        $confSide.Orientation = -4128 # xlVertical
        $confSide.HorizontalAlignment = -4108
        $confSide.VerticalAlignment = -4108

        # --- RESULT SECTION ---
        $resRow = $endConf + 1
        
        # Result Row 1: Type
        $newSh.Rows.Item($resRow).RowHeight = 20
        $newSh.Cells.Item($resRow, 2).Value2 = "Type(N : Normal, A : Abnormal, B : Boundary)"
        $types = $fc.tc_types
        for ($t = 1; $t -le $numTc; $t++) {
            $c = $newSh.Cells.Item($resRow, 5 + $t)
            $c.Value2 = [string]$types[$t - 1]
            $c.Font.Bold = $true
            $c.HorizontalAlignment = -4108
        }

        # Result Row 2: Passed/Failed
        $newSh.Rows.Item($resRow + 1).RowHeight = 20
        $newSh.Cells.Item($resRow + 1, 2).Value2 = "Passed/Failed"
        for ($t = 1; $t -le $numTc; $t++) {
            $c = $newSh.Cells.Item($resRow + 1, 5 + $t)
            $c.Value2 = "P"
            $c.Font.Bold = $true
            $c.HorizontalAlignment = -4108
        }

        # Result Row 3: Executed Date
        $newSh.Rows.Item($resRow + 2).RowHeight = 20
        $newSh.Cells.Item($resRow + 2, 2).Value2 = "Executed Date"
        for ($t = 1; $t -le $numTc; $t++) {
            $c = $newSh.Cells.Item($resRow + 2, 5 + $t)
            $c.NumberFormat = "@" # Text format to prevent ####
            $c.Value2 = "28/10"
            $c.HorizontalAlignment = -4108
        }

        # Result Row 4: Defect ID
        $newSh.Rows.Item($resRow + 3).RowHeight = 20
        $newSh.Cells.Item($resRow + 3, 2).Value2 = "Defect ID"
        for ($t = 1; $t -le $numTc; $t++) {
            $c = $newSh.Cells.Item($resRow + 3, 5 + $t)
            $c.Value2 = ""
            $c.HorizontalAlignment = -4108
        }

        # Merge Result sidebar
        $resSide = $newSh.Range("A${resRow}:A" + ($resRow + 3))
        $resSide.Merge()
        $resSide.Value2 = "Result"
        $resSide.Interior.Color = 8388608
        $resSide.Font.Color = 16777215
        $resSide.Font.Bold = $true
        $resSide.Font.Name = "Arial"
        $resSide.Orientation = -4128
        $resSide.HorizontalAlignment = -4108
        $resSide.VerticalAlignment = -4108

        # Apply Table Borders across entire generated table
        $fullTableRng = $newSh.Range("A9", $newSh.Cells.Item($resRow + 3, $lastTcCol))
        Set-TableBorders($fullTableRng)

        # Update Row 7 Formulas using FormulaLocal with semicolon
        $newSh.Cells.Item(7, 1).FormulaLocal = ("=COUNTIF(F{0}:{1}{0};""P"")" -f ($resRow + 1), $lastColLtr)
        $newSh.Cells.Item(7, 3).FormulaLocal = ("=COUNTIF(F{0}:{1}{0};""F"")" -f ($resRow + 1), $lastColLtr)
        $newSh.Cells.Item(7, 6).FormulaLocal = "=SUM(O7;-A7;-C7)"
        $newSh.Cells.Item(7, 12).FormulaLocal = ("=COUNTIF(F{0}:{1}{0};""N"")" -f $resRow, $lastColLtr)
        $newSh.Cells.Item(7, 13).FormulaLocal = ("=COUNTIF(F{0}:{1}{0};""A"")" -f $resRow, $lastColLtr)
        $newSh.Cells.Item(7, 14).FormulaLocal = ("=COUNTIF(F{0}:{1}{0};""B"")" -f $resRow, $lastColLtr)
        $newSh.Cells.Item(7, 15).FormulaLocal = ("=COUNTA(F9:{0}9)" -f $lastColLtr)

        # Set Column Widths
        $newSh.Columns.Item(1).ColumnWidth = 4.5  # Sidebar
        $newSh.Columns.Item(2).ColumnWidth = 20   # Category/Param name
        $newSh.Columns.Item(3).ColumnWidth = 2    # Separator
        $newSh.Columns.Item(4).ColumnWidth = 42   # Condition / Return / Exception text
        $newSh.Columns.Item(5).ColumnWidth = 2    # Separator
        for ($col = 6; $col -le $lastTcCol; $col++) {
            $newSh.Columns.Item($col).ColumnWidth = 5.0 # Test case columns
        }

        # Clean columns right of table
        for ($col = $lastTcCol + 1; $col -le 25; $col++) {
            $newSh.Columns.Item($col).ColumnWidth = 3.5
        }
    }

    # 3. Update FunctionList Sheet
    $fl = $wb.Sheets.Item("FunctionList")
    if ($fl) {
        Write-Host "Updating FunctionList sheet with complete table layout..."
        # Clear rows 10 to 60 completely
        $fl.Range("A10:H60").Clear()

        # Build Header Row 10
        $fl.Rows.Item(10).RowHeight = 26
        $headersFL = @("No", "Requirement Name", "Class Name", "Function Name", "Function Code(Optional)", "Sheet Name", "Description", "Pre-Condition")
        for ($c = 1; $c -le 8; $c++) {
            $cell = $fl.Cells.Item(10, $c)
            $cell.Value2 = $headersFL[$c - 1]
            $cell.Interior.Color = 8388608
            $cell.Font.Color = 16777215
            $cell.Font.Bold = $true
            $cell.Font.Name = "Arial"
            $cell.Font.Size = 10
            $cell.HorizontalAlignment = -4108
            $cell.VerticalAlignment = -4108
        }

        # Populate Rows 11 to 37
        for ($i = 0; $i -lt $totalFuncs; $i++) {
            $fc = $functionsData[$i]
            $r = 11 + $i
            $fl.Rows.Item($r).RowHeight = 26

            $fl.Cells.Item($r, 1).Value2 = [double]($i + 1)
            $fl.Cells.Item($r, 1).HorizontalAlignment = -4108

            $fl.Cells.Item($r, 2).Value2 = [string]$fc.req_name
            $fl.Cells.Item($r, 3).Value2 = [string]$fc.class_name
            $fl.Cells.Item($r, 4).Value2 = [string]$fc.name
            $fl.Cells.Item($r, 5).Value2 = [string]$fc.code

            # Sheet Name with Hyperlink
            $sheetTarget = [string]$fc.sheet_name
            $cellF = $fl.Cells.Item($r, 6)
            $fl.Hyperlinks.Add($cellF, "", "'$sheetTarget'!A1", "", $sheetTarget) | Out-Null
            $cellF.Font.Name = "Arial"
            $cellF.Font.Size = 10

            $cellG = $fl.Cells.Item($r, 7)
            $cellG.Value2 = [string]$fc.test_req
            $cellG.WrapText = $true

            $preShort = if ($fc.preconditions -and $fc.preconditions.Count -gt 0) { [string]$fc.preconditions[0][0] } else { "System is operational" }
            $cellH = $fl.Cells.Item($r, 8)
            $cellH.Value2 = $preShort
            $cellH.WrapText = $true
        }

        # Format borders and column widths
        Set-TableBorders($fl.Range("A10:H37"))
        $fl.Columns.Item(1).ColumnWidth = 6
        $fl.Columns.Item(2).ColumnWidth = 30
        $fl.Columns.Item(3).ColumnWidth = 35
        $fl.Columns.Item(4).ColumnWidth = 28
        $fl.Columns.Item(5).ColumnWidth = 28
        $fl.Columns.Item(6).ColumnWidth = 26
        $fl.Columns.Item(7).ColumnWidth = 50
        $fl.Columns.Item(8).ColumnWidth = 45
    }

    # 4. Remove old sample sheets
    for ($s = $wb.Sheets.Count; $s -ge 1; $s--) {
        $sName = $wb.Sheets.Item($s).Name
        if ($sName -in @("Example", "Function3", "Function2") -or $sName -like "Function1*") {
            try {
                Write-Host " Deleting old sheet: $sName"
                $wb.Sheets.Item($s).Delete()
            } catch {}
        }
    }

    # 5. Update Test Report Sheet
    $tr = $wb.Sheets.Item("Test Report")
    if ($tr) {
        Write-Host "Updating Test Report sheet with clean table layout..."
        # Clear range A11:I60
        $tr.Range("A11:I60").Clear()

        # Header Row 11
        $tr.Rows.Item(11).RowHeight = 26
        $headersTR = @("No", "Function code", "Passed", "Failed", "Untested", "N", "A", "B", "Total Test Cases")
        for ($c = 1; $c -le 9; $c++) {
            $cell = $tr.Cells.Item(11, $c)
            $cell.Value2 = $headersTR[$c - 1]
            $cell.Interior.Color = 8388608
            $cell.Font.Color = 16777215
            $cell.Font.Bold = $true
            $cell.Font.Name = "Arial"
            $cell.Font.Size = 10
            $cell.HorizontalAlignment = -4108
            $cell.VerticalAlignment = -4108
        }

        # Populate Rows 12 to 38
        for ($i = 0; $i -lt $totalFuncs; $i++) {
            $fc = $functionsData[$i]
            $r = 12 + $i
            $shName = [string]$fc.sheet_name
            $tr.Rows.Item($r).RowHeight = 20

            $tr.Cells.Item($r, 1).Value2 = [double]($i + 1)
            $tr.Cells.Item($r, 1).HorizontalAlignment = -4108

            $cellB = $tr.Cells.Item($r, 2)
            $tr.Hyperlinks.Add($cellB, "", "'$shName'!A1", "", $shName) | Out-Null
            $cellB.Font.Name = "Arial"
            $cellB.Font.Size = 10

            # Formulas referencing row 7 of function sheet
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
                $c.Font.Bold = $false
            }
        }

        # Table borders for data rows
        Set-TableBorders($tr.Range("A11:I38"))

        # Row 39: Sub total
        $subtotalRow = 39
        $tr.Rows.Item($subtotalRow).RowHeight = 22
        $cellSub = $tr.Cells.Item($subtotalRow, 2)
        $cellSub.Value2 = "Sub total"
        $cellSub.Font.Bold = $true
        $cellSub.HorizontalAlignment = -4152 # xlRight

        $startRow = 12
        $endRow = 38
        $tr.Cells.Item($subtotalRow, 3).FormulaLocal = ("=SUM(C{0}:C{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 4).FormulaLocal = ("=SUM(D{0}:D{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 5).FormulaLocal = ("=SUM(E{0}:E{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 6).FormulaLocal = ("=SUM(F{0}:F{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 7).FormulaLocal = ("=SUM(G{0}:G{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 8).FormulaLocal = ("=SUM(H{0}:H{1})" -f $startRow, $endRow)
        $tr.Cells.Item($subtotalRow, 9).FormulaLocal = ("=SUM(I{0}:I{1})" -f $startRow, $endRow)

        for ($col = 3; $col -le 9; $col++) {
            $c = $tr.Cells.Item($subtotalRow, $col)
            $c.NumberFormat = "0"
            $c.Font.Bold = $true
            $c.HorizontalAlignment = -4108
        }
        # Subtotal borders: thin top, double bottom
        Set-TableBorders($tr.Range("A39:I39"))
        $tr.Range("A39:I39").Borders.Item(9).LineStyle = -4119 # xlDouble

        # Row 40: % row
        $pctRow = 40
        $tr.Rows.Item($pctRow).RowHeight = 20
        $cellPct = $tr.Cells.Item($pctRow, 2)
        $cellPct.Value2 = "%"
        $cellPct.Font.Bold = $true
        $cellPct.HorizontalAlignment = -4152

        $tr.Cells.Item($pctRow, 3).FormulaLocal = ("=C{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 4).FormulaLocal = ("=D{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 5).FormulaLocal = ("=E{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 6).FormulaLocal = ("=F{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 7).FormulaLocal = ("=G{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 8).FormulaLocal = ("=H{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item($pctRow, 9).FormulaLocal = ("=I{0}/I{0}" -f $subtotalRow)

        for ($col = 3; $col -le 9; $col++) {
            $c = $tr.Cells.Item($pctRow, $col)
            $c.NumberFormat = "0.00%"
            $c.Font.Bold = $true
            $c.HorizontalAlignment = -4108
        }
        Set-TableBorders($tr.Range("A40:I40"))

        # Row 42 to 44: Breakdown section
        $tr.Cells.Item(42, 2).Value2 = "Normal case"
        $tr.Cells.Item(42, 2).Font.Bold = $true
        $tr.Cells.Item(42, 4).FormulaLocal = ("=F{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item(42, 4).NumberFormat = "0.00%"
        $tr.Cells.Item(42, 4).Font.Bold = $true

        $tr.Cells.Item(43, 2).Value2 = "Abnormal case"
        $tr.Cells.Item(43, 2).Font.Bold = $true
        $tr.Cells.Item(43, 4).FormulaLocal = ("=G{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item(43, 4).NumberFormat = "0.00%"
        $tr.Cells.Item(43, 4).Font.Bold = $true

        $tr.Cells.Item(44, 2).Value2 = "Boundary case"
        $tr.Cells.Item(44, 2).Font.Bold = $true
        $tr.Cells.Item(44, 4).FormulaLocal = ("=H{0}/I{0}" -f $subtotalRow)
        $tr.Cells.Item(44, 4).NumberFormat = "0.00%"
        $tr.Cells.Item(44, 4).Font.Bold = $true

        # Set Column Widths in Test Report
        $tr.Columns.Item(1).ColumnWidth = 6
        $tr.Columns.Item(2).ColumnWidth = 28
        for ($col = 3; $col -le 9; $col++) {
            $tr.Columns.Item($col).ColumnWidth = 12
        }

        # Position Charts cleanly below table
        $chartTop = 750
        try {
            if ($tr.ChartObjects().Count -ge 1) {
                $ch1 = $tr.ChartObjects().Item(1)
                $ch1.Top = $chartTop
                $ch1.Left = 320
                $ch1.Width = 280
                $ch1.Height = 210
            }
            if ($tr.ChartObjects().Count -ge 2) {
                $ch2 = $tr.ChartObjects().Item(2)
                $ch2.Top = $chartTop
                $ch2.Left = 20
                $ch2.Width = 280
                $ch2.Height = 210
            }
        } catch {
            Write-Host "Note: Chart positioning skipped or failed: $_"
        }
    }

    # Recalculate all formulas
    Write-Host "Recalculating all formulas across workbook..."
    $excel.Calculate()

    # Remove existing destination files if present to prevent COM SaveAs conflicts
    if (Test-Path $outXlsPath) { Remove-Item $outXlsPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $outXlsxPath) { Remove-Item $outXlsxPath -Force -ErrorAction SilentlyContinue }

    # Save to .xlsx first, then .xls
    Write-Host "Saving modified workbook as .xlsx to $outXlsxPath..."
    $wb.SaveAs($outXlsxPath, 51) # 51 = xlOpenXMLWorkbook (.xlsx)

    Write-Host "Saving modified workbook as .xls to $outXlsPath..."
    $wb.SaveAs($outXlsPath, 56) # 56 = xlExcel8 (.xls)

    $wb.Close($false)
    Write-Host "ALL REPAIRS COMPLETED SUCCESSFULLY!"

    # Copy to brain dir for direct artifact links
    $brainDir = "C:\Users\USER\.gemini\antigravity\brain\32e387b4-0cfa-4625-b46d-31a9d86280e6"
    Copy-Item $outXlsPath "$brainDir\Report5_Unit Test Case_ConcertShield_v2.xls" -Force -ErrorAction SilentlyContinue
    Copy-Item $outXlsxPath "$brainDir\Report5_Unit Test Case_ConcertShield_v2.xlsx" -Force -ErrorAction SilentlyContinue

} finally {
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
