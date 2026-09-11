-- ============================================================
-- EventReservation schema
-- Run against a fresh database. Safe to re-run only after a
-- full drop, since this does not check for existing objects.
-- ============================================================

-- ------------------------------------------------------------
-- Roles: 'api' is used by the application's own connection;
-- 'admin' is for direct/manual psql sessions. Both feed the
-- created_by/updated_by columns via current_user.
-- ------------------------------------------------------------

CREATE ROLE api WITH LOGIN PASSWORD :'api_password';
CREATE ROLE admin WITH LOGIN PASSWORD :'admin_password';

CREATE TABLE venues (
    id uuid PRIMARY KEY,
    name varchar(200) NOT NULL,
    address varchar(300) NOT NULL,
    capacity integer NOT NULL CHECK (capacity > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user
);

CREATE TABLE seats (
    id uuid PRIMARY KEY,
    venue_id uuid NOT NULL REFERENCES venues(id),
    section varchar(50) NOT NULL,
    row integer NOT NULL CHECK (row > 0),
    number integer NOT NULL CHECK (number > 0),
    status varchar(20) NOT NULL CHECK (status IN ('Available', 'Held', 'Reserved')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user,
    CONSTRAINT ux_seats_venue_section_row_number UNIQUE (venue_id, section, row, number)
);

CREATE TABLE events (
    id uuid PRIMARY KEY,
    venue_id uuid NOT NULL REFERENCES venues(id),
    name varchar(200) NOT NULL,
    description text NOT NULL,
    start_time timestamptz NOT NULL,
    end_time timestamptz NOT NULL CHECK (end_time > start_time),
    ticket_price numeric(10, 2) NOT NULL CHECK (ticket_price >= 0),
    status varchar(20) NOT NULL CHECK (status IN ('Draft', 'Published', 'Cancelled')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user
);

CREATE TABLE customers (
    id uuid PRIMARY KEY,
    first_name varchar(100) NOT NULL,
    last_name varchar(100) NOT NULL,
    email varchar(320) NOT NULL unique,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user
);

CREATE TABLE reservations (
    id uuid PRIMARY KEY,
    seat_id uuid NOT NULL REFERENCES seats(id),
    event_id uuid NOT NULL REFERENCES events(id),
    customer_id uuid NOT NULL REFERENCES customers(id),
    price numeric(10, 2) NOT NULL CHECK (price >= 0),
    status varchar(20) NOT NULL CHECK (status IN ('Held', 'Confirmed', 'Cancelled', 'Expired')),
    hold_expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user
);

-- Enforces "at most one active reservation per seat+event" at the database
-- level - this is the real concurrency guarantee, not just an app-layer CHECK.
CREATE UNIQUE INDEX ux_reservations_active_seat_event
    ON reservations (seat_id, event_id)
    WHERE status IN ('Held', 'Confirmed');

CREATE TABLE orders (
    id uuid PRIMARY KEY,
    customer_id uuid NOT NULL REFERENCES customers(id),
    status varchar(20) NOT NULL CHECK (status IN ('Pending', 'Completed', 'Cancelled', 'Refunded')),
    confirmation_number varchar(50) UNIQUE,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(50) NOT NULL DEFAULT current_user,
    updated_by varchar(50) NOT NULL DEFAULT current_user
);

CREATE TABLE order_reservations (
    order_id uuid NOT NULL REFERENCES orders(id),
    reservation_id uuid NOT NULL REFERENCES reservations(id),
    PRIMARY KEY (order_id, reservation_id)
);

-- ------------------------------------------------------------
-- Trigger: keeps updated_at/updated_by current on every UPDATE.
-- One function, reused across every table below.
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION set_updated_metadata()
RETURNS trigger AS $$
BEGIN
    NEW.updated_at = now();
    NEW.updated_by = current_user;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_venues_updated
    BEFORE UPDATE ON venues
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

CREATE TRIGGER trg_seats_updated
    BEFORE UPDATE ON seats
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

CREATE TRIGGER trg_events_updated
    BEFORE UPDATE ON events
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

CREATE TRIGGER trg_customers_updated
    BEFORE UPDATE ON customers
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

CREATE TRIGGER trg_reservations_updated
    BEFORE UPDATE ON reservations
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

CREATE TRIGGER trg_orders_updated
    BEFORE UPDATE ON orders
    FOR EACH ROW EXECUTE FUNCTION set_updated_metadata();

-- ------------------------------------------------------------
-- Grants: 'api' gets the DML it needs at runtime; 'admin' gets
-- full access for manual work. Run after tables exist.
-- ------------------------------------------------------------

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO api;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO admin;