# LCC Equipment Borrowing System

Reservation, borrowing and return tracking for school equipment (speakers, microphones, projectors, extension cords…) at Legacy College of Compostela.

- **Backend:** ASP.NET Core on .NET 10, EF Core 10, MySQL/MariaDB
- **Frontend:** Angular 20 (standalone components, signals)
- **Design notes:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

```
backend/
  src/BorrowingSystem.Api/      Web API (feature folders + domain model)
  tests/BorrowingSystem.Tests/  Unit tests for the business rules
frontend/                       Angular app
docs/                           Architecture and design decisions
```

## Running locally

Requirements: .NET 10 SDK, Node 20+, and MySQL or MariaDB (XAMPP works — start MySQL from the XAMPP control panel).

1. **API** — the database `lcc_borrowing` is created and migrated on first start, and sample equipment plus two accounts are seeded in Development. The connection string and the seed passwords are in `backend/src/BorrowingSystem.Api/appsettings.Development.json`.

   ```bash
   dotnet run --project backend/src/BorrowingSystem.Api --launch-profile http
   ```

   The API listens on http://localhost:5073.

2. **Frontend** — proxies `/api` to the API.

   ```bash
   npm --prefix frontend install
   ```

   ```bash
   npm --prefix frontend start
   ```

   Open http://localhost:4200 and sign in as `admin@lcc.edu.ph` (custodian) or `faculty@lcc.edu.ph` (faculty).

3. **Tests**

   ```bash
   dotnet test backend
   ```

## Deploying

1. `npm --prefix frontend run build`, then copy `frontend/dist/frontend/browser/*` into `backend/src/BorrowingSystem.Api/wwwroot/`.
2. `dotnet publish backend/src/BorrowingSystem.Api -c Release -o publish`
3. On the server, set these as environment variables (never commit them):
   - `ConnectionStrings__Default`
   - `Jwt__SigningKey` (32+ random characters)
   - `Seed__AdminPassword` (only needed for the very first start)
4. Run `publish/BorrowingSystem.Api.exe` behind IIS or as a Windows service, and sign in as `admin@lcc.edu.ph` to change the password and add real users, departments and equipment.

## Database changes

```bash
dotnet ef migrations add <Name> -p backend/src/BorrowingSystem.Api -o Data/Migrations
```

Migrations are applied automatically when the API starts.
