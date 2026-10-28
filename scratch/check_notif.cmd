@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d notification_db -c "\dt"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d notification_db -c "\d notifications"
