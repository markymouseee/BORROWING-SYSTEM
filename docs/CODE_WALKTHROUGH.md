# Code Walkthrough — Equipment Borrowing System

Legacy College of Compostela

This document explains the code so you can walk a reviewer through it. It goes in the order a reviewer usually asks: what the system does, how it is organised, how one request travels through it, then each part in detail. The last sections list the OOP concepts and data structures used (with the file where each one lives) and questions a teacher is likely to ask.

For the *why* behind the design choices, see [ARCHITECTURE.md](ARCHITECTURE.md). This file is about the *how*.

---

## 1. The system in one minute

**What it does.** Teachers reserve school equipment (speakers, microphones, projectors, extension cords) for an event. The Property Custodian approves the request, hands the equipment over, and records its return. The system stops two departments from booking the same item at the same time.

**Two kinds of user.**

| Role | Can do |
|---|---|
| **Faculty** | See equipment, reserve for their own department, cancel their own requests, see only their own reservations |
| **Admin** (Property Custodian) | Everything above, plus approve / reject, release, record returns, and manage equipment, departments and users |

**Technology.**

| Part | Technology | Folder |
|---|---|---|
| Backend (API) | ASP.NET Core on .NET 10, Entity Framework Core 10 | `backend/src/BorrowingSystem.Api` |
| Database | MySQL / MariaDB (XAMPP), database `lcc_borrowing` | — |
| Frontend | Angular 20 | `frontend/src/app` |
| Unit tests | xUnit (26 tests) | `backend/tests/BorrowingSystem.Tests` |

**The life of a reservation.** This is the most important picture in the project:

```
Pending ──approve──▶ Approved ──release──▶ Borrowed ──receive──▶ Returned
   │                    │                     │
   ├──reject──▶ Rejected│                     └─ past its end time = "Overdue"
   └──cancel──▶ Cancelled ◀──cancel──┘
```

---

## 2. How the code is organised

### The architecture in one sentence

It is **one backend project, organised by feature, with a small domain layer that holds the business rules** (a "modular monolith with feature folders").

### The layers

```
Browser (Angular)
   │  HTTP + JSON, with a login token on every request
   ▼
Controller      receives the request, checks the role, calls a service
   ▼
Service         loads data, checks who is asking, calls the domain, saves
   ▼
Domain          the business rules (plain C# classes, no database code)
   ▼
Data (EF Core)  turns C# objects into SQL and back
   ▼
MySQL / MariaDB
```

Each layer only talks to the one below it. The **domain** layer knows nothing about the database or the web — that is why it can be unit-tested without either.

### Backend folders

```
backend/src/BorrowingSystem.Api/
├─ Program.cs              starts the app: registers services, builds the request pipeline
├─ Domain/                 THE BUSINESS RULES
│   ├─ Reservation.cs          the borrowing workflow (state machine)
│   ├─ ReservationItem.cs      one equipment unit inside a reservation
│   ├─ Equipment.cs            one physical, tagged unit
│   ├─ User.cs, Department.cs
│   ├─ DateRange.cs            a time period + the "do these overlap?" rule
│   ├─ Booking.cs              "unit X is held by reservation Y during period Z"
│   ├─ EquipmentStatusResolver.cs   works out Available / Reserved / Borrowed…
│   └─ Enums.cs                UserRole, ReservationStatus, EquipmentStatus…
├─ Features/               ONE FOLDER PER FEATURE (controller + service + DTOs)
│   ├─ Auth/                   login, token, lockout
│   ├─ Inventory/              equipment list, detail, availability, add/edit
│   ├─ Reservations/           create, approve, reject, cancel, release, receive
│   ├─ Scheduling/             reads the equipment calendar (conflict checks)
│   ├─ Dashboard/, Departments/, Users/
├─ Data/                   DATABASE
│   ├─ AppDbContext.cs         the EF Core "session" with the database
│   ├─ EntityConfigurations.cs table/column rules (lengths, indexes, keys)
│   ├─ DatabaseInitializer.cs  applies migrations + seeds first data
│   ├─ Migrations/             generated: creates the tables
│   └─ MariaDbHistoryRepository.cs  small fix for XAMPP's MariaDB
└─ Common/                 SHARED HELPERS
    ├─ Entity.cs               base class with Id
    ├─ Exceptions.cs           DomainException, ConflictException, NotFoundException…
    ├─ ApiExceptionHandler.cs  turns exceptions into HTTP error responses
    ├─ SecuritySetup.cs        rate limiting + security headers
    └─ PagedResult.cs          one page of a list
```

### Frontend folders

