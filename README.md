# FlowCare — Queue & Appointment Booking System

A secure, role-based appointment booking backend built for **Rihal Codestacker 2026**.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 Web API |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 18 |
| Auth | HTTP Basic Authentication |
| Containerization | Docker + Docker Compose |

---

## Features

- Role-based access control (Admin, Branch Manager, Staff, Customer)
- Appointment booking, rescheduling, and cancellation
- Slot management with soft delete and retention policy
- File uploads (customer ID images + appointment attachments)
- Full audit logging with CSV export
- Real-time queue position per branch
- Rate limiting (bookings and reschedules per day)
- Background cleanup service for expired soft-deleted slots
- Idempotent database seeding from `seed/example.json`

---

## Roles & Credentials (Seeded)

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| Branch Manager (Muscat) | `mgr_muscat` | `Manager@123` |
| Branch Manager (Suhar) | `mgr_suhar` | `Manager@123` |
| Staff (Muscat) | `staff_muscat_1` | `Staff@123` |
| Staff (Suhar) | `staff_suhar_1` | `Staff@123` |
| Customer | `cust_ahmed` | `Customer@123` |

---

## Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string | see appsettings.json |
| `FileStorage__BasePath` | Directory for uploaded files | `uploads` |
| `RateLimit__MaxBookingsPerDay` | Max bookings per customer per day | `5` |
| `RateLimit__MaxReschedulesPerDay` | Max reschedules per customer per day | `3` |
| `SlotCleanup__IntervalHours` | How often the cleanup service runs | `1` |
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |

---

## Quick Start with Docker Compose (Recommended)

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/codeAlmamari/AppointmentBookingSystem.git
cd AppointmentBookingSystem

# 2. Set a custom DB password
export DB_PASSWORD=yourpassword   # Linux/Mac
set DB_PASSWORD=yourpassword      # Windows

# 3. Start everything
docker-compose up --build

# 4. Open Swagger UI
# http://localhost:8080/swagger
```

The app will automatically:
- Start PostgreSQL
- Apply database migrations
- Seed all data from `seed/example.json`

To stop:
```bash
docker-compose down
```

To stop and remove all data:
```bash
docker-compose down -v
```

---

## Manual Setup (Without Docker)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 18](https://www.postgresql.org/download/)

### Steps

```bash
# 1. Clone the repo
git clone https://github.com/codeAlmamari/AppointmentBookingSystem.git
cd AppointmentBookingSystem

# 2. Update connection string in appsettings.json
# Set your PostgreSQL password in ConnectionStrings:Default

# 3. Install EF Core tools
dotnet tool install --global dotnet-ef

# 4. Apply migrations
dotnet ef database update

# 5. Run the app (seeding happens automatically on startup)
dotnet run
```

---

## API Documentation

Swagger UI is available at:
```
http://localhost:8080/swagger
```

### Authentication

All protected endpoints use **HTTP Basic Authentication**.

In Swagger: click **Authorize** and enter `username:password`.

In curl:
```bash
curl -u admin:Admin@123 http://localhost:8080/api/appointments
```

---

## API Overview

### Public (No Auth)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/branches` | List all branches |
| GET | `/api/branches/{id}/services` | List services for a branch |
| GET | `/api/branches/{id}/slots` | List available slots |

### Auth
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a new customer |
| POST | `/api/auth/login` | Login and get profile |

### Appointments
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/appointments` | Book an appointment |
| GET | `/api/appointments` | List appointments (scoped by role) |
| GET | `/api/appointments/{id}` | Get appointment details |
| DELETE | `/api/appointments/{id}` | Cancel appointment |
| PUT | `/api/appointments/{id}/reschedule` | Reschedule appointment |
| PATCH | `/api/appointments/{id}/status` | Update status (staff+) |
| GET | `/api/appointments/{id}/attachment` | Download attachment |

### Slots (Manager/Admin)
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/slots` | Create slots (single or bulk) |
| GET | `/api/slots` | List slots |
| PUT | `/api/slots/{id}` | Update a slot |
| DELETE | `/api/slots/{id}` | Soft-delete a slot |

### Admin
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/admin/audit-logs` | View audit logs |
| GET | `/api/admin/audit-logs/export` | Export audit logs as CSV |
| GET | `/api/admin/settings/retention` | Get retention period |
| PUT | `/api/admin/settings/retention` | Update retention period |
| DELETE | `/api/admin/slots/cleanup` | Manually trigger hard delete cleanup |
| GET | `/api/admin/queue/{branchId}` | Get live queue for a branch |

---

## Example curl Requests

```bash
# List branches (public)
curl http://localhost:8080/api/branches

# Login
curl -u admin:Admin@123 -X POST http://localhost:8080/api/auth/login

# Book an appointment
curl -u cust_ahmed:Customer@123 \
  -X POST http://localhost:8080/api/appointments \
  -H "Content-Type: multipart/form-data" \
  -F "slotId=slot_mus_002" \
  -F "serviceTypeId=svc_mus_001"

# List all appointments (admin)
curl -u admin:Admin@123 http://localhost:8080/api/appointments

# Export audit logs as CSV
curl -u admin:Admin@123 http://localhost:8080/api/admin/audit-logs/export -o logs.csv
```

---

## Deployment to Railway

1. Push your code to GitHub
2. Go to [railway.app](https://railway.app) and sign in with GitHub
3. Click **"New Project"** → **"Deploy from GitHub repo"**
4. Select your repository
5. Railway will detect the Dockerfile automatically
6. Add a **PostgreSQL** plugin from the Railway dashboard
7. Set the environment variable:
   ```
   ConnectionStrings__Default=Host=YOUR_RAILWAY_DB_HOST;Port=5432;Database=flowcare;Username=postgres;Password=YOUR_RAILWAY_DB_PASSWORD
   ```
8. Deploy — your live URL will appear in the dashboard

---

## Database Schema

Key entities and relationships:

```
Branch ──< ServiceType ──< Slot ──< Appointment >── Customer
                                         │
                                       Staff
Branch ──< Staff (BranchId FK)
Staff >──< ServiceType (StaffServiceType join table)
AuditLog (actor, action, entity, timestamp, metadata)
AppSetting (key-value store for retention period etc.)
```

---

## Project Structure

```
AppointmentBookingSystem/
├── Controllers/         # 7 controllers (Public, Auth, Appointments, Slots, Staff, Customers, Admin)
├── Data/                # AppDbContext + EF configuration
├── DTOs/                # Request/Response models
├── Helpers/             # AutoMapper profile
├── Middleware/          # BasicAuth + UserContext
├── Migrations/          # EF Core migrations
├── Models/              # Entity models
├── Services/            # AuditService, FileStorageService, RateLimitService, SeedImporter, SlotCleanupBackgroundService
├── seed/
│   └── example.json     # Seed data
├── Dockerfile
├── docker-compose.yml
└── README.md
```
