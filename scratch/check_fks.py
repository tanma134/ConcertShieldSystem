import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

conn = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur = conn.cursor()

cur.execute("""
    SELECT tc.table_name, kcu.column_name, ccu.table_name AS foreign_table_name, ccu.column_name AS foreign_column_name 
    FROM information_schema.table_constraints AS tc 
    JOIN information_schema.key_column_usage AS kcu ON tc.constraint_name = kcu.constraint_name
    JOIN information_schema.constraint_column_usage AS ccu ON ccu.constraint_name = tc.constraint_name
    WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name IN ('tickets', 'orders', 'order_details', 'ticket_qr_tokens');
""")
print("Foreign keys in ticket_db:")
for r in cur.fetchall():
    print(r)

conn.close()
