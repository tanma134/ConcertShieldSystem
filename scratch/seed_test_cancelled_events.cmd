@echo off
set PGPASSWORD=123456
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "UPDATE events SET status = 'Cancelled' WHERE event_id = 1;"
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "UPDATE events SET status = 'Postponed' WHERE event_id = 16;"

"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "INSERT INTO orders (customer_id, event_id, full_name, email, phone, event_name, total_amount, discount_amount, final_amount, payment_method, status, is_deleted) VALUES (2, 16, 'Test Customer', 'customer@ticketbox.com', '0987654321', 'AMEE - E11EVEN Tour 2027', 2000000, 0, 2000000, 'VNPay', 'Paid', false);"

"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "INSERT INTO tickets (ticket_code, order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, is_deleted) VALUES (gen_random_uuid(), (SELECT MAX(order_id) FROM orders WHERE event_id = 16), 16, 38, 2, 'Standard Area', 'Test Customer', 'Active', false), (gen_random_uuid(), (SELECT MAX(order_id) FROM orders WHERE event_id = 16), 16, 38, 2, 'Standard Area', 'Test Customer', 'Active', false);"
