import psycopg2

sql = """
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS last_exit_at timestamp with time zone NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS checked_in_gate varchar(100) NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS is_inside boolean NOT NULL DEFAULT false;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS re_entry_count integer NOT NULL DEFAULT 0;
"""

for db in ['ticket_db', '05_ticket_db']:
    try:
        conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
        cur = conn.cursor()
        cur.execute(sql)
        conn.commit()
        print(f"Successfully added missing columns to {db}.tickets")
        conn.close()
    except Exception as e:
        print(f"Error {db}: {e}")
