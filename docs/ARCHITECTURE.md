# Equipment Borrowing Management System — Design

Legacy College of Compostela · Property Office

## 1. Requirements

### Functional

| # | Objective (from the proposal) | How the system meets it |
|---|---|---|
| 1 | Keep a record of equipment and availability | Inventory of **individually tagged units** (`SPK-001`, `PRJ-002`…) with category, description and condition |
| 2 | Let faculty reserve before an event | Reserve flow: pick a time window → see only what is free → pick units → submit |
| 3 | Record borrowing and returning | Custodian **releases** (borrowed) and **receives** (returned) each reservation; who and when are stamped on the record, with per-unit return condition |
| 4 | Monitor status: available / reserved / borrowed / returned | Live status per unit (Available, Reserved, Borrowed, Under repair, Missing, Retired); reservation status (Pending → Approved → Borrowed → Returned) |
| — | Avoid two departments booking the same item | Server-side overlap check on every booking; the UI greys out booked units and names who has them |

**Roles**

- **Faculty** — browse equipment, reserve for their own department, cancel their own pending/approved requests, see only their own reservations.
- **Admin (Property Custodian)** — everything faculty can do, plus approve/reject, release, record returns, manage equipment, departments and user accounts. Bookings an admin logs on someone's behalf are approved immediately.

### Non-functional

| Concern | Target / assumption |
|---|---|
| Users | ~100–300 accounts, a handful active at once |
| Data | Hundreds of units, a few thousand reservations per year |
| Availability | School hours; a restart is acceptable |
| Hosting | One Windows machine (the existing XAMPP server) or any small VM |
| Security | Signed-in access only, role-based permissions, hashed passwords |

### Constraints

.NET 10 backend, Angular frontend, MySQL/MariaDB (already installed with XAMPP), a small team of student or junior developers maintaining it.

## 2. Architecture choice

### What was considered

| Option | Verdict |
|---|---|
| **Full Clean Architecture** (Domain / Application / Infrastructure / API projects, CQRS, MediatR, repositories) | Too heavy here. Four projects and a pipeline of handlers for ~25 endpoints means most code is plumbing, and new contributors have to learn the pattern before they can fix a bug. |
| **Microservices** | No. One small team, one database, one deployment. |
| **Plain "controllers talk to the DbContext"** | Too thin. The borrowing rules (state machine, double-booking) would end up scattered across controllers. |
| **Modular monolith with feature folders + a rich domain model** ✅ | Chosen. |

### The chosen shape

One ASP.NET Core project, organised **by feature** (vertical slices), with a small **domain layer** that owns the business rules. This keeps the main benefit of Clean Architecture — rules live in plain C# classes that can be unit-tested without a database — without the extra projects and indirection.

```
┌───────────────────────────── Browser ─────────────────────────────┐
│  Angular 20 SPA  (standalone components, signals, lazy routes)    │
│  core/  api.ts · auth · interceptors     shared/  badges · modal  │
│  features/  dashboard · equipment · reservations · admin          │
└───────────────▲───────────────────────────────────────────────────┘
                │ JSON over HTTPS, JWT bearer token
┌───────────────┴────────── ASP.NET Core (.NET 10) ─────────────────┐
│  Features/                                                         │
│   ├─ Auth           login, me, change password                     │
│   ├─ Inventory      EquipmentController → EquipmentService         │
│   ├─ Reservations   ReservationsController → ReservationService    │
│   ├─ Scheduling     ScheduleService  (the one place that reads     │
│   │                  the equipment calendar)                       │
│   ├─ Dashboard, Departments, Users                                 │
│  Domain/            Equipment · Reservation (aggregate) · User ·   │
│                     Department · DateRange · EquipmentStatusResolver│
│  Data/              AppDbContext (EF Core) · configurations ·      │
│                     migrations · seeding                           │
│  Common/            exceptions → ProblemDetails handler            │
└───────────────▲───────────────────────────────────────────────────┘
                │ EF Core 10 (MySql.EntityFrameworkCore)
        ┌───────┴────────┐
        │ MariaDB/MySQL  │  database: lcc_borrowing
        └────────────────┘
```

