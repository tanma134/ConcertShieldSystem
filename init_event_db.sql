-- =============================================================================
-- DATABASE: event_db
-- System: PostgreSQL 13+
-- Description: Init script for EventAPI database schema
-- =============================================================================

DROP TABLE IF EXISTS seating_templates CASCADE;
DROP TABLE IF EXISTS reviews CASCADE;
DROP TABLE IF EXISTS wishlists CASCADE;
DROP TABLE IF EXISTS seats CASCADE;
DROP TABLE IF EXISTS seat_zones CASCADE;
DROP TABLE IF EXISTS seat_maps CASCADE;
DROP TABLE IF EXISTS refund_policies CASCADE;
DROP TABLE IF EXISTS pricing_rules CASCADE;
DROP TABLE IF EXISTS ticket_types CASCADE;
DROP TABLE IF EXISTS event_images CASCADE;
DROP TABLE IF EXISTS events CASCADE;

-- 1. Events table
CREATE TABLE events (
    event_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organizer_id INT NOT NULL,
    category_id INT NOT NULL DEFAULT 1,
    title VARCHAR(200) NOT NULL,
    slug VARCHAR(250) NOT NULL UNIQUE,
    short_description VARCHAR(500),
    description TEXT,
    poster_url VARCHAR(500),
    poster_public_id VARCHAR(500),
    banner_url VARCHAR(500),
    banner_public_id VARCHAR(500),
    location_name VARCHAR(200),
    address VARCHAR(300),
    city VARCHAR(100),
    longitude NUMERIC(11,8),
    latitude NUMERIC(10,8),
    starts_at TIMESTAMPTZ,
    ends_at TIMESTAMPTZ,
    timezone VARCHAR(50) DEFAULT 'SE Asia Standard Time',
    has_seating_chart BOOLEAN DEFAULT FALSE,
    seating_mode VARCHAR(30) DEFAULT 'ReservedSeating',
    requires_virtual_queue BOOLEAN DEFAULT FALSE,
    status VARCHAR(20) DEFAULT 'Draft',
    rejected_reason VARCHAR(500),
    submitted_at TIMESTAMPTZ,
    approved_at TIMESTAMPTZ,
    rejected_at TIMESTAMPTZ,
    reviewed_by INT,
    is_featured BOOLEAN DEFAULT FALSE,
    view_count INT DEFAULT 0,
    total_tickets INT DEFAULT 0,
    sold_tickets INT DEFAULT 0,
    min_tickets_per_account INT,
    max_tickets_per_account INT,
    meta_title VARCHAR(200),
    meta_description VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    published_at TIMESTAMPTZ,
    created_by INT,
    updated_by INT,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by INT
);

CREATE INDEX ix_events_organizer_id ON events(organizer_id);
CREATE INDEX ix_events_status ON events(status);

-- 2. Event Images table
CREATE TABLE event_images (
    image_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    image_url VARCHAR(500) NOT NULL,
    public_id VARCHAR(500),
    sort_order INT DEFAULT 0,
    is_main BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by INT
);

-- 3. Ticket Types table
CREATE TABLE ticket_types (
    ticket_type_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    type_name VARCHAR(100) NOT NULL,
    description VARCHAR(500),
    price NUMERIC(15,2) NOT NULL,
    original_price NUMERIC(15,2),
    quantity INT NOT NULL,
    sold_quantity INT DEFAULT 0,
    min_per_order INT DEFAULT 1,
    max_per_order INT DEFAULT 10,
    color_code VARCHAR(20),
    sort_order INT DEFAULT 0,
    status VARCHAR(20) DEFAULT 'Active',
    sales_starts_at TIMESTAMPTZ,
    sales_ends_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by INT
);

CREATE UNIQUE INDEX uq_ticket_types_event_name_active ON ticket_types(event_id, type_name) WHERE is_deleted = false;

-- 4. Pricing Rules table
CREATE TABLE pricing_rules (
    pricing_rule_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_type_id INT NOT NULL REFERENCES ticket_types(ticket_type_id) ON DELETE CASCADE,
    rule_name VARCHAR(100),
    rule_type VARCHAR(30),
    adjusted_price NUMERIC(15,2),
    discount_percent NUMERIC(5,2),
    trigger_from TIMESTAMPTZ,
    trigger_to TIMESTAMPTZ,
    quantity_threshold INT,
    priority INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. Refund Policies table
CREATE TABLE refund_policies (
    refund_policy_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    policy_name VARCHAR(150),
    description VARCHAR(1000),
    deadline_before_event_hours INT,
    refund_percent NUMERIC(5,2),
    requires_organizer_approval BOOLEAN DEFAULT TRUE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 6. Seat Maps table
CREATE TABLE seat_maps (
    seat_map_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    name VARCHAR(150),
    layout_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by INT
);

-- 7. Seat Zones table
CREATE TABLE seat_zones (
    seat_zone_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    seat_map_id INT NOT NULL REFERENCES seat_maps(seat_map_id) ON DELETE CASCADE,
    ticket_type_id INT REFERENCES ticket_types(ticket_type_id),
    zone_name VARCHAR(100),
    shape_json JSONB,
    zone_type VARCHAR(20) DEFAULT 'Seated',
    capacity INT DEFAULT 0
);

-- 8. Seats table
CREATE TABLE seats (
    seat_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    seat_zone_id INT NOT NULL REFERENCES seat_zones(seat_zone_id) ON DELETE CASCADE,
    row_label VARCHAR(10),
    seat_number VARCHAR(10),
    x_coordinate INT,
    y_coordinate INT,
    status VARCHAR(20) DEFAULT 'Available',
    held_by_user_id INT,
    hold_expires_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 9. Wishlists table
CREATE TABLE wishlists (
    wishlist_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    user_id INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX uq_wishlists_user_event ON wishlists(user_id, event_id);

-- 10. Reviews table
CREATE TABLE reviews (
    review_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES events(event_id) ON DELETE CASCADE,
    user_id INT NOT NULL,
    rating INT NOT NULL,
    comment VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

-- 11. Seating Templates table
CREATE TABLE seating_templates (
    seating_template_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organizer_id INT NOT NULL,
    name VARCHAR(150) NOT NULL,
    description VARCHAR(500),
    seating_mode VARCHAR(30) DEFAULT 'ReservedSeating',
    is_public BOOLEAN DEFAULT FALSE,
    layout_json JSONB,
    zones_json JSONB NOT NULL,
    created_by INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ
);

CREATE INDEX ix_seating_templates_organizer_id ON seating_templates(organizer_id);
CREATE INDEX ix_seating_templates_is_public ON seating_templates(is_public);