```
frontend/src/app/
├─ app.config.ts        registers the router and the HTTP interceptors
├─ app.routes.ts        URL → page, with guards (who may open it)
├─ app.ts               root component: page outlet + toasts + confirm dialog
├─ core/                APP-WIDE SERVICES (no UI)
│   ├─ models.ts            TypeScript types that mirror the API's JSON
│   ├─ api.ts               one class per API area (EquipmentApi, ReservationApi…)
│   ├─ auth.service.ts      the logged-in session (token + user)
│   ├─ http.ts              interceptors: attach token, handle errors
│   ├─ guards.ts            authGuard, adminGuard, guestGuard
│   ├─ errors.ts            turns an error response into a readable message
│   ├─ toast.service.ts, confirm.service.ts, theme.service.ts
├─ shared/              REUSABLE UI PIECES
│   ├─ icon, status-badge, modal, confirm-dialog, pagination components
│   ├─ schedule.pipe.ts     formats a date range
│   └─ count-up.directive.ts  animates a number
├─ layout/
│   └─ shell.component.*    sidebar + top bar that wrap every page
└─ features/            ONE FOLDER PER SCREEN AREA
    ├─ auth/login.page.*
    ├─ dashboard/dashboard.page.*
    ├─ equipment/           list, detail, add/edit dialog
    ├─ reservations/        list, detail, new reservation
    └─ admin/               departments, users
```

---

## 3. One request, start to finish

The best way to explain the code is to follow one action. **A teacher clicks "Send request" on the New Reservation page.**

**① Angular page** — [new-reservation.page.ts](../frontend/src/app/features/reservations/new-reservation.page.ts), `submit()`
Checks the form (a time is set, at least one item is picked, the event has a name), then calls `reservationApi.create({...})`.

**② API client** — [api.ts](../frontend/src/app/core/api.ts), `ReservationApi.create()`
Sends `POST /api/reservations` with the data as JSON.

**③ Interceptor** — [http.ts](../frontend/src/app/core/http.ts), `authInterceptor`
Adds the header `Authorization: Bearer <token>` so the server knows who is calling.

**④ Controller** — [ReservationsController.cs](../backend/src/BorrowingSystem.Api/Features/Reservations/ReservationsController.cs), `Create()`
`[Authorize]` rejects the call if there is no valid token. `[ApiController]` checks the request's validation attributes (`[Required]`, `[StringLength]`). Then it passes the request to the service. Controllers are kept thin — they contain no business logic.

**⑤ Service** — [ReservationService.cs](../backend/src/BorrowingSystem.Api/Features/Reservations/ReservationService.cs), `CreateAsync()`

```csharp
var requester = await currentUser.GetAsync(ct);                    // who is asking
var departmentId = await ResolveDepartmentAsync(requester, ...);   // faculty → own department
var period = new DateRange(request.StartAt..., request.EndAt...);  // throws if end <= start
var equipment = await db.Equipment.Where(e => ids.Contains(e.Id)).ToListAsync(ct);

await ScheduleGate.WaitAsync(ct);          // only one booking is checked at a time
try
{
    await schedule.EnsureAvailableAsync(equipment, period, ct: ct);   // double-booking check
    var reservation = Reservation.Create(requester, departmentId, ..., period, equipment, Now);
    if (requester.IsAdmin) reservation.Approve(requester, Now);       // custodian's own bookings skip approval
    db.Reservations.Add(reservation);
    await db.SaveChangesAsync(ct);
    return await GetAsync(reservation.Id, ct);
}
finally { ScheduleGate.Release(); }
```

**⑥ Conflict check** — [ScheduleService.cs](../backend/src/BorrowingSystem.Api/Features/Scheduling/ScheduleService.cs), `EnsureAvailableAsync()`
Loads existing bookings for those units and asks `DateRange.Overlaps()` whether any of them clash. If one does, it throws a `ConflictException` naming the unit and who has it.

**⑦ Domain** — [Reservation.cs](../backend/src/BorrowingSystem.Api/Domain/Reservation.cs), `Reservation.Create()`
Enforces the rules: event name required, the time has not passed, at most 14 days, at least one item, every item is serviceable. If all pass, it builds the reservation with status `Pending`.

**⑧ Database** — `db.SaveChangesAsync()`
EF Core writes one row to `Reservations` and one row per item to `ReservationItems`, in a single transaction.

**⑨ Response**
The service returns a `ReservationDetailDto`. The controller sends it back as JSON with status `201 Created`.

**If something goes wrong** — [ApiExceptionHandler.cs](../backend/src/BorrowingSystem.Api/Common/ApiExceptionHandler.cs)
Any exception is caught in one place and mapped to an HTTP status:

| Exception | HTTP status | Meaning |
|---|---|---|
| `DomainException` | 400 | a business rule was broken |
| `ConflictException` | 409 | already booked |
| `ForbiddenException` | 403 | not allowed |
| `NotFoundException` | 404 | no such record |
| anything else | 500 | bug — logged, generic message shown |

**⑩ Back in Angular**
On success the page shows a toast and opens the reservation's detail page. On error the message is shown beside the Submit button and the availability list is refreshed.

---

## 4. Backend, part by part

### 4.1 `Program.cs` — where the app starts

[Program.cs](../backend/src/BorrowingSystem.Api/Program.cs) does two jobs.

