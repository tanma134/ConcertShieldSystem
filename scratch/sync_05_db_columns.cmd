@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS is_checked_in boolean DEFAULT false;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS gate_id character varying(50) DEFAULT 'GATE_A';"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "UPDATE public.tickets SET is_checked_in = true WHERE checked_in_at IS NOT NULL;"