**Dependency rule:** `Features → Domain`, `Features → Data`, `Data → Domain`. The domain references nothing else, so it stays testable and readable.

### OOP and data structures where they earn their place

- **Encapsulated entities.** Setters are private; state changes only through methods such as `reservation.Approve(...)`, `Release(...)`, `Receive(...)`. Invalid transitions throw `DomainException`, so the rules cannot be bypassed from a controller.
- **Aggregate root.** `Reservation` owns its `ReservationItem`s; nothing edits an item directly.
- **Value object.** `DateRange` is an immutable half-open interval `[Start, End)` with `Overlaps()` — the single definition of "conflict". Back-to-back bookings are allowed.
- **Strategy-like resolver.** `EquipmentStatusResolver` derives a unit's status from its condition and bookings. Status is never stored, so it can never go stale.
- **Lookups and sets.** `ScheduleService` returns an `ILookup<int, Booking>` keyed by equipment id (O(1) per unit when building lists). The Angular picker keeps selections in a `Set<number>` and groups units with a `Map<category, units>`.
- **No generic repository.** `DbContext` already is a unit of work and repository; wrapping it would add code without adding value.

## 3. Data model

```
Department 1───* User                 Equipment
    │                │                    │
    │                │ requested/reviewed/released/received by
    │                ▼                    │
    └──────1───* Reservation 1───* ReservationItem *───1
```

| Table | Key columns |
|---|---|
| `Departments` | `Code` (unique), `Name` |
| `Users` | `Email` (unique), `PasswordHash`, `Role`, `DepartmentId?`, `IsActive` |
| `Equipment` | `AssetTag` (unique), `Name`, `Category` (indexed), `Description?`, `Condition`, `IsActive` |
| `Reservations` | `EventName`, `Venue?`, `Purpose?`, `StartAt`, `EndAt`, `Status`, `DepartmentId`, `RequestedById`, review/release/receive audit columns. Index on `(Status, StartAt, EndAt)` for the overlap query |
| `ReservationItems` | PK `(ReservationId, EquipmentId)`, `ReturnCondition?` |

Enums are stored as strings (readable in phpMyAdmin). All times are stored in UTC and shown in the browser's local time.

### Reservation lifecycle

```
Pending ──approve──▶ Approved ──release──▶ Borrowed ──receive──▶ Returned
   │                    │                     │
   ├──reject──▶ Rejected│                     └─ (past EndAt) shown as Overdue
   └──cancel──▶ Cancelled ◀──cancel──┘
```

`Pending`, `Approved` and `Borrowed` hold equipment on the calendar. A unit returned **Damaged** or **Missing** is flagged on the equipment record and can no longer be reserved until the custodian marks it good again.

## 4. API

All endpoints are under `/api` and need a bearer token except login. Errors use RFC 7807 problem responses (`400` rule broken, `401` not signed in, `403` not allowed, `404` not found, `409` schedule conflict).

| Method & path | Who | Purpose |
|---|---|---|
| `POST /auth/login` | anyone | Returns JWT + user |
| `GET /auth/me`, `POST /auth/change-password` | signed in | |
| `GET /dashboard` | signed in | Status counts, to-release, due-back, overdue (scoped to own for faculty) |
| `GET /equipment?search&category&status&includeRetired` | signed in | Inventory with live status |
| `GET /equipment/categories` | signed in | |
| `GET /equipment/availability?start&end` | signed in | Every serviceable unit + whether it is free in that window, and who holds it if not |
| `GET /equipment/{id}` | signed in | Unit, next 60 days of bookings, last 10 returns |
| `POST /equipment`, `PUT /equipment/{id}` | admin | |
| `GET /reservations?status&overdueOnly&mine&search&page&pageSize` | signed in | Paged; faculty only see their own |
| `GET /reservations/{id}` | owner or admin | Includes `allowedActions` for the caller |
| `POST /reservations` | signed in | Create (conflict-checked) |
| `POST /reservations/{id}/approve` · `/reject` · `/release` · `/receive` | admin | Workflow steps |
| `POST /reservations/{id}/cancel` | owner or admin | |
| `GET/POST/PUT /departments` | read: all · write: admin | |
| `GET/POST/PUT /users`, `POST /users/{id}/reset-password` | admin | |

