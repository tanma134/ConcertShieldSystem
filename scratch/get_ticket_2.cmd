@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_id, ticket_code, event_id, status, checked_in_at, ticket_type_name, owner_name FROM tickets WHERE ticket_id = 2;"
