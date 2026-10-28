@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "UPDATE tickets SET checked_in_at = NULL, checked_in_by = NULL, is_checked_in = false, status = 'Active' WHERE ticket_id = 2;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "UPDATE ticket_qr_tokens SET is_used = false, expires_at = NOW() + INTERVAL '24 hours' WHERE ticket_id = 2;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "DELETE FROM checkin_logs WHERE ticket_id = 2 OR ticket_code = 'c9a3203a-251e-4d95-9b82-4d23d0397e8c';"
