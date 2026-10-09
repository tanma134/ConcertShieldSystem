-- Chạy trong event_db sau database/event_db.sql. Không mở bán tự động cho dữ liệu legacy.
BEGIN;
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_status text NOT NULL DEFAULT 'NotSubmitted';
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_version integer NOT NULL DEFAULT 0;
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_reviewed_version integer;
ALTER TABLE events ADD COLUMN IF NOT EXISTS schedule_version integer NOT NULL DEFAULT 0;
ALTER TABLE events ADD COLUMN IF NOT EXISTS sales_frozen boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS event_compliance_documents (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), version integer NOT NULL CHECK(version > 0),
 document_type text NOT NULL, file_name text NOT NULL, content_type text NOT NULL,
 public_id text NOT NULL, secure_url text NOT NULL, resource_type text NOT NULL DEFAULT 'raw',
 size bigint NOT NULL CHECK(size > 0), submitted_by integer NOT NULL, submitted_at timestamptz NOT NULL,
 UNIQUE(event_id,version,document_type)
);
CREATE TABLE IF NOT EXISTS event_compliance_reviews (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), version integer NOT NULL,
 decision text NOT NULL CHECK(decision IN ('Approved','Rejected','RequestMoreInfo')),
 notes text NOT NULL CHECK(length(trim(notes)) BETWEEN 1 AND 2000), reviewed_by integer NOT NULL, reviewed_at timestamptz NOT NULL,
 UNIQUE(event_id,version)
);
CREATE TABLE IF NOT EXISTS event_change_requests (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), type text NOT NULL CHECK(type IN ('Postpone','Reschedule')),
 status text NOT NULL DEFAULT 'Pending' CHECK(status IN ('Pending','Approved','Rejected')),
 reason text NOT NULL CHECK(length(trim(reason)) BETWEEN 10 AND 1000),
 old_starts_at timestamptz NOT NULL, old_ends_at timestamptz NOT NULL,
 new_starts_at timestamptz, new_ends_at timestamptz, old_status text NOT NULL,
 event_version integer NOT NULL, submitted_by integer NOT NULL, submitted_at timestamptz NOT NULL,
 reviewed_by integer, reviewed_at timestamptz, review_notes text, processing_status text NOT NULL DEFAULT 'NotApplied',
 CHECK((type='Postpone' AND new_starts_at IS NULL AND new_ends_at IS NULL) OR
       (type='Reschedule' AND new_starts_at IS NOT NULL AND new_ends_at > new_starts_at))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_event_change_pending ON event_change_requests(event_id) WHERE status='Pending';
CREATE TABLE IF NOT EXISTS governance_outbox (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, kind text NOT NULL, payload text NOT NULL,
 created_at timestamptz NOT NULL, completed_at timestamptz, next_attempt_at timestamptz NOT NULL,
 attempts integer NOT NULL DEFAULT 0, last_error text
);
CREATE INDEX IF NOT EXISTS ix_governance_outbox_pending ON governance_outbox(next_attempt_at,id) WHERE completed_at IS NULL;
COMMIT;
