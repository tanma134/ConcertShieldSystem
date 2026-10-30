import psycopg2
import sys
import uuid

sys.stdout.reconfigure(encoding='utf-8')

# -----------------------------------------------------------------
# 1. Insert into 05_ticket_db (Used by CheckinAPI for scanner)
# -----------------------------------------------------------------
print("Connecting to 05_ticket_db...")
conn05 = psycopg2.connect("host=localhost port=5432 dbname=05_ticket_db user=postgres password=123456")
cur05 = conn05.cursor()

fresh_tickets = [
    # Event 20: Acoustic Night (Order 6)
    (6, 20, "TKT-AC-VIP-011", "QR-ACOUSTIC-VIP-011", "VIP Diamond", 1, "GATE_A"),
    (6, 20, "TKT-AC-VIP-012", "QR-ACOUSTIC-VIP-012", "VIP Diamond", 1, "GATE_A"),
    (6, 20, "TKT-AC-PLAT-013", "QR-ACOUSTIC-PLAT-013", "Platinum", 2, "GATE_A"),
    (6, 20, "TKT-AC-PLAT-014", "QR-ACOUSTIC-PLAT-014", "Platinum", 2, "GATE_A"),
    (6, 20, "TKT-AC-STD-015", "QR-ACOUSTIC-STD-015", "Standard Zone A", 3, "GATE_A"),
    (6, 20, "TKT-AC-STD-016", "QR-ACOUSTIC-STD-016", "Standard Zone A", 3, "GATE_A"),

    # Event 21: Hà Anh Tuấn - "Chân Trời Rực Rỡ" Live Concert (Order 7)
    (7, 21, "TKT-HAT-VIP-011", "QR-HAT-VIP-011", "VIP Diamond", 1, "GATE_A"),
    (7, 21, "TKT-HAT-VIP-012", "QR-HAT-VIP-012", "VIP Diamond", 1, "GATE_A"),
    (7, 21, "TKT-HAT-PLAT-013", "QR-HAT-PLAT-013", "Platinum", 2, "GATE_A"),
    (7, 21, "TKT-HAT-PLAT-014", "QR-HAT-PLAT-014", "Platinum", 2, "GATE_A"),
    (7, 21, "TKT-HAT-STD-015", "QR-HAT-STD-015", "Standard Zone A", 3, "GATE_A"),
    (7, 21, "TKT-HAT-STD-016", "QR-HAT-STD-016", "Standard Zone A", 3, "GATE_A"),
]

inserted_05 = []
for order_id, event_id, t_code, qr_token, t_name, t_type_id, gate_id in fresh_tickets:
    cur05.execute("""
        INSERT INTO tickets (
            ticket_code, order_id, event_id, ticket_type_id,
            owner_user_id, ticket_type_name, owner_name, status,
            is_checked_in, is_deleted, gate_id
        ) VALUES (
            %s, %s, %s, %s,
            2, %s, 'Test Customer', 'Active',
            false, false, %s
        ) RETURNING ticket_id;
    """, (t_code, order_id, event_id, t_type_id, t_name, gate_id))
    tkt_id = cur05.fetchone()[0]

    cur05.execute("""
        INSERT INTO ticket_qr_tokens (
            ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used
        ) VALUES (
            %s, %s, NOW(), NOW() + INTERVAL '30 days', false, false
        );
    """, (tkt_id, qr_token))

    inserted_05.append((tkt_id, event_id, t_code, qr_token, t_name))

conn05.commit()
conn05.close()
print(f"Inserted {len(inserted_05)} fresh active tickets into 05_ticket_db.")


# -----------------------------------------------------------------
# 2. Insert into ticket_db (Used by TicketAPI for /my-tickets)
# -----------------------------------------------------------------
print("\nConnecting to ticket_db...")
conn_t = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur_t = conn_t.cursor()

# Get Order for Event 20 (Acoustic Night) in ticket_db
cur_t.execute("SELECT order_id FROM orders WHERE customer_id = 2 AND event_id = 20 ORDER BY order_id DESC LIMIT 1;")
ord20_t = cur_t.fetchone()[0]

# Get Order for Event 21 (Hà Anh Tuấn) in ticket_db
cur_t.execute("SELECT order_id FROM orders WHERE customer_id = 2 AND event_id = 21 ORDER BY order_id DESC LIMIT 1;")
ord21_t = cur_t.fetchone()[0]

inserted_t = []
for order_id_05, event_id, t_code, qr_token, t_name, t_type_id, gate_id in fresh_tickets:
    ord_id_target = ord20_t if event_id == 20 else ord21_t
    gen_uuid = str(uuid.uuid4())

    cur_t.execute("""
        INSERT INTO tickets (
            ticket_code, order_id, event_id, ticket_type_id,
            owner_user_id, ticket_type_name, owner_name, status,
            is_checked_in, is_deleted, gate_id
        ) VALUES (
            %s, %s, %s, %s,
            2, %s, 'Test Customer', 'Active',
            false, false, %s
        ) RETURNING ticket_id;
    """, (gen_uuid, ord_id_target, event_id, t_type_id, t_name, gate_id))
    tkt_id = cur_t.fetchone()[0]

    cur_t.execute("""
        INSERT INTO ticket_qr_tokens (
            ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used
        ) VALUES (
            %s, %s, NOW(), NOW() + INTERVAL '30 days', false, false
        );
    """, (tkt_id, qr_token))

    inserted_t.append((tkt_id, event_id, t_code, qr_token, t_name))

conn_t.commit()
conn_t.close()
print(f"Inserted {len(inserted_t)} fresh active tickets into ticket_db.")

print("\n--- Summary of New Fresh Tickets for Testing ---")
for t in inserted_05:
    ev_name = "Acoustic Night" if t[1] == 20 else 'Hà Anh Tuấn Live Concert'
    print(f"Event: {ev_name} | Tier: {t[4]} | Ticket Code: {t[2]} | Dynamic QR: {t[3]}")