**Job 1 — register services (dependency injection).** The app lists the classes it needs, and ASP.NET creates them and hands them to whoever asks:

```csharp
builder.Services.AddDbContext<AppDbContext>(...);       // database
builder.Services.AddJwtAuthentication(...);             // login tokens
builder.Services.AddApiSecurity();                      // lockout + rate limits
builder.Services.AddScoped<ReservationService>();       // one per request
```

- `AddScoped` = a new object for each HTTP request.
- `AddSingleton` = one object for the whole app (used for `TokenService`, `LoginThrottle`, `TimeProvider`).

**Job 2 — build the request pipeline (middleware).** Every request passes through these in order:

```csharp
app.UseExceptionHandler();     // catch errors → JSON error response
app.UseSecurityHeaders();      // add security headers
app.UseStaticFiles();          // serve the built Angular files (production)
app.UseRateLimiter();          // too many requests → 429
app.UseAuthentication();       // read the token: who are you?
app.UseAuthorization();        // are you allowed?
app.MapControllers();          // run the matching controller method
```

Finally `InitializeDatabaseAsync()` creates/updates the tables and seeds the first data.

### 4.2 Domain — the business rules

These are plain C# classes. They have **private setters**: other code can read the data but can only change it by calling a method, and the method checks the rules first.

#### `Entity` — [Entity.cs](../backend/src/BorrowingSystem.Api/Common/Entity.cs)

```csharp
public abstract class Entity
{
    public int Id { get; protected set; }
}
```

The base class that `User`, `Department`, `Equipment` and `Reservation` inherit from.

#### `DateRange` — [DateRange.cs](../backend/src/BorrowingSystem.Api/Domain/DateRange.cs)

A period of time with a start and an end. It is a **value object**: it has no Id, it cannot be changed after creation, and two ranges with the same dates are equal.

```csharp
public DateRange(DateTime start, DateTime end)
{
    if (end <= start) throw new DomainException("The end of the schedule must be after its start.");
    Start = start; End = end;
}

public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;
```

`Overlaps` is the single definition of a schedule conflict. Two periods overlap when each one starts before the other ends. A booking that ends at 10:00 does **not** clash with one that starts at 10:00, so back-to-back bookings are allowed.

#### `Equipment` — [Equipment.cs](../backend/src/BorrowingSystem.Api/Domain/Equipment.cs)

One physical unit with its own asset tag (`PRJ-001`). The system tracks individual units, not quantities, so it can say exactly which projector a department has.

- `Condition` — `Good`, `NeedsRepair`, or `Missing`.
- `IsActive` — `false` means retired.
- `IsServiceable` — `IsActive && Condition == Good`. Only serviceable units can be borrowed.
- `ApplyReturnCondition()` — when a unit comes back Damaged it becomes `NeedsRepair`; Missing becomes `Missing`.

#### `Reservation` — [Reservation.cs](../backend/src/BorrowingSystem.Api/Domain/Reservation.cs)

**The most important class.** It is the *aggregate root*: it owns its list of `ReservationItem`s and every status change goes through one of its methods.

| Method | Allowed from | Result | Extra rule |
|---|---|---|---|
| `Create(...)` (static) | — | `Pending` | name required, not in the past, ≤ 14 days, ≥ 1 serviceable item |
| `Approve(reviewer, now)` | `Pending` | `Approved` | records who and when |
| `Reject(reviewer, reason, now)` | `Pending` | `Rejected` | a reason is required |
| `Cancel()` | `Pending`, `Approved` | `Cancelled` | |
| `Release(custodian, now)` | `Approved` | `Borrowed` | every item must still be serviceable |
| `Receive(custodian, conditions, remarks, now)` | `Borrowed` | `Returned` | sets each item's return condition |

Every method starts with a guard:

```csharp
private void EnsureStatus(string action, params ReservationStatus[] allowed)
{
    if (!allowed.Contains(Status))
        throw new DomainException($"Cannot {action} a reservation that is {Status...}.");
}
```

So it is impossible to, for example, release a reservation that was never approved — not from a controller, not from a bug elsewhere. This pattern is called a **state machine**.

Other members:

- `BlockingStatuses` — `Pending`, `Approved`, `Borrowed`. These hold equipment on the calendar.
- `IsOverdue(now)` — `Status == Borrowed && now > EndAt`. "Overdue" is calculated, not stored.
- `Period` — returns `new DateRange(StartAt, EndAt)`.
- `Code` — `RSV-00001`, built from the Id.

#### `EquipmentStatusResolver` — [EquipmentStatusResolver.cs](../backend/src/BorrowingSystem.Api/Domain/EquipmentStatusResolver.cs)

Works out what status to show for a unit. The status is **not stored in the database** — it is calculated from the unit's condition and its bookings each time, so it can never be out of date.

