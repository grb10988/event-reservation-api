-- ============================================================
-- EventReservation schema
-- Run against a fresh database. Safe to re-run only after a
-- full drop, since this does not check for existing objects.
-- Postgres 13+ ships gen_random_uuid() natively - no extension needed.
-- ============================================================

-- ------------------------------------------------------------
-- Roles: 'api' is used by the application's own connection;
-- 'admin' is for direct/manual psql sessions. Both feed the
-- created_by/updated_by columns via current_user. Passwords
-- below are placeholders - change them, and store the real
-- values in user secrets / your own environment, never here.
-- ------------------------------------------------------------

create role api with login password 'CHANGE_ME_api';
create role admin with login password 'CHANGE_ME_admin';

create table venues (
    id uuid primary key,
    name varchar(200) not null,
    address varchar(300) not null,
    capacity integer not null check (capacity > 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user
);

create table seats (
    id uuid primary key,
    venue_id uuid not null references venues(id),
    section varchar(50) not null,
    row integer not null check (row > 0),
    number integer not null check (number > 0),
    status varchar(20) not null check (status in ('Available', 'Held', 'Reserved')),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user,
    unique (venue_id, section, row, number)
);

create table events (
    id uuid primary key,
    venue_id uuid not null references venues(id),
    name varchar(200) not null,
    description text not null,
    start_time timestamptz not null,
    end_time timestamptz not null check (end_time > start_time),
    ticket_price numeric(10, 2) not null check (ticket_price >= 0),
    status varchar(20) not null check (status in ('Draft', 'Published', 'Cancelled')),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user
);

create table customers (
    id uuid primary key,
    first_name varchar(100) not null,
    last_name varchar(100) not null,
    email varchar(320) not null unique,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user
);

create table reservations (
    id uuid primary key,
    seat_id uuid not null references seats(id),
    event_id uuid not null references events(id),
    customer_id uuid not null references customers(id),
    price numeric(10, 2) not null check (price >= 0),
    status varchar(20) not null check (status in ('Held', 'Confirmed', 'Cancelled', 'Expired')),
    hold_expires_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user
);

-- Enforces "at most one active reservation per seat+event" at the database
-- level - this is the real concurrency guarantee, not just an app-layer check.
create unique index ux_reservations_active_seat_event
    on reservations (seat_id, event_id)
    where status in ('Held', 'Confirmed');

create table orders (
    id uuid primary key,
    customer_id uuid not null references customers(id),
    status varchar(20) not null check (status in ('Pending', 'Completed', 'Cancelled', 'Refunded')),
    confirmation_number varchar(50) unique,
    created_at timestamptz not null,
    updated_at timestamptz not null default now(),
    created_by varchar(50) not null default current_user,
    updated_by varchar(50) not null default current_user
);

create table order_reservations (
    order_id uuid not null references orders(id),
    reservation_id uuid not null references reservations(id),
    primary key (order_id, reservation_id)
);

-- ------------------------------------------------------------
-- Trigger: keeps updated_at/updated_by current on every UPDATE.
-- One function, reused across every table below.
-- ------------------------------------------------------------

create or replace function set_updated_metadata()
returns trigger as $$
begin
    new.updated_at = now();
    new.updated_by = current_user;
    return new;
end;
$$ language plpgsql;

create trigger trg_venues_updated
    before update on venues
    for each row execute function set_updated_metadata();

create trigger trg_seats_updated
    before update on seats
    for each row execute function set_updated_metadata();

create trigger trg_events_updated
    before update on events
    for each row execute function set_updated_metadata();

create trigger trg_customers_updated
    before update on customers
    for each row execute function set_updated_metadata();

create trigger trg_reservations_updated
    before update on reservations
    for each row execute function set_updated_metadata();

create trigger trg_orders_updated
    before update on orders
    for each row execute function set_updated_metadata();

-- ------------------------------------------------------------
-- Grants: 'api' gets the DML it needs at runtime; 'admin' gets
-- full access for manual work. Run after tables exist.
-- ------------------------------------------------------------

grant select, insert, update, delete on all tables in schema public to api;
grant all privileges on all tables in schema public to admin;