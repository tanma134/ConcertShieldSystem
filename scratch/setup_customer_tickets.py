import psycopg2
import sys
import uuid

sys.stdout.reconfigure(encoding='utf-8')

# -------------------------------------------------------------
# 1. Update 05_ticket_db (Used by CheckinAPI for scanning)
# -------------------------------------------------------------
print("Updating 05_ticket_db...")
conn05 = psycopg2.connect("host=localhost port=5432 dbname=05_ticket_db user=postgres password=123456")
cur05 = conn05.cursor()

# Update Order 6 & 7 to belong to customer@ticketbox.com (User ID 2)
cur05.execute("""
    UPDATE orders
    SET customer_id = 2,
        full_name = 'Test Customer',
        email = 'customer@ticketbox.com',
        phone = '0987654321',
        status = 'Paid'
    WHERE order_id IN (6, 7);
""")

# Update Tickets 10..19 to belong to User ID 2 (Test Customer)
cur05.execute("""
    UPDATE tickets
    SET owner_user_id = 2,
        owner_name = 'Test Customer'
    WHERE ticket_id BETWEEN 10 AND 19;
""")
conn05.commit()
print("Updated orders 6, 7 and tickets 10..19 in 05_ticket_db successfully.")

# Query back tickets in 05_ticket_db
cur05.execute("""
    SELECT t.ticket_id, t.ticket_code, t.event_id, t.ticket_type_name, t.owner_name, t.status, o.email, qr.qr_token
    FROM tickets t
    JOIN orders o ON t.order_id = o.order_id
    LEFT JOIN ticket_qr_tokens qr ON t.ticket_id = qr.ticket_id
    WHERE o.customer_id = 2 AND t.ticket_id BETWEEN 10 AND 19
    ORDER BY t.ticket_id;
""")
print("\n05_ticket_db tickets for customer@ticketbox.com:")
for r in cur05.fetchall():
    print(" ", r)

conn05.close()


# -------------------------------------------------------------
# 2. Update and Insert into ticket_db (Used by TicketAPI for /my-tickets)
# -------------------------------------------------------------
print("\nUpdating ticket_db...")
conn_t = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur_t = conn_t.cursor()

# Ensure Order 8 (Event 21 - Hà Anh Tuấn) belongs to customer@ticketbox.com
cur_t.execute("""
    UPDATE orders
    SET customer_id = 2,
        full_name = 'Test Customer',
        email = 'customer@ticketbox.com',
        phone = '0987654321',
        status = 'Paid'
    WHERE order_id = 8;
""")

# Ensure tickets 200..205 in ticket_db have owner_user_id = 2, owner_name = 'Test Customer'
cur_t.execute("""
    UPDATE tickets
    SET owner_user_id = 2,
        owner_name = 'Test Customer'
    WHERE order_id = 8;
""")

# Check if Order for Event 20 (Acoustic Night) already exists in ticket_db
cur_t.execute("SELECT order_id FROM orders WHERE customer_id = 2 AND event_id = 20;")
existing_ord20 = cur_t.fetchone()

if not existing_ord20:
    cur_t.execute("""
        INSERT INTO orders (
            customer_id, event_id, full_name, email, phone, event_name,
            poster_url, order_date, starts_at, ends_at, total_amount,
            discount_amount, final_amount, payment_method, status, is_deleted
        ) VALUES (
            2, 20, 'Test Customer', 'customer@ticketbox.com', '0987654321', 'Acoustic Night',
            'https://res.cloudinary.com/ticketbox/image/upload/v1/events/acoustic.jpg',
            NOW(), NOW() + INTERVAL '1 day', NOW() + INTERVAL '1 day 4 hours', 3500000,
            0, 3500000, 'VNPAY', 'Paid', false
        ) RETURNING order_id;
    """)
    ord20_id = cur_t.fetchone()[0]
    print(f"Created Order #{ord20_id} for Event 20 in ticket_db")

    # Insert 3 tickets for Event 20 in ticket_db
    tkt_configs = [
        ("VIP Diamond", 1, "00000000-0000-0000-0000-000000000021"),
        ("Platinum", 2, "00000000-0000-0000-0000-000000000022"),
        ("Standard Zone A", 3, "00000000-0000-0000-0000-000000000023"),
    ]

    for t_name, t_type_id, t_code in tkt_configs:
        cur_t.execute("""
            INSERT INTO tickets (
                ticket_code, order_id, event_id, ticket_type_id,
                owner_user_id, ticket_type_name, owner_name, status,
                is_checked_in, is_deleted, gate_id
            ) VALUES (
                %s, %s, 20, %s,
                2, %s, 'Test Customer', 'Active',
                false, false, 'GATE_A'
            ) RETURNING ticket_id;
        """, (t_code, ord20_id, t_type_id, t_name))
        tkt_id = cur_t.fetchone()[0]

        # Insert QR token
        cur_t.execute("""
            INSERT INTO ticket_qr_tokens (
                ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used
            ) VALUES (
                %s, %s, NOW(), NOW() + INTERVAL '30 days', false, false
            );
        """, (tkt_id, f"DYN-TICKET-{tkt_id}-{t_code[:8]}"))
else:
    ord20_id = existing_ord20[0]
    print(f"Order #{ord20_id} for Event 20 already exists in ticket_db")

conn_t.commit()

# Verify tickets for customer_id = 2 in ticket_db
cur_t.execute("""
    SELECT t.ticket_id, t.order_id, t.event_id, t.ticket_type_name, t.owner_name, t.status, o.event_name, o.email
    FROM tickets t
    JOIN orders o ON t.order_id = o.order_id
    WHERE t.owner_user_id = 2 AND NOT t.is_deleted
    ORDER BY t.ticket_id DESC LIMIT 10;
""")
print("\nticket_db tickets for customer@ticketbox.com (User ID 2):")
for r in cur_t.fetchall():
    print(" ", r)

conn_t.close()

# Also sync the UUIDs to 05_ticket_db so if customer displays QR from TicketAPI, CheckinAPI can validate it!
conn05 = psycopg2.connect("host=localhost port=5432 dbname=05_ticket_db user=postgres password=123456")
cur05 = conn05.cursor()

# For tickets 200..205, add their ticket_code / QR tokens to 05_ticket_db if not already there
for tkt_code in ["c6117c4e-6ac7-48eb-92cf-2c2e9babc221", "9307ae2a-90f6-4115-a3de-4fbda09784c4", "a930a77f-ada3-42c6-8c50-3ed48ab86433", "60324223-0af2-4420-916e-6d659a7f099b"]:
    cur05.execute("""
        INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
        VALUES (15, %s, NOW(), NOW() + INTERVAL '30 days', false, false)
        ON CONFLICT DO NOTHING;
    """, (f"DYN-{tkt_code}",))

conn05.commit()
conn05.close()

print("\nAll tickets and QR tokens synced successfully for customer@ticketbox.com!")