```
not active                  → Retired
condition Missing           → Missing
condition NeedsRepair       → UnderRepair
has a Borrowed booking      → Borrowed
has a booking in next 24 h  → Reserved
otherwise                   → Available
```

#### `User` and `Department`

[User.cs](../backend/src/BorrowingSystem.Api/Domain/User.cs) holds name, email (stored lowercase), password **hash**, role, department, and `IsActive`. Rule: a Faculty account must have a department. [Department.cs](../backend/src/BorrowingSystem.Api/Domain/Department.cs) holds a code and a name.

### 4.3 Data — talking to the database

#### `AppDbContext` — [AppDbContext.cs](../backend/src/BorrowingSystem.Api/Data/AppDbContext.cs)

The Entity Framework Core class that represents the database. Each `DbSet` is a table:

```csharp
public DbSet<Equipment> Equipment => Set<Equipment>();
public DbSet<Reservation> Reservations => Set<Reservation>();
```

You write C# (LINQ) and EF Core produces the SQL:

```csharp
db.Equipment.Where(e => e.IsActive).OrderBy(e => e.AssetTag).ToListAsync();
// → SELECT ... FROM Equipment WHERE IsActive = 1 ORDER BY AssetTag
```

Because EF Core always sends values as **parameters**, user input never becomes part of the SQL text. That is what prevents SQL injection.

`ConfigureConventions` sets two project-wide rules: all dates are stored as UTC, and enums are stored as readable text (`"Approved"`, not `1`).

#### `EntityConfigurations` — [EntityConfigurations.cs](../backend/src/BorrowingSystem.Api/Data/EntityConfigurations.cs)

Column lengths, unique indexes (asset tag, email, department code), foreign keys, and the composite primary key of `ReservationItems` (`ReservationId` + `EquipmentId`).

#### Tables

| Table | Main columns |
|---|---|
| `Departments` | Id, Code (unique), Name |
| `Users` | Id, FullName, Email (unique), PasswordHash, Role, DepartmentId, IsActive |
| `Equipment` | Id, AssetTag (unique), Name, Category, Description, Condition, IsActive |
| `Reservations` | Id, EventName, Venue, Purpose, StartAt, EndAt, Status, DepartmentId, RequestedById, + who/when for review, release, return |
| `ReservationItems` | ReservationId + EquipmentId (key), ReturnCondition |

```
Department 1 ──── * User
Department 1 ──── * Reservation * ──── 1 User (requested by)
Reservation 1 ──── * ReservationItem * ──── 1 Equipment
```

#### `DatabaseInitializer` — [DatabaseInitializer.cs](../backend/src/BorrowingSystem.Api/Data/DatabaseInitializer.cs)

Runs at startup: applies migrations (creates/updates tables), then, only if the tables are empty, adds the default departments, the admin account, and (in Development) a sample faculty account and sample equipment.

#### `MariaDbHistoryRepository` — [MariaDbHistoryRepository.cs](../backend/src/BorrowingSystem.Api/Data/MariaDbHistoryRepository.cs)

A compatibility fix. XAMPP's MariaDB 10.4 does not accept one SQL command the MySQL provider uses when applying migrations. This class wraps the provider's own class and replaces only that one command (**Decorator pattern**). It can be deleted after upgrading to MySQL 8 / MariaDB 10.5+.

### 4.4 Features

Every feature folder follows the same pattern:

- **`…Controller.cs`** — HTTP endpoints. Thin.
- **`…Service.cs`** — the work: load, check permission, call the domain, save.
- **`…Dtos.cs`** — the shapes of the JSON going in and out (**DTO** = Data Transfer Object). Entities are never sent directly, so internal fields such as `PasswordHash` cannot leak.

#### Auth — [Features/Auth](../backend/src/BorrowingSystem.Api/Features/Auth)

| File | Job |
|---|---|
| `AuthController.cs` | `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/change-password` |
| `TokenService.cs` | Creates the JWT after a successful login |
| `JwtOptions.cs` | Token settings + sets up token validation |
| `CurrentUser.cs` | `ICurrentUser`: gives services the caller's Id and role |
| `LoginThrottle.cs` | Locks sign-in after repeated wrong passwords |

**How login works** (`AuthController.Login`):

1. Ask `LoginThrottle` whether this email + computer is locked. If yes → `429` with the seconds left.
2. Find the user by email.
3. Check the password with `PasswordHasher.VerifyHashedPassword`. Passwords are never stored — only a one-way hash (PBKDF2).
4. Wrong → record a failure, return `401` with how many attempts remain.
5. Right → clear the failures, create a token, return it with the user's details.

**What a JWT is.** A signed string containing the user's Id, name and role, valid for 8 hours. The browser sends it on every request. The server checks the signature with its secret key, so the token cannot be forged or edited. The server does not need to store sessions.

**`ICurrentUser`** is an interface. Services depend on the interface, not on `HttpContext`, which keeps them independent of the web layer.

#### Inventory — [Features/Inventory](../backend/src/BorrowingSystem.Api/Features/Inventory)

