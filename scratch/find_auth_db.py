import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

conn = psycopg2.connect("host=localhost port=5432 dbname=postgres user=postgres password=123456")
cur = conn.cursor()
cur.execute("SELECT datname FROM pg_database WHERE datistemplate = false;")
dbs = [r[0] for r in cur.fetchall()]
print("Postgres Databases:", dbs)

for db in dbs:
    try:
        c = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
        cr = c.cursor()
        cr.execute("SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name LIKE '%user%';")
        tbls = cr.fetchall()
        if tbls:
            print(f"\nTables in {db}:", tbls)
            for t in tbls:
                tname = t[0]
                cr.execute(f"SELECT * FROM {tname} WHERE email ILIKE '%customer%' LIMIT 5;")
                print(f"Users in {db}.{tname}:", cr.fetchall())
        c.close()
    except Exception as e:
        print(f"Error {db}: {e}")

conn.close()
