@echo off
set PGPASSWORD=123456
echo ===================== TICKET RECONCILIATIONS =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT reconciliation_id, event_id, total_tickets_affected, reason, refund_queue_status, executed_at FROM ticket_reconciliations;"
echo ===================== TICKETS STATUS =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_id, ticket_code, event_id, status, is_deleted, deleted_at FROM tickets WHERE event_id = 18;"
echo ===================== TICKET REVOCATIONS =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT revocation_id, ticket_id, event_id, customer_id, reason_type, reconciliation_id, refund_queued, notification_sent FROM ticket_revocations;"
echo ===================== TICKET RETURN REQUESTS (REFUND QUEUE) =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT ticket_return_request_id, ticket_id, order_id, event_id, requester_user_id, status, refund_amount, reason FROM ticket_return_requests;"
echo ===================== ORDERS STATUS =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d 05_ticket_db -c "SELECT order_id, customer_id, event_id, status FROM orders WHERE event_id = 18;"
echo ===================== EVENT DB STATUS =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d event_db -c "SELECT event_id, title, status FROM events WHERE event_id = 18;"
echo ===================== NOTIFICATION DB =====================
"C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5432 -U postgres -d notification_db -c "SELECT notification_id, user_id, title, content, type, is_read, created_at FROM notifications WHERE user_id = 2 ORDER BY notification_id DESC LIMIT 2;"