`allowedActions` is computed on the server, so the Angular app only shows buttons the API will accept — the rules are written once.

## 5. Key decisions and trade-offs

| Decision | Why | Cost / what we gave up |
|---|---|---|
| Track **individual units**, not quantities | Answers "which projector did CBA take?", lets one damaged unit be pulled without touching the rest | Adding 20 identical extension cords means 20 rows (asset tags are needed anyway for a property audit) |
| **Pending requests block the calendar** | First come, first served; a department never gets approved for something already promised | A forgotten pending request holds equipment until someone rejects or cancels it |
| **Status is derived**, never stored | Cannot drift out of sync with reservations | Each list call runs one extra bookings query (cheap at this size) |
| **In-process lock** (`SemaphoreSlim`) around "check calendar then insert" | Simple and correct for one API process | Not valid if the API is ever run as several instances (see §7) |
| **JWT in localStorage** | Simple SPA auth, no cookie/CSRF setup | An XSS bug could read the token; mitigated by Angular's default escaping and an 8-hour expiry |
| **One deployable** (API serves the built Angular files from `wwwroot`) | One thing to install and back up | Frontend and backend release together |
| **MariaDB shim** (`Data/MariaDbHistoryRepository.cs`) | XAMPP ships MariaDB 10.4, which rejects the provider's `GET_LOCK(name, -1)` migration lock | One small provider-specific file; delete it after moving to MySQL 8 / MariaDB 10.5+ |

## 6. Scale, reliability, operations

- **Load.** Even at 300 users the API handles a few requests per second at peak — a single process on the existing server is plenty. No cache or queue is needed.
- **Backups.** The whole state is the `lcc_borrowing` database. A nightly `mysqldump` to another disk is the most valuable operational step.
- **Errors.** Unhandled exceptions are logged and return a generic message; business-rule errors return a readable sentence the UI shows directly.
- **Audit.** Every reservation stores who reviewed, released and received it, and when.

## 7. Security

| Control | Implementation |
|---|---|
| Brute-force protection | `LoginThrottle`: every 5 failed sign-ins → lockout of **1 min, then 5 min, then 10 min** (and 10 min for each further round). Keyed by email + IP so an attacker cannot lock a teacher out from elsewhere. Reset on success or after 24 h of no failures. The UI shows a live countdown from the `retryAfterSeconds` the API returns with `429`. |
| Rate limiting | ASP.NET Core rate limiter: 20 sign-in requests/min and 300 API requests/min per IP. |
| Account enumeration | Same error for unknown email and wrong password; unknown emails still run one hash check so timing does not leak which accounts exist. |
| Passwords | ASP.NET Core Identity `PasswordHasher` (PBKDF2), rehashed automatically when the algorithm settings are upgraded; minimum 8 characters. |
| Sessions | Signed JWT (HMAC-SHA256), 8-hour expiry, signing key from configuration/environment — never committed for production. |
| Authorization | Role checks on every admin endpoint; faculty can only read and cancel their own reservations (enforced in the service, not just the UI). |
| HTTP headers | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, a Content-Security-Policy; HSTS + HTTPS redirect outside Development. |
| Errors | Problem responses never include stack traces or internal messages. |

**Behind IIS or a reverse proxy:** enable `UseForwardedHeaders` so the lockout and rate limits see the real client IP rather than the proxy's.

## 8. What to revisit as it grows

| Trigger | Change |
|---|---|
| Running more than one API instance | Replace the in-process lock with a database lock (`SELECT … FOR UPDATE` on the equipment rows, or `GET_LOCK`) |
| Requests left pending too long | Auto-expire pending requests N hours before their start time |
| Custodian asks for reminders | Background job (hosted service) e-mailing due-back and overdue notices |
| Need for a calendar view | Week/month view over the existing `availability` and `bookings` queries |
| Reports for property audit | CSV/PDF export of borrowing history per unit or department |
| School SSO (Google Workspace / Microsoft 365) | Swap password login for OpenID Connect; roles stay in the `Users` table |
| Hundreds of identical consumables | Add a quantity-based "bulk item" type alongside tagged units |
