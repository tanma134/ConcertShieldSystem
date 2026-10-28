# ConcertShield — SQL consolidated by database

These are consolidated *source scripts*, not a verified migration against your live PostgreSQL databases. Back up every database before applying.

| File | Contents | Caution |
| --- | --- | --- |
| event_db.sql | event_db.sql + ALL_IN_ONE/1_event_db_ALL.sql | Original event seed contains data-changing statements; review before applying to an existing DB |
| ticket_db.sql | 05_ticket_db_FULL_RERUN_SAFE.sql + ALL_IN_ONE/2_ticket_db_ALL.sql | Leading ROLLBACK removed; includes demo/test data updates; review before running |
| payment_db.sql | ALL_IN_ONE/4_payment_db_ALL.sql | **Only payment_refunds extension**. No base payment schema found in ZIP. Do not treat as complete payment DB bootstrap |
| notification_db.sql | ALL_IN_ONE/3_notification_db_ALL.sql | Notification schema extension |
| authentication_db.sql | init_authen_db.sql + avatar migration | **DROP TABLE statements: destructive** |
| checkin_db.sql | 07_checkin_db.sql | Existing checkin script |

No cross-database foreign keys are introduced. The mixed database/011_return_review_refund.sql is already distributed among event/ticket/payment via the ALL_IN_ONE scripts, not appended twice.

Original migrations remain in the original source ZIP. These scripts should be run **against their respective databases**, not all in one PostgreSQL database. They have NOT been run against a live database and have NOT been proven fully rerunnable. Review transaction and seed sections first.

Missing source: payment_db base creation SQL; only refund table migration is available. Also no dedicated complete review/queue database initialization was found in the top-level database folder.
