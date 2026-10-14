# Event Reservation API

A ticketed-event reservation system built in ASP.NET Core as a deep dive into
distributed-systems concerns that most CRUD portfolio projects skip: race
conditions under real concurrency, event-driven architecture across process
boundaries, and full observability wired through every layer - not bolted on
after the fact.

This is a learning/portfolio project, built deliberately to the standard of a
real production service rather than the shortest path to a working demo.

## What it does

Venues contain seats. Events are scheduled at a venue and published, which
provisions that venue's seats as inventory for that specific event. Customers
hold a seat for an event (creating a reservation), confirm it, and bundle one
or more confirmed reservations into an order. Orders complete, get cancelled,
or get refunded; events can be cancelled, which cascades through every active
reservation and order tied to them.

## Architecture

Four layers, each only depending on the one below it:

    src/
    ├── domain/          Aggregates, value objects, the Result railway type - no framework dependencies
    ├── application/     Commands/queries, handlers, pipeline behaviors, domain & integration events
    ├── infrastructure/  Dapper repositories, RabbitMQ pub/sub, OpenTelemetry wiring, background workers
    └── api/             Minimal API endpoints, DI composition, Swagger

**Railway-oriented error handling.** Every operation that can fail returns a
`Result`/`Result<T>` instead of throwing for expected failure modes. Handlers
compose with `Bind`/`Map`/`Ensure`/`Combine` rather than manual
`if (result.IsFailure)` branching, so a failure anywhere in a chain short-
circuits the rest automatically. Each `ResultError` carries a `Context` (which
business rule failed), a `Message` (safe to return to a client), and a
`Category` (`Validation`, `Conflict`, `NotFound`, …) that the API layer maps
to the correct HTTP status - callers never see the category or context
directly, only the message.

**CQRS-style dispatch with a real pipeline.** A small hand-rolled
`IDispatcher` resolves `ICommand`/`IQuery` requests to their handler and runs
them through composable `IPipelineBehavior`s - logging, tracing, and metrics
are implemented once and apply to every request, domain event, and
integration event without each handler knowing they exist.

**Domain events → integration events → RabbitMQ.** In-process domain events
(e.g. "a reservation was cancelled") are handled synchronously within the same
request; integration events cross process boundaries over RabbitMQ to
background consumers. Both paths share the same logging/tracing/metrics
pipeline shape as regular requests.

**Seat availability is modeled per event, not per seat.** A physical seat
isn't globally "available" or "held" - it's available for *this* event, which
is why availability lives in its own `event_seats` join aggregate
(`EventSeat`/`IEventSeatRepository`) rather than as a status column on `Seat`.
Holding, reserving, and releasing a seat for an event are all fenced by the
id of the reservation that currently owns the hold, so a stale caller can
never undo someone else's current claim.

## Concurrency and consistency

This project treats race conditions as a first-class concern, not an
afterthought:

- Every state transition (`TryPublishAsync`, `TryHoldAsync`,
  `TryCompleteAsync`, …) is a single atomic `UPDATE ... WHERE id = @Id AND
  status = @RequiredStatus`-style compare-and-swap, not a read-then-write.
- `OrderRepository.AddAsync` runs inside a real transaction that
  re-validates every reservation is still held and unexpired at the instant
  of the actual insert - closing the gap between when a handler first checks
  a reservation and when it's actually claimed.
- A unique index on `order_reservations(reservation_id)` prevents two orders
  from ever successfully claiming the same reservation, even though neither
  insert would violate the table's own composite primary key on its own.
- A background `ReservationHoldSweepWorker` reaps expired holds on an
  interval, racing safely against customer-driven confirms/cancels for the
  same reservation.
- Integration tests exercise these races directly - e.g. firing concurrent
  publish/cancel or concurrent order-claim requests at the same row and
  asserting on the actual outcome, not an assumed one.

## Observability

One OpenTelemetry Collector fans out all three signals to dedicated backends,
all correlated by shared trace/span IDs:

| Signal  | Backend    | URL (local)             |
|---------|------------|--------------------------|
| Traces  | Tempo      | via Grafana              |
| Metrics | Prometheus | http://localhost:9090   |
| Logs    | Loki       | via Grafana              |
| All three | Grafana  | http://localhost:3000   |

