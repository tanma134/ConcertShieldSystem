$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open('D:\BEN9\3\ConcertShieldSystem\Report5_Unit Test Case.xls')

try {
    $template = $wb.Sheets.Item(5) # Function1_Đăng nhập
    Write-Host "Template sheet name: $($template.Name)"

    $template.Copy([Type]::Missing, $wb.Sheets.Item($wb.Sheets.Count))
    $newSheet = $wb.Sheets.Item($wb.Sheets.Count)
    $newSheet.Name = "UC_7.5_Test"
    Write-Host "New sheet created: $($newSheet.Name)"

    # Clear matrix contents
    $newSheet.Range("B11:T33").ClearContents()
    $newSheet.Range("B35:T41").ClearContents()
    $newSheet.Range("F42:T45").ClearContents()
    Write-Host "ClearContents succeeded."

    # Set some cells
    $newSheet.Cells.Item(11, 4).Value = "Precondition 1"
    $newSheet.Cells.Item(11, 6).Value = "O"
    $newSheet.Cells.Item(42, 6).Value = "N"
    $newSheet.Cells.Item(43, 6).Value = "P"
    $newSheet.Cells.Item(44, 6).Value = "28/10/2026"
    Write-Host "Set cells succeeded."

    $newSheet.Calculate()
    Write-Host "Calculated: A7=$($newSheet.Cells.Item(7, 1).Text), Total=$($newSheet.Cells.Item(7, 15).Text)"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
