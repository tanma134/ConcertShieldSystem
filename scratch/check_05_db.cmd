@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_qr_token_id, ticket_id, qr_token, expires_at, is_used FROM ticket_qr_tokens WHERE qr_token LIKE '%%9NB5TV%%' OR qr_token LIKE '%%9NB%%';"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_qr_token_id, ticket_id, qr_token, expires_at, is_used FROM ticket_qr_tokens ORDER BY ticket_qr_token_id DESC LIMIT 5;"
