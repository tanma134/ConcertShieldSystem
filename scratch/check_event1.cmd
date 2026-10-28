@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_id, ticket_code, order_id, event_id, status, is_deleted FROM tickets WHERE event_id = 1;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "SELECT event_id, title, status FROM events WHERE event_id = 1;"
