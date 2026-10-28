@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT event_id, count(*), count(case when status = 'Active' then 1 end) as active from tickets where is_deleted = false group by event_id;"
