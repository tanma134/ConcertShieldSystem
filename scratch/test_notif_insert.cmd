@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d notification_db -c "INSERT INTO notifications (user_id, title, content, message, type, category, target_url, is_read, created_at, is_deleted) VALUES (2, 'Concert Update Test', 'Concert has been cancelled test notification', 'Concert has been cancelled test notification', 'event_cancelled', 'ticket_return', '/my-tickets', false, NOW(), false); SELECT * FROM notifications ORDER BY notification_id DESC LIMIT 1;"
