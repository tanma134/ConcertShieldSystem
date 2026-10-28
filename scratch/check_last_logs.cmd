@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT checkin_id, ticket_code, status, failure_reason, scan_time FROM checkin_logs ORDER BY checkin_id DESC LIMIT 3;"
