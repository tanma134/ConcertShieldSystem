@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "UPDATE ticket_qr_tokens SET expires_at = NOW() + INTERVAL '1 hour', is_used = false WHERE ticket_qr_token_id = 2;"
