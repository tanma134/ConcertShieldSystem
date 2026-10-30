import psycopg2

def sync_dbs():
    conn_src = psycopg2.connect('host=localhost port=5432 user=postgres password=123456 dbname=ticket_db')
    conn_dst = psycopg2.connect('host=localhost port=5432 user=postgres password=123456 dbname=05_ticket_db')
    
    cur_src = conn_src.cursor()
    cur_dst = conn_dst.cursor()
    
    # 1. Sync orders
    cur_src.execute("""
        SELECT order_id, customer_id, event_id, total_amount, discount_amount, COALESCE(final_amount, total_amount), status, order_date, email, full_name, phone
        FROM orders
    """)
    src_orders = cur_src.fetchall()
    for o in src_orders:
        cur_dst.execute("""
            INSERT INTO orders (order_id, customer_id, event_id, total_amount, discount_amount, final_amount, status, order_date, email, full_name, phone)
            OVERRIDING SYSTEM VALUE
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (order_id) DO UPDATE SET
                status = EXCLUDED.status,
                email = EXCLUDED.email,
                full_name = EXCLUDED.full_name,
                phone = EXCLUDED.phone
        """, o)
    conn_dst.commit()
    print(f"Synced {len(src_orders)} orders.")
    
    # 2. Sync tickets
    cur_src.execute("""
        SELECT ticket_id, order_id, event_id, ticket_type_id, ticket_type_name, 
               ticket_code, status, is_checked_in, checked_in_at, checked_in_by, 
               gate_id, is_deleted, owner_name, owner_user_id
        FROM tickets
    """)
    src_tickets = cur_src.fetchall()
    for t in src_tickets:
        cur_dst.execute("""
            INSERT INTO tickets (ticket_id, order_id, event_id, ticket_type_id, ticket_type_name,
                                ticket_code, status, is_checked_in, checked_in_at, checked_in_by,
                                gate_id, is_deleted, owner_name, owner_user_id)
            OVERRIDING SYSTEM VALUE
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (ticket_id) DO UPDATE SET
                status = EXCLUDED.status,
                ticket_code = EXCLUDED.ticket_code,
                is_checked_in = EXCLUDED.is_checked_in,
                checked_in_at = EXCLUDED.checked_in_at,
                checked_in_by = EXCLUDED.checked_in_by,
                gate_id = EXCLUDED.gate_id,
                owner_name = EXCLUDED.owner_name,
                owner_user_id = EXCLUDED.owner_user_id
        """, t)
    conn_dst.commit()
    print(f"Synced {len(src_tickets)} tickets.")
    
    # 3. Sync ticket_qr_tokens
    cur_src.execute("""
        SELECT ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used, used_at
        FROM ticket_qr_tokens
    """)
    src_qr = cur_src.fetchall()
    for q in src_qr:
        cur_dst.execute("""
            INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used, used_at)
            VALUES (%s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (qr_token) DO UPDATE SET
                ticket_id = EXCLUDED.ticket_id,
                expires_at = EXCLUDED.expires_at,
                is_revoked = EXCLUDED.is_revoked,
                is_used = EXCLUDED.is_used,
                used_at = EXCLUDED.used_at
        """, q)
    conn_dst.commit()
    print(f"Synced {len(src_qr)} qr tokens.")

    conn_src.close()
    conn_dst.close()
    print("Database sync complete!")

if __name__ == '__main__':
    sync_dbs()
