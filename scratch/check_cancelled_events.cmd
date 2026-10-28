@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "SELECT event_id, title, status FROM events WHERE status ILIKE 'Cancelled' OR status ILIKE 'Postponed';"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT event_id, count(*) as total_tickets, count(case when status ILIKE 'Active' or status ILIKE 'Available' or status ILIKE 'Issued' then 1 end) as active_tickets FROM tickets GROUP BY event_id;"
