$baseDir = "D:\BEN9\3\ConcertShieldSystem"
$brainDir = "C:\Users\USER\.gemini\antigravity\brain\32e387b4-0cfa-4625-b46d-31a9d86280e6"

# Copy v3 over v2
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xlsx" "$baseDir\Report5_Unit Test Case_ConcertShield_v2.xlsx" -Force
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xls" "$baseDir\Report5_Unit Test Case_ConcertShield_v2.xls" -Force
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xlsx" "$brainDir\Report5_Unit Test Case_ConcertShield_v2.xlsx" -Force
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xls" "$brainDir\Report5_Unit Test Case_ConcertShield_v2.xls" -Force

# Try copy over base files if unlocked
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xlsx" "$baseDir\Report5_Unit Test Case_ConcertShield.xlsx" -Force -ErrorAction SilentlyContinue
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xls" "$baseDir\Report5_Unit Test Case_ConcertShield.xls" -Force -ErrorAction SilentlyContinue
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xlsx" "$brainDir\Report5_Unit Test Case_ConcertShield.xlsx" -Force -ErrorAction SilentlyContinue
Copy-Item "$baseDir\Report5_Unit Test Case_ConcertShield_v3.xls" "$brainDir\Report5_Unit Test Case_ConcertShield.xls" -Force -ErrorAction SilentlyContinue

Write-Host "All file copies synchronized successfully."