`EquipmentService`:

| Method | What it does |
|---|---|
| `ListAsync(query)` | Equipment filtered by search / category / status, each with its live status |
| `GetAsync(id)` | One unit + bookings for the next 60 days + last 10 returns |
| `GetAvailabilityAsync(period)` | Every serviceable unit, marked free or booked for that period |
| `CreateAsync`, `UpdateAsync` | Add / edit (admin only); asset tag must be unique |
| `WithStatusAsync(units)` | Private helper: loads bookings once and resolves each unit's status |

#### Scheduling — [ScheduleService.cs](../backend/src/BorrowingSystem.Api/Features/Scheduling/ScheduleService.cs)

The only class that reads the equipment calendar. Both Inventory and Reservations use it, so the overlap logic exists in one place.

- `GetBookingsAsync(window, ...)` — returns an **`ILookup<int, Booking>`**: bookings grouped by equipment Id, so finding one unit's bookings is instant.
- `EnsureAvailableAsync(equipment, period)` — throws `ConflictException` if any unit is already booked.
- `FindUnitsStillOutAsync(...)` — units not yet returned from an earlier borrowing (blocks a release).

#### Reservations — [Features/Reservations](../backend/src/BorrowingSystem.Api/Features/Reservations)

`ReservationService` is the application service for the workflow. Its methods are short because the rules are in `Reservation`:

```csharp
public Task<ReservationDetailDto> ApproveAsync(int id, CancellationToken ct) =>
    UpdateAsync(id, adminOnly: true, (r, user) => r.Approve(user, Now), ct);
```

`UpdateAsync` is one shared helper: load the reservation → check permission → run the change → save → return the DTO. Approve, Reject and Cancel all reuse it and differ only in the one line that calls the domain.

Other points:

- **`ScheduleGate`** — a `SemaphoreSlim(1, 1)`. It makes "check the calendar, then save" happen one at a time, so two people clicking Reserve at the same instant cannot both get the same projector.
- **`EnsureCanView`** — faculty can only open their own reservations.
- **`AllowedActionsFor`** — works out which buttons this user may see (Approve, Reject, Cancel, Release, Receive) and sends that list to the frontend. The UI just shows what the server says, so the rules are written once.
- **`ListAsync`** — paged (`Skip` / `Take`), with filters; faculty are always limited to their own.

#### Dashboard, Departments, Users

- [DashboardController.cs](../backend/src/BorrowingSystem.Api/Features/Dashboard/DashboardController.cs) — counts by status, pending approvals, and three lists: to release, due back, overdue.
- [DepartmentsController.cs](../backend/src/BorrowingSystem.Api/Features/Departments/DepartmentsController.cs) — simple list / add / edit. No service class because there is no logic to put in one.
- [UserService.cs](../backend/src/BorrowingSystem.Api/Features/Users/UserService.cs) — admin creates and edits accounts and resets passwords. An admin cannot disable or demote their own account.

### 4.5 Security — [SecuritySetup.cs](../backend/src/BorrowingSystem.Api/Common/SecuritySetup.cs) and [LoginThrottle.cs](../backend/src/BorrowingSystem.Api/Features/Auth/LoginThrottle.cs)

| Protection | How |
|---|---|
| Passwords | Stored only as PBKDF2 hashes |
| Sessions | Signed JWT, expires after 8 hours |
| Roles | `[Authorize(Roles = "Admin")]` on admin endpoints; ownership checks in services |
| Brute force | 5 wrong passwords → locked 1 min, then 5 min, then 10 min (repeating) |
| Rate limiting | Max 20 login requests and 300 API requests per minute per computer |
| SQL injection | EF Core uses parameters for all values |
| Error leaks | 500 errors show a generic message; details go to the server log |
| Browser attacks | Security headers: no framing, no content sniffing, Content-Security-Policy |
| Account guessing | Same message and same response time for unknown email and wrong password |

**How the lockout works** (`LoginThrottle`): it keeps a dictionary keyed by `email|ip`. Each entry counts failures. On the 5th failure it sets `LockedUntil` using this table and moves to the next step:

```csharp
public static readonly TimeSpan[] LockoutSteps =
[ TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10) ];
```

A successful login removes the entry. The API returns `retryAfterSeconds`, and the login page counts it down live.

---

## 5. Frontend, part by part

Angular builds the screens from **components**. A component = a TypeScript class (data + behaviour) + an HTML template (what to show) + optional styles.

### 5.1 Startup

- [main.ts](../frontend/src/main.ts) starts the app.
- [app.config.ts](../frontend/src/app/app.config.ts) registers the router and the two HTTP interceptors.
- [app.routes.ts](../frontend/src/app/app.routes.ts) maps URLs to pages:

```ts
{ path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login.page')... },
{ path: '', component: ShellComponent, canActivate: [authGuard], children: [
    { path: 'equipment', loadComponent: ... },
    { path: 'admin/users', canActivate: [adminGuard], loadComponent: ... },
]}
```

