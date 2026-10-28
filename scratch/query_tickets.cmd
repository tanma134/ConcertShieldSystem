@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d ticket_db -c "SELECT ticket_id, ticket_code, event_id, status, is_checked_in FROM tickets ORDER BY ticket_id DESC LIMIT 10;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d ticket_db -c "SELECT ticket_qr_token_id, ticket_id, qr_token, expires_at, is_used, is_revoked FROM ticket_qr_tokens ORDER BY ticket_qr_token_id DESC LIMIT 10;"
