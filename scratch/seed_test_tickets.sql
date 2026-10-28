-- Seed active order and tickets for Event 18 so it has active tickets for testing Bulk Revocation
INSERT INTO orders (
    customer_id, event_id, full_name, email, phone, event_name, 
    total_amount, discount_amount, final_amount, payment_method, status, is_deleted
) VALUES (
    2, 18, 'Test Customer', 'customer@ticketbox.com', '0987654321', 'Wren Evans - "Call Me" Tour',
    3000000, 0, 3000000, 'VNPay', 'Paid', false
) RETURNING order_id;

INSERT INTO tickets (
    ticket_code, order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, is_deleted
) VALUES 
(gen_random_uuid(), (SELECT MAX(order_id) FROM orders WHERE event_id = 18), 18, 43, 2, 'VIP Zone A', 'Test Customer', 'Active', false),
(gen_random_uuid(), (SELECT MAX(order_id) FROM orders WHERE event_id = 18), 18, 43, 2, 'VIP Zone A', 'Test Customer', 'Active', false);

-- Also ensure Event 1 has 2 active tickets
INSERT INTO tickets (
    ticket_code, order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, is_deleted
) VALUES 
(gen_random_uuid(), 1, 1, 1, 2, 'VIP Arena', 'Test Customer', 'Active', false)
ON CONFLICT DO NOTHING;
