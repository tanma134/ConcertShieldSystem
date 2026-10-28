@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ALTER COLUMN ticket_id DROP NOT NULL;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ALTER COLUMN ticket_code TYPE text USING ticket_code::text;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ADD COLUMN IF NOT EXISTS customer_name character varying(255);"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ADD COLUMN IF NOT EXISTS zone_name character varying(100);"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ADD COLUMN IF NOT EXISTS gate_name character varying(100);"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.checkin_logs ADD COLUMN IF NOT EXISTS scan_method character varying(50);"
