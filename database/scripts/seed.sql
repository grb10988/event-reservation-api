-- ============================================================
-- Sample data for local development / demoing the API.
-- Run after schema.sql. Uses hardcoded, sequentially-ordered
-- UUIDv7 values so records are easy to reference by hand while
-- testing (Postman, curl, etc.) and remain B-tree-friendly.
-- Only catalog data is seeded (Venue/Seats/Events/Customers) -
-- Reservations/Orders are left for the app itself to create.
-- ============================================================

insert into venues (id, name, address, capacity) values
    ('01b8dac5-b7e8-7001-a000-000000000001', 'City Amphitheater', '123 Main St, Springfield', 500);

insert into seats (id, venue_id, section, row, number, status) values
    ('01b8dac5-bbd0-7002-a000-000000000002', '01b8dac5-b7e8-7001-a000-000000000001', 'A', 1, 1, 'Available'),
    ('01b8dac5-bfb8-7003-a000-000000000003', '01b8dac5-b7e8-7001-a000-000000000001', 'A', 1, 2, 'Available'),
    ('01b8dac5-c3a0-7004-a000-000000000004', '01b8dac5-b7e8-7001-a000-000000000001', 'A', 2, 1, 'Available'),
    ('01b8dac5-c788-7005-a000-000000000005', '01b8dac5-b7e8-7001-a000-000000000001', 'B', 1, 1, 'Available');

insert into events (id, venue_id, name, description, start_time, end_time, ticket_price, status) values
    ('01b8dac5-cb70-7006-a000-000000000006'
    , '01b8dac5-b7e8-7001-a000-000000000001'
    , 'Summer Concert'
    , 'An outdoor summer concert series.'
    , now() + interval '30 days'
    , now() + interval '30 days 3 hours'
    , 50.00
    , 'Published'
);

insert into customers (id, first_name, last_name, email) values
    ('01b8dac5-cf58-7007-a000-000000000007', 'Jane', 'Doe', 'jane.doe@example.com'),
    ('01b8dac5-d340-7008-a000-000000000008', 'John', 'Smith', 'john.smith@example.com');