`loadComponent` is **lazy loading** — a page's code is downloaded only when the user first opens it.

### 5.2 `core/` — services

| File | What it does |
|---|---|
| [models.ts](../frontend/src/app/core/models.ts) | TypeScript interfaces matching the API's JSON, so mistakes are caught at compile time |
| [api.ts](../frontend/src/app/core/api.ts) | `EquipmentApi`, `ReservationApi`, `DepartmentApi`, `UserApi`, `DashboardApi` — all HTTP calls live here |
| [auth.service.ts](../frontend/src/app/core/auth.service.ts) | Holds the session (token + user) in a signal and in `localStorage`; `login()`, `logout()`, `isAdmin` |
| [http.ts](../frontend/src/app/core/http.ts) | `authInterceptor` adds the token to each request; `errorInterceptor` logs out on 401 and shows a toast for other errors |
| [guards.ts](../frontend/src/app/core/guards.ts) | `authGuard` (must be logged in), `adminGuard` (must be admin), `guestGuard` (login page only when logged out) |
| [errors.ts](../frontend/src/app/core/errors.ts) | `errorMessage(err)` pulls the readable sentence out of an error response |
| [toast.service.ts](../frontend/src/app/core/toast.service.ts) | Small pop-up messages |
| [confirm.service.ts](../frontend/src/app/core/confirm.service.ts) | `await confirm.ask({...})` shows an "Are you sure?" dialog and returns `true` / `false` |
| [theme.service.ts](../frontend/src/app/core/theme.service.ts) | Light / Dark / System theme, remembered in `localStorage` |

**Important:** guards and hidden buttons are only for a clean user experience. The real security is on the server — every endpoint checks the token and role again.

### 5.3 Signals — how the screens update

A **signal** holds a value and tells Angular when it changes. A **computed** signal is calculated from other signals and updates automatically.

From [equipment-list.page.ts](../frontend/src/app/features/equipment/equipment-list.page.ts):

```ts
protected readonly all = signal<Equipment[] | null>(null);   // everything from the API
protected readonly search = signal('');                      // what the user typed

protected readonly filtered = computed(() =>                 // recalculated automatically
  (this.all() ?? []).filter(e => /* matches search, category, status */));

protected readonly pageRows = computed(() => paginate(this.filtered(), this.page(), this.pageSize));
```

When the user types in the search box, `search` changes → `filtered` recalculates → `pageRows` recalculates → the table redraws. No manual refresh code.

### 5.4 `shared/` — reusable pieces

| File | What it is |
|---|---|
| [icon.component.ts](../frontend/src/app/shared/icon.component.ts) | All icons as SVG paths in one lookup object |
| [status-badge.component.ts](../frontend/src/app/shared/status-badge.component.ts) | The coloured status pill; a lookup table maps each status to a label and colour |
| [modal.component.ts](../frontend/src/app/shared/modal.component.ts) | Wrapper around the browser's `<dialog>` element |
| [confirm-dialog.component.ts](../frontend/src/app/shared/confirm-dialog.component.ts) | The one "Are you sure?" dialog used everywhere |
| [pagination.component.ts](../frontend/src/app/shared/pagination.component.ts) | Page buttons with `…`; `buildPageItems(current, total)` decides which numbers to show |
| [schedule.pipe.ts](../frontend/src/app/shared/schedule.pipe.ts) | Formats a period: `Nov 12, 2026 · 8:00 AM – 12:00 PM` |
| [count-up.directive.ts](../frontend/src/app/shared/count-up.directive.ts) | Animates dashboard numbers |

### 5.5 `layout/shell.component` — the frame

[shell.component.ts](../frontend/src/app/layout/shell.component.ts) draws the sidebar and the top bar; the current page appears inside `<router-outlet />`. The menu is **data**, not hard-coded HTML:

```ts
const NAVIGATION: NavSection[] = [
  { title: 'Overview', items: [{ label: 'Dashboard', path: '/', icon: 'dashboard', exact: true }] },
  { title: 'Administration', items: [{ label: 'Users', path: '/admin/users', icon: 'users', adminOnly: true }] },
];
```

Items with `adminOnly: true` are filtered out for faculty. To add a menu item you add one line.

### 5.6 `features/` — the screens

