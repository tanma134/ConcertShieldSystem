@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "SELECT event_id, title, status FROM events ORDER BY event_id ASC;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_id, ticket_code, order_id, event_id, status, is_deleted, owner_name FROM tickets;"
