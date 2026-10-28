$body = '{"eventId":16,"reasonType":"EVENT_POSTPONED","reasonNote":"Doi lich bieu dien sang thang sau theo quyet dinh cua ban to chuc","triggerRefundQueue":true,"notifyAttendees":true}'

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5288/api/checkin/governance/bulk-revoke" -Method Post -Body $body -ContentType "application/json; charset=utf-8"
    $response | ConvertTo-Json -Depth 5
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Error Body:" $reader.ReadToEnd()
}