| Screen | File | Notes |
|---|---|---|
| Login | [login.page.ts](../frontend/src/app/features/auth/login.page.ts) | Reactive form; handles `429` by starting a countdown; error clears when the user types |
| Dashboard | [dashboard.page.ts](../frontend/src/app/features/dashboard/dashboard.page.ts) | One API call; stats and panels are `computed` from it |
| Equipment list | [equipment-list.page.ts](../frontend/src/app/features/equipment/equipment-list.page.ts) | Loads once, filters and paginates in the browser |
| Equipment detail | [equipment-detail.page.ts](../frontend/src/app/features/equipment/equipment-detail.page.ts) | Upcoming bookings and history |
| Add/edit equipment | [equipment-form.dialog.ts](../frontend/src/app/features/equipment/equipment-form.dialog.ts) | One dialog for both add and edit |
| New reservation | [new-reservation.page.ts](../frontend/src/app/features/reservations/new-reservation.page.ts) | Described below |
| Reservation list | [reservation-list.page.ts](../frontend/src/app/features/reservations/reservation-list.page.ts) | Tabs by status; paging done by the server |
| Reservation detail | [reservation-detail.page.ts](../frontend/src/app/features/reservations/reservation-detail.page.ts) | Buttons come from `allowedActions`; timeline is a `computed` list |
| Departments, Users | [features/admin](../frontend/src/app/features/admin) | Table + dialog |

**New reservation, in more detail.** The page is driven by three signals:

1. `start` and `end` (the chosen time) → `period` is computed from them and validated.
2. Whenever `period` changes, the page waits 250 ms (`debounceTime`), then asks the API for availability. `switchMap` cancels an older request if a newer one starts.
3. `selected` is a `Set<number>` of picked equipment Ids. `groups` uses a `Map` to group units by category for display.

If the time changes and a picked unit is no longer free, it is removed from the selection and the user is told.

---

## 6. OOP concepts — and where to point

| Concept | Meaning | Where in this code |
|---|---|---|
| **Encapsulation** | Data is hidden; changes go through methods that check rules | Every domain class has `private set`. `Reservation.Status` can only change via `Approve()`, `Release()`, etc. |
| **Abstraction** | Depend on *what* something does, not *how* | `ICurrentUser` (interface) hides `HttpContext`; `TimeProvider` hides the system clock; services hide the database from controllers |
| **Inheritance** | A class reuses and extends another | `User`, `Department`, `Equipment`, `Reservation` inherit `Id` from `Entity`. `ConflictException` inherits from `DomainException` |
| **Polymorphism** | One call, different behaviour by type | `ApiExceptionHandler` handles any `Exception` and picks the status by its actual type. The test `ManualClock` overrides `TimeProvider.GetUtcNow()` |
| **Composition** | An object is built from other objects | A `Reservation` *has* `ReservationItem`s; each item *has* an `Equipment` |
| **Single responsibility** | One class, one job | Controller = HTTP. Service = orchestration. Domain = rules. `ScheduleService` = calendar only |
| **Dependency injection** | A class receives what it needs instead of creating it | Constructor parameters, e.g. `ReservationService(AppDbContext db, ScheduleService schedule, ICurrentUser currentUser, TimeProvider time)` |

**Design patterns used**

| Pattern | Where |
|---|---|
| State machine | `Reservation` and its `EnsureStatus` guard |
| Value object | `DateRange` |
| Aggregate root | `Reservation` owns `ReservationItem` |
| Factory method | `Reservation.Create(...)` — the only way to build a valid reservation |
| DTO | `*Dtos.cs` files |
| Decorator | `MariaDbHistoryRepository` |
| Interceptor / middleware | `authInterceptor`, `errorInterceptor`, `ApiExceptionHandler` |
| Unit of Work / Repository | EF Core's `DbContext` already is both, so no extra repository classes were written |

---

## 7. Data structures — and where to point

| Structure | Where | Why |
|---|---|---|
| **List** | `Reservation._items` (`List<ReservationItem>`) | Ordered collection of the items in a reservation |
| **Dictionary / hash map** | `Receive(..., IReadOnlyDictionary<int, ReturnCondition>)`; `LoginThrottle._entries` (`ConcurrentDictionary`) | Look something up by key in constant time |
| **Lookup (key → many values)** | `ScheduleService.GetBookingsAsync` returns `ILookup<int, Booking>` | All bookings of one unit without searching the whole list |
| **Set** | Angular: `selected = signal<ReadonlySet<number>>` ; `can()` in reservation detail | No duplicates; fast "is it in here?" |
| **Map** | Angular: `groups` in new-reservation groups units by category | Group items under a key while keeping order |
| **Array as a lookup table** | `LoginThrottle.LockoutSteps`; `NAVIGATION`; `TABS` | Behaviour driven by data instead of `if` chains |
| **Record / lookup object** | `EQUIPMENT_STATUS`, `RESERVATION_STATUS` in `status-badge.component.ts`; `PATHS` in `icon.component.ts` | Map a status / name to its label, colour or shape |
| **Enum** | `Enums.cs` | A fixed set of named values instead of loose strings |
| **Interval** | `DateRange` | Represents a time period and answers overlap questions |
| **Semaphore** | `ReservationService.ScheduleGate` | Lets only one booking check run at a time |

---

## 8. Tests

`backend/tests/BorrowingSystem.Tests` — 26 unit tests. They test the **domain** directly, with no database and no web server, which is why they run in under a second.

