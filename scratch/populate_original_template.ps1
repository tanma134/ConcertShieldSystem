$ErrorActionPreference = "Stop"

$baseDir = "D:\BEN9\3\ConcertShieldSystem"
$templateFile = "$baseDir\Report5_Unit Test Case.xls"
$jsonFile = "$baseDir\scratch\functions_data.json"

Write-Host "Loading function definitions from $jsonFile..."
$jsonContent = Get-Content -Path $jsonFile -Raw -Encoding UTF8
$functions = $jsonContent | ConvertFrom-Json

Write-Host "Total functions to populate: $($functions.Count)"

Write-Host "Starting Excel COM Application..."
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $wb = $excel.Workbooks.Open($templateFile)
    Write-Host "Workbook opened successfully."

    # 1. Update Cover Sheet
    Write-Host "Updating Cover sheet..."
    $cover = $wb.Sheets.Item("Cover")
    $cover.Cells.Item(4, 2).Value2 = "ConcertShield System"
    $cover.Cells.Item(5, 2).Value2 = "CSS"
    $cover.Cells.Item(6, 2).Value2 = "CSS_UnitTest_v1.0"
    $cover.Cells.Item(4, 6).Value2 = "Developer"
    $cover.Cells.Item(5, 6).Value2 = "Technical Lead / QA Lead"
    $cover.Cells.Item(6, 6).Value2 = "28/10/2026"
    $cover.Cells.Item(12, 1).Value2 = "28/10/2026"
    $cover.Cells.Item(12, 2).Value2 = "1.0"
    $cover.Cells.Item(12, 3).Value2 = "All 27 Use Cases"
    $cover.Cells.Item(12, 4).Value2 = "A"
    $cover.Cells.Item(12, 5).Value2 = "Initial creation of complete Unit Test Case matrix for 27 functions"
    $cover.Cells.Item(12, 6).Value2 = "Architecture & SRS Specification"

    # 2. Find Master Template Sheet (Function1 or Example)
    $masterTemplate = $null
    for ($s = 1; $s -le $wb.Sheets.Count; $s++) {
        $shName = $wb.Sheets.Item($s).Name
        if ($shName -like "Function1*" -or $shName -eq "Example") {
            $masterTemplate = $wb.Sheets.Item($s)
            break
        }
    }
    if ($null -eq $masterTemplate) {
        throw "Master template sheet not found!"
    }
    Write-Host "Using master template sheet: $($masterTemplate.Name)"

    # 3. Duplicate and Populate 27 Function Sheets
    Write-Host "Duplicating and populating 27 function sheets from master template..."

    for ($i = 0; $i -lt $functions.Count; $i++) {
        $fc = $functions[$i]
        Write-Host (" - [{0:D2}/27] Creating sheet: {1}..." -f ($i + 1), $fc.sheet_name)

        # Copy master template after last sheet
        $masterTemplate.Copy([Type]::Missing, $wb.Sheets.Item($wb.Sheets.Count))
        $sh = $wb.Sheets.Item($wb.Sheets.Count)
        $sh.Name = $fc.sheet_name

        # Header info
        $sh.Cells.Item(2, 3).Value2 = [string]$fc.code
        $sh.Cells.Item(2, 12).Value2 = [string]$fc.name
        $sh.Cells.Item(3, 3).Value2 = "Developer"
        $sh.Cells.Item(3, 12).Value2 = "QA Tester"
        $sh.Cells.Item(4, 3).Value2 = [double]$fc.loc
        $sh.Cells.Item(5, 3).Value2 = [string]$fc.test_req

        # Clear existing data rows
        $sh.Range("F9:T9").ClearContents()
        $sh.Range("B11:T33").ClearContents()
        $sh.Range("B35:T41").ClearContents()
        $sh.Range("F42:T45").ClearContents()

        # Set UTCID columns (F9..T9)
        $numTc = $fc.num_tc
        for ($t = 0; $t -lt $numTc; $t++) {
            $col = 6 + $t
            $sh.Cells.Item(9, $col).Value2 = ("UTCID{0:D2}" -f ($t + 1))
        }

        # Populate Condition (Rows 11 to 33)
        $currR = 11

        # Preconditions
        if ($fc.preconditions) {
            foreach ($pre in $fc.preconditions) {
                if ($currR -gt 33) { break }
                $preText = [string]$pre[0]
                $tcs = $pre[1]
                $sh.Cells.Item($currR, 4).Value2 = $preText
                for ($t = 0; $t -lt $numTc; $t++) {
                    if ($tcs -contains ($t + 1)) {
                        $sh.Cells.Item($currR, 6 + $t).Value2 = "O"
                    }
                }
                $currR++
            }
        }

        # Inputs
        if ($fc.inputs) {
            foreach ($inp in $fc.inputs) {
                if ($currR -gt 33) { break }
                $pName = [string]$inp[0]
                $valList = $inp[1]

                # Param name row
                $sh.Cells.Item($currR, 2).Value2 = $pName
                $currR++

                foreach ($vItem in $valList) {
                    if ($currR -gt 33) { break }
                    $vText = [string]$vItem[0]
                    $tcs = $vItem[1]
                    $sh.Cells.Item($currR, 4).Value2 = $vText
                    for ($t = 0; $t -lt $numTc; $t++) {
                        if ($tcs -contains ($t + 1)) {
                            $sh.Cells.Item($currR, 6 + $t).Value2 = "O"
                        }
                    }
                    $currR++
                }
            }
        }

        # Populate Confirm (Rows 34 to 41)
        # Returns start at row 35
        $retR = 35
        if ($fc.returns) {
            foreach ($ret in $fc.returns) {
                if ($retR -ge 37) { break }
                $retText = [string]$ret[0]
                $tcs = $ret[1]
                $sh.Cells.Item($retR, 4).Value2 = $retText
                for ($t = 0; $t -lt $numTc; $t++) {
                    if ($tcs -contains ($t + 1)) {
                        $sh.Cells.Item($retR, 6 + $t).Value2 = "O"
                    }
                }
                $retR++
            }
        }

        # Exceptions start at row 38
        $excR = 38
        if ($fc.exceptions) {
            foreach ($exc in $fc.exceptions) {
                if ($excR -ge 41) { break }
                $excText = [string]$exc[0]
                $tcs = $exc[1]
                $sh.Cells.Item($excR, 4).Value2 = $excText
                for ($t = 0; $t -lt $numTc; $t++) {
                    if ($tcs -contains ($t + 1)) {
                        $sh.Cells.Item($excR, 6 + $t).Value2 = "O"
                    }
                }
                $excR++
            }
        }

        # Populate Results (Rows 42 to 45)
        # Row 42: Type
        for ($t = 0; $t -lt $numTc; $t++) {
            $sh.Cells.Item(42, 6 + $t).Value2 = [string]$fc.tc_types[$t]
        }
        # Row 43: Passed/Failed
        for ($t = 0; $t -lt $numTc; $t++) {
            $sh.Cells.Item(43, 6 + $t).Value2 = [string]$fc.tc_status[$t]
        }
        # Row 44: Executed Date
        for ($t = 0; $t -lt $numTc; $t++) {
            $sh.Cells.Item(44, 6 + $t).Value2 = "28/10/2026"
        }
        # Row 45: Defect ID
        if ($fc.defect_ids) {
            for ($t = 0; $t -lt $numTc; $t++) {
                $sh.Cells.Item(45, 6 + $t).Value2 = [string]$fc.defect_ids[$t]
            }
        }

        # Force calculate sheet formulas
        $sh.Calculate()
    }

    # 4. Update FunctionList Sheet
    Write-Host "Updating FunctionList sheet..."
    $fl = $wb.Sheets.Item("FunctionList")
    $fl.Cells.Item(4, 4).Value2 = "ConcertShield System"
    $fl.Cells.Item(5, 4).Value2 = "CSS"
    $fl.Cells.Item(6, 4).Value2 = [double]100
    $fl.Cells.Item(7, 4).Value2 = "1. Runtime: .NET 8.0 SDK (C# 12) | 2. xUnit 2.5+, Moq 4.20+, FluentAssertions | 3. PostgreSQL 16 & EF Core InMemoryDatabase | 4. WebApplicationFactory"

    # Clear existing rows in FunctionList (from row 11 downwards)
    $fl.Range("A11:H45").ClearContents()

    for ($i = 0; $i -lt $functions.Count; $i++) {
        $fc = $functions[$i]
        $r = 11 + $i
        $fl.Cells.Item($r, 1).Value2 = [double]($i + 1)
        $fl.Cells.Item($r, 2).Value2 = [string]$fc.req_name
        $fl.Cells.Item($r, 3).Value2 = [string]$fc.class_name
        $fl.Cells.Item($r, 4).Value2 = [string]$fc.name
        $fl.Cells.Item($r, 5).Value2 = [string]$fc.code
        
        # Hyperlink to sheet
        $flCell = $fl.Cells.Item($r, 6)
        $fl.Hyperlinks.Add($flCell, "", "'$($fc.sheet_name)'!A1", "", [string]$fc.sheet_name) | Out-Null
        
        $fl.Cells.Item($r, 7).Value2 = [string]$fc.test_req
        $precond = ""
        if ($fc.preconditions -and $fc.preconditions.Count -gt 0) {
            $precond = [string]$fc.preconditions[0][0]
        }
        $fl.Cells.Item($r, 8).Value2 = $precond

        # Format borders
        $fl.Range("A$r:H$r").Borders.LineStyle = 1
    }

    # 5. Delete Old Sample Sheets
    Write-Host "Removing old sample sheets..."
    for ($s = $wb.Sheets.Count; $s -ge 1; $s--) {
        $sName = $wb.Sheets.Item($s).Name
        if ($sName -like "Function1*" -or $sName -eq "Function2" -or $sName -eq "Function3" -or $sName -eq "Example") {
            Write-Host " Deleting old sheet: $sName"
            $wb.Sheets.Item($s).Delete()
        }
    }

    # 6. Update Test Report Sheet
    Write-Host "Updating Test Report sheet with all 27 functions..."
    $tr = $wb.Sheets.Item("Test Report")

    # Clear existing rows 12-25
    $tr.Range("A12:I25").ClearContents()

    $startRow = 12
    for ($i = 0; $i -lt $functions.Count; $i++) {
        $fc = $functions[$i]
        $r = $startRow + $i

        $tr.Cells.Item($r, 1).Value2 = [double]($i + 1)
        
        # Hyperlink to function sheet
        $cellB = $tr.Cells.Item($r, 2)
        $tr.Hyperlinks.Add($cellB, "", "'$($fc.sheet_name)'!A1", "", [string]$fc.sheet_name) | Out-Null
        
        # Link formulas to function sheet row 7
        $tr.Cells.Item($r, 3).Formula = "='$($fc.sheet_name)'!A7"
        $tr.Cells.Item($r, 4).Formula = "='$($fc.sheet_name)'!C7"
        $tr.Cells.Item($r, 5).Formula = "='$($fc.sheet_name)'!F7"
        $tr.Cells.Item($r, 6).Formula = "='$($fc.sheet_name)'!L7"
        $tr.Cells.Item($r, 7).Formula = "='$($fc.sheet_name)'!M7"
        $tr.Cells.Item($r, 8).Formula = "='$($fc.sheet_name)'!N7"
        $tr.Cells.Item($r, 9).Formula = "='$($fc.sheet_name)'!O7"

        # Apply borders
        $tr.Range("A$r:I$r").Borders.LineStyle = 1
    }

    $endRow = $startRow + $functions.Count - 1
    $subtotalRow = $endRow + 1

    # Sub total row
    Write-Host "Setting Sub total row at row $subtotalRow..."
    $tr.Cells.Item($subtotalRow, 2).Value2 = "Sub total"
    $tr.Cells.Item($subtotalRow, 3).Formula = ("=SUM(C{0}:C{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 4).Formula = ("=SUM(D{0}:D{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 5).Formula = ("=SUM(E{0}:E{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 6).Formula = ("=SUM(F{0}:F{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 7).Formula = ("=SUM(G{0}:G{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 8).Formula = ("=SUM(H{0}:H{1})" -f $startRow, $endRow)
    $tr.Cells.Item($subtotalRow, 9).Formula = ("=SUM(I{0}:I{1})" -f $startRow, $endRow)

    # Style Sub total row: Navy Blue (#002060), Bold White
    $subRange = $tr.Range("A$subtotalRow:I$subtotalRow")
    $subRange.Interior.Color = 0x602000 # BGR format: 0x602000 is RGB(0, 32, 96) = #002060
    $subRange.Font.Color = 0xFFFFFF
    $subRange.Font.Bold = $true
    $subRange.Borders.LineStyle = 1

    # Summary Statistics
    $covRow = $subtotalRow + 2
    $tr.Cells.Item($covRow, 2).Value2 = "Test coverage"
    $tr.Cells.Item($covRow, 4).Formula = "=(C$subtotalRow+D$subtotalRow)*100/I$subtotalRow"
    $tr.Cells.Item($covRow, 5).Value2 = "%"

    $succRow = $covRow + 1
    $tr.Cells.Item($succRow, 2).Value2 = "Test successful coverage"
    $tr.Cells.Item($succRow, 4).Formula = "=C$subtotalRow*100/I$subtotalRow"
    $tr.Cells.Item($succRow, 5).Value2 = "%"

    $normRow = $succRow + 1
    $tr.Cells.Item($normRow, 2).Value2 = "Normal case"
    $tr.Cells.Item($normRow, 4).Formula = "=F$subtotalRow*100/I$subtotalRow"
    $tr.Cells.Item($normRow, 5).Value2 = "%"

    $abnRow = $normRow + 1
    $tr.Cells.Item($abnRow, 2).Value2 = "Abnormal case"
    $tr.Cells.Item($abnRow, 4).Formula = "=G$subtotalRow*100/I$subtotalRow"
    $tr.Cells.Item($abnRow, 5).Value2 = "%"

    $bndRow = $abnRow + 1
    $tr.Cells.Item($bndRow, 2).Value2 = "Boundary case"
    $tr.Cells.Item($bndRow, 4).Formula = "=H$subtotalRow*100/I$subtotalRow"
    $tr.Cells.Item($bndRow, 5).Value2 = "%"

    for ($r = $covRow; $r -le $bndRow; $r++) {
        $tr.Cells.Item($r, 2).Font.Bold = $true
        $tr.Cells.Item($r, 4).Font.Bold = $true
        $tr.Cells.Item($r, 4).NumberFormat = "0.00"
    }

    # Reposition Pie Charts below the statistics
    $chartTop = $tr.Rows.Item($bndRow + 3).Top
    Write-Host "Positioning charts at Top = $chartTop..."
    
    if ($tr.ChartObjects().Count -ge 1) {
        $ch1 = $tr.ChartObjects().Item(1)
        $ch1.Top = $chartTop
        $ch1.Left = 330
        try {
            $ch1.Chart.SeriesCollection(1).Values = ("='Test Report'!`$F`${0}:`$H`${0}" -f $subtotalRow)
            $ch1.Chart.SeriesCollection(1).XValues = "='Test Report'!`$F`$11:`$H`$11"
        } catch { Write-Host "Chart 1 series update notice: $($_.Exception.Message)" }
    }
    if ($tr.ChartObjects().Count -ge 2) {
        $ch2 = $tr.ChartObjects().Item(2)
        $ch2.Top = $chartTop
        $ch2.Left = 10
        try {
            $ch2.Chart.SeriesCollection(1).Values = ("='Test Report'!`$C`${0}:`$E`${0}" -f $subtotalRow)
            $ch2.Chart.SeriesCollection(1).XValues = "='Test Report'!`$C`$11:`$E`$11"
        } catch { Write-Host "Chart 2 series update notice: $($_.Exception.Message)" }
    }

    # Recalculate whole workbook
    $excel.Calculate()

    # Save to original template copy
    $outXls = "$baseDir\Report5_Unit Test Case_ConcertShield.xls"
    $outXlsx = "$baseDir\Report5_Unit Test Case_ConcertShield.xlsx"

    Write-Host "Saving modified workbook as .xls to $outXls..."
    $wb.SaveAs($outXls, 56) # 56 = xlExcel8 (.xls)

    Write-Host "Saving modified workbook as .xlsx to $outXlsx..."
    $wb.SaveAs($outXlsx, 51) # 51 = xlOpenXMLWorkbook (.xlsx)

    $wb.Close($false)
    Write-Host "ALL TASKS COMPLETED SUCCESSFULLY!"
} catch {
    Write-Host "Error occurred: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
} finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}