Every request/domain-event/integration-event pipeline reports the same way:
a structured log on completion, a span with status and error tags, and a
counter/histogram pair. Failures carry their `ResultError.Context` (which
business rule failed) through all three signals as a queryable, bounded-
cardinality field - never embedded as free text, and never the client-facing
message, which stays `Context`-free by design.

Sensitive data (connection strings, auth headers, `db.statement`) is
scrubbed from every span and log record before export.

## Tech stack

| Concern              | Choice                                      |
|-----------------------|----------------------------------------------|
| Runtime              | .NET 10 / ASP.NET Core Minimal APIs          |
| Data access           | Dapper + Npgsql over PostgreSQL 16           |
| Messaging             | RabbitMQ (topic exchange, DLQ per consumer)  |
| Observability         | OpenTelemetry Collector → Tempo / Prometheus / Loki / Grafana |
| Unit tests             | MSTest + NSubstitute                        |
| Integration tests      | MSTest + Testcontainers (real Postgres, no mocks) |
| Containerization       | Docker Compose                              |

## Getting started

**Prerequisites:** Docker Desktop, .NET 10 SDK (only needed if you want to run
outside Docker).

1. Create a `.env` file in the repo root with:

       POSTGRES_USER=...
       POSTGRES_PASSWORD=...
       POSTGRES_DB=eventreservation
       API_PASSWORD=...
       ADMIN_PASSWORD=...
       RABBITMQ_HOST=rabbitmq
       RABBITMQ_USER=...
       RABBITMQ_PASS=...

2. `docker compose up --build`
3. API + Swagger UI: http://localhost:5045
   Grafana: http://localhost:3000 · Prometheus: http://localhost:9090 ·
   RabbitMQ management: http://localhost:15672

The Postgres container runs `database/01_init.sh` on first start, which
applies `schema.sql` and `seed.sql` automatically - no separate migration
step.

## Running the tests

    dotnet test

Four test projects, bottom-up:
- `tests.domain` - aggregates and value objects, no I/O
- `tests.application` - handlers against substituted repositories (NSubstitute)
- `tests.infrastructure` - infrastructure-level units (e.g. exception mapping)
- `tests.infrastructure.integration` - repositories and full scenario flows
  against a real Postgres instance via Testcontainers; no database mocking
  anywhere in the suite

## API overview

All routes are prefixed with `/api`.

| Resource | Routes |
|----------|--------|
| Venues | `POST /venues` · `GET /venues/{id}` · `GET /venues` · `PUT /venues/{id}` |
| Customers | `POST /customers` · `GET /customers/{id}` · `GET /customers/by-email` · `PUT /customers/{id}` |
| Events | `POST /events` · `GET /events/{id}` · `GET /events` · `PUT /events/{id}` · `POST /events/{id}/publish` · `POST /events/{id}/cancel` |
| Reservations | `POST /reservations` · `GET /reservations/{id}` · `GET /reservations` · `POST /reservations/{id}/confirm` · `POST /reservations/{id}/cancel` |
| Orders | `POST /orders` · `GET /orders/{id}` · `GET /orders/by-confirmation/{confirmationNumber}` · `GET /orders` · `POST /orders/{id}/complete` · `POST /orders/{id}/cancel` |

Refunding a completed order isn't a public route - it's only ever dispatched
internally, as part of the cascade when the event it belongs to is cancelled.

## Known limitations

Documented rather than hidden, since that's more useful to a reader than
pretending they don't exist:

- No authentication/authorization yet - every endpoint is open.
- `POST /reservations/{id}/expire` is reachable publicly even though it's
  meant to be driven only by the background sweep worker.
- RabbitMQ publish confirmation (`mandatory: true` + `BasicReturn` handling)
  isn't wired up yet, so a message that fails to route is currently dropped
  silently.
- No outbox/saga pattern - cross-aggregate consistency (e.g. event
  cancellation cascading to orders and reservations) uses explicit
  compensation, not a transactional guarantee.

## License

Not yet licensed - all rights reserved by default until a license is added.