| File | What it proves |
|---|---|
| [DateRangeTests.cs](../backend/tests/BorrowingSystem.Tests/DateRangeTests.cs) | Overlap detection, back-to-back allowed, end must be after start |
| [ReservationTests.cs](../backend/tests/BorrowingSystem.Tests/ReservationTests.cs) | The full lifecycle; cannot release before approval; cannot cancel once borrowed; rejection needs a reason; damaged return flags the unit |
| [EquipmentStatusResolverTests.cs](../backend/tests/BorrowingSystem.Tests/EquipmentStatusResolverTests.cs) | Available / Reserved / Borrowed / UnderRepair / Retired rules |
| [LoginThrottleTests.cs](../backend/tests/BorrowingSystem.Tests/LoginThrottleTests.cs) | Lockout goes 1 → 5 → 10 → 10 minutes; success resets; another computer is not locked |

Run them with:

```bash
dotnet test backend
```

`LoginThrottleTests` uses a fake clock (`ManualClock`) so it can "wait 10 minutes" instantly. That only works because the code asks `TimeProvider` for the time instead of calling `DateTime.Now` directly.

---

## 9. Questions a reviewer may ask

**Q: What architecture did you use, and why?**
A modular monolith with feature folders and a domain layer. It is one project and one deployment, which suits a small system. Full Clean Architecture (four projects, CQRS, repositories) would add layers that only pass data along. Putting everything in controllers would scatter the borrowing rules. This is the middle: rules in the domain, features grouped in folders.

**Q: How do you prevent double booking?**
Three things. (1) `DateRange.Overlaps` defines a conflict. (2) `ScheduleService.EnsureAvailableAsync` checks every requested unit against existing bookings before saving. (3) `ScheduleGate` makes the check-then-save step run one at a time, so two simultaneous requests cannot both pass. Pending requests also block the calendar, so it is first come, first served.

**Q: Where are the business rules?**
In `Domain/`, mainly `Reservation.cs`. Controllers and services do not change status themselves — they call methods like `Approve()` and the method enforces the rule.

**Q: Why is the equipment status not a column in the database?**
A stored status can become wrong — for example if a reservation is cancelled and someone forgets to update the equipment. Here the status is calculated from the reservations that exist, by `EquipmentStatusResolver`, so it is always correct.

**Q: Why track each unit instead of a quantity?**
So the record says exactly which unit a department has, each unit keeps its own history, and one damaged unit can be pulled out without affecting the others.

**Q: How are passwords stored?**
As one-way PBKDF2 hashes made by ASP.NET's `PasswordHasher`. The real password is never saved and cannot be read back.

**Q: How does the server know who is logged in?**
After login the server issues a signed JWT. The browser sends it in the `Authorization` header on every request. The server verifies the signature and reads the user's Id and role from it.

**Q: What stops a faculty user from calling an admin endpoint directly?**
The server. Admin endpoints have `[Authorize(Roles = "Admin")]`, and services check ownership (`EnsureCanView`). Hiding buttons in Angular is only for convenience.

**Q: What if two faculty type a wrong password many times?**
The lockout is per email + computer. One person's failures do not lock another person or another computer.

**Q: Is it protected from SQL injection?**
Yes. All database access goes through EF Core, which sends user input as parameters, never as part of the SQL text.

**Q: Why no repository classes?**
EF Core's `DbContext` already is a repository and a unit of work. Wrapping it in another layer would add code without adding anything.

**Q: Why does the frontend ask the server which buttons to show?**
`allowedActions` is computed on the server from the same rules it enforces. The rules exist in one place, and the UI can never show a button the server would refuse.

**Q: What are the limitations?**
- The booking lock and the login lockout are in memory, so they work for one server process. Running several copies would need a database lock.
- The token is kept in `localStorage`; it expires after 8 hours.
- There are no e-mail reminders or calendar view yet.
- Tests cover the domain rules, not the HTTP endpoints or the Angular screens.

---

## 10. Glossary

| Term | Meaning |
|---|---|
| **API** | The backend's set of URLs the frontend calls |
| **Endpoint** | One URL + method, e.g. `POST /api/reservations` |
| **Controller** | Class whose methods are endpoints |
| **Service** | Class that carries out a use case |
| **Domain** | The business rules, independent of web and database |
| **Entity** | An object with its own identity (Id), stored in a table |
| **Value object** | An object defined only by its values, e.g. `DateRange` |
| **DTO** | The shape of data sent to or from the API |
| **EF Core** | Library that maps C# classes to database tables |
| **Migration** | Generated code that creates or changes tables |
| **Dependency injection** | The framework creates objects and passes them to constructors |
| **Middleware** | Code that every request passes through |
| **JWT** | Signed login token |
| **Component** | An Angular building block: class + template + styles |
| **Signal** | An Angular value that notifies the screen when it changes |
| **Interceptor** | Code that runs on every HTTP request the frontend makes |
| **Guard** | Code that decides whether a page may be opened |
| **Lazy loading** | Downloading a page's code only when it is first opened |
