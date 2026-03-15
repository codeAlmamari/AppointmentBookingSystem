# FlowCare — Queue & Appointment Booking System
### Rihal Codestacker 2026 — Backend Challenge

A secure, role-based appointment booking backend for **FlowCare**, a growing network of service branches across Oman.

> **Live API:** `https://appointmentbookingsystem-production-b878.up.railway.app`
> **Swagger UI:** `https://appointmentbookingsystem-production-b878.up.railway.app/swagger`
> **GitHub:** `https://github.com/codeAlmamari/AppointmentBookingSystem`

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 Web API |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 18 |
| Authentication | HTTP Basic Authentication |
| Containerization | Docker + Docker Compose |
| Deployment | Railway |

---

## Project Structure

```
AppointmentBookingSystem/
├── Controllers/
│   ├── PublicController.cs         # No-auth endpoints
│   ├── AuthController.cs           # Register + Login
│   ├── AppointmentsController.cs   # Booking, cancel, reschedule
│   ├── SlotsController.cs          # Slot management
│   ├── StaffController.cs          # Staff + service assignments
│   ├── CustomersController.cs      # Customer management
│   └── AdminController.cs          # Audit logs, settings, cleanup
├── Data/
│   └── AppDbContext.cs
├── DTOs/                            # Request/Response models
├── Helpers/
│   └── MappingProfile.cs           # AutoMapper
├── Middleware/
│   ├── BasicAuthMiddleware.cs
│   └── UserContext.cs
├── Migrations/                      # EF Core migration scripts
├── Models/
│   ├── BaseEntity.cs
│   └── Entities.cs
├── Services/
│   ├── AuditService.cs
│   ├── FileStorageService.cs
│   ├── RateLimitService.cs
│   ├── SeedImporter.cs
│   └── SlotCleanupBackgroundService.cs
├── seed/
│   └── example.json                 # Seed data
├── Dockerfile
├── docker-compose.yml
└── README.md
```

---

## Database Schema

```
Branch ──< ServiceType ──< Slot ──< Appointment >── Customer
                                          │
                                        Staff
Branch ──< User (staff/manager, BranchId FK)
User >──< ServiceType  (StaffServiceType join table)
AuditLog  (actorId, actorRole, actionType, entityType, entityId, branchId, timestamp, metadata)
AppSetting (key / value — stores soft_delete_retention_days)
```

### Entities

| Entity | Description |
|---|---|
| `Branch` | Service branches (Muscat, Suhar) |
| `User` | Unified users table — Admin, Manager, Staff, Customer |
| `ServiceType` | Services offered per branch |
| `StaffServiceType` | Join table — staff assigned to service types |
| `Slot` | Bookable time slots per branch/service/staff |
| `Appointment` | Customer bookings linked to a slot |
| `AuditLog` | Immutable log of all sensitive actions |
| `AppSetting` | Key-value config (e.g. retention period) |

---

## Roles & Permissions

| Role | Scope | Key Permissions |
|---|---|---|
| **Admin** | System-wide | Full access to everything |
| **Branch Manager** | Branch-scoped | Manage own branch, slots, staff, appointments |
| **Staff** | Branch-scoped | View schedule, update appointment status |
| **Customer** | Own data only | Book, cancel, reschedule own appointments |

---

## Seeded Credentials

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| Manager (Muscat) | `mgr_muscat` | `Manager@123` |
| Manager (Suhar) | `mgr_suhar` | `Manager@123` |
| Staff (Muscat) | `staff_muscat_1` | `Staff@123` |
| Staff (Muscat) | `staff_muscat_2` | `Staff@123` |
| Staff (Suhar) | `staff_suhar_1` | `Staff@123` |
| Staff (Suhar) | `staff_suhar_2` | `Staff@123` |
| Customer | `cust_ahmed` | `Customer@123` |
| Customer | `cust_fatima` | `Customer@123` |
| Customer | `cust_khalid` | `Customer@123` |

---

## Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string | see appsettings.json |
| `FileStorage__BasePath` | Directory for uploaded files | `uploads` |
| `RateLimit__MaxBookingsPerDay` | Max bookings per customer per day | `5` |
| `RateLimit__MaxReschedulesPerDay` | Max reschedules per customer per day | `3` |
| `SlotCleanup__IntervalHours` | Background cleanup interval | `1` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` |

---

## Setup Instructions

### Option 1 — Docker Compose (Recommended)

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/)

```bash
# 1. Clone the repository
git clone https://github.com/codeAlmamari/AppointmentBookingSystem.git
cd AppointmentBookingSystem

# 2. Start PostgreSQL + API together
docker-compose up --build

# 3. Open Swagger UI
# http://localhost:8080/swagger
```

The app will automatically:
- Start PostgreSQL
- Apply all database migrations
- Seed all data from `seed/example.json` (idempotent)

To stop:
```bash
docker-compose down
```

To stop and wipe all data:
```bash
docker-compose down -v
```

---

### Option 2 — Manual Setup (Without Docker)

**Prerequisites:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16](https://www.postgresql.org/download/)

```bash
# 1. Clone the repository
git clone https://github.com/codeAlmamari/AppointmentBookingSystem.git
cd AppointmentBookingSystem

# 2. Update appsettings.json with your PostgreSQL password
# ConnectionStrings:Default → set your password

# 3. Install EF Core tools
dotnet tool install --global dotnet-ef

# 4. Apply migrations
dotnet ef database update

# 5. Run (seeding happens automatically on startup)
dotnet run
```

---

## Seeding Instructions

Seeding runs **automatically on every startup** — no manual step needed.

- The app reads `seed/example.json` on startup
- It inserts only records that don't already exist (idempotent)
- Running the app multiple times will never duplicate data
- Seed covers: branches, service types, staff, managers, customers, slots, appointments, audit logs, and default app settings

---

## API Reference

### Authentication

All protected endpoints use **HTTP Basic Authentication**.

In Swagger: click **Authorize** → enter username and password.

In curl: add `-u username:password` to every request.

---

### Public Endpoints (No Auth)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/branches` | List all active branches |
| GET | `/api/branches/{branchId}/services` | List services for a branch |
| GET | `/api/branches/{branchId}/slots` | List available slots (optional `?date=` filter) |

---

### Auth

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a new customer (requires ID image upload) |
| POST | `/api/auth/login` | Validate credentials and return profile |

---

### Appointments (Customer+)

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/appointments` | Book a new appointment (optional attachment) |
| GET | `/api/appointments` | List appointments — scoped by role |
| GET | `/api/appointments/{id}` | Get appointment details |
| DELETE | `/api/appointments/{id}` | Cancel appointment |
| PUT | `/api/appointments/{id}/reschedule` | Reschedule to a different slot |
| PATCH | `/api/appointments/{id}/status` | Update status — Staff/Manager/Admin only |
| GET | `/api/appointments/{id}/attachment` | Download appointment attachment |

---

### Slots (Manager/Admin)

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/slots` | Create one or multiple slots |
| GET | `/api/slots` | List slots — scoped by role |
| GET | `/api/slots/{id}` | Get a single slot |
| PUT | `/api/slots/{id}` | Update a slot |
| DELETE | `/api/slots/{id}` | Soft-delete a slot |

---

### Staff (Manager/Admin)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/staff` | List staff — scoped by role |
| GET | `/api/staff/{id}` | Get a staff member |
| GET | `/api/staff/{id}/services` | Get assigned service types |
| POST | `/api/staff/{id}/services` | Assign service types |
| DELETE | `/api/staff/{id}/services/{serviceTypeId}` | Unassign a service type |

---

### Customers (Manager/Admin)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/customers` | List all customers |
| GET | `/api/customers/{id}` | Get a customer |
| GET | `/api/customers/{id}/appointments` | Get appointment history |
| GET | `/api/customers/{id}/id-image` | Download customer ID image (Admin only) |

---

### Admin Only

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/admin/audit-logs` | View audit logs (scoped by role) |
| GET | `/api/admin/audit-logs/export` | Export all audit logs as CSV |
| GET | `/api/admin/settings/retention` | Get soft-delete retention period |
| PUT | `/api/admin/settings/retention` | Update retention period (days) |
| DELETE | `/api/admin/slots/cleanup` | Manually trigger hard-delete cleanup |
| GET | `/api/admin/queue/{branchId}` | Get live queue status for a branch |

---

## Example curl Requests

```bash
# List all branches (public)
curl https://appointmentbookingsystem-production-b878.up.railway.app/api/branches

# Login
curl -u admin:Admin@123 -X POST \
  https://appointmentbookingsystem-production-b878.up.railway.app/api/auth/login

# List available slots for Muscat branch
curl https://appointmentbookingsystem-production-b878.up.railway.app/api/branches/br_muscat_001/slots

# Book an appointment
curl -u cust_ahmed:Customer@123 \
  -X POST https://appointmentbookingsystem-production-b878.up.railway.app/api/appointments \
  -H "Content-Type: multipart/form-data" \
  -F "slotId=slot_mus_002" \
  -F "serviceTypeId=svc_mus_001"

# List all appointments (admin sees all)
curl -u admin:Admin@123 \
  https://appointmentbookingsystem-production-b878.up.railway.app/api/appointments

# Update appointment status (staff)
curl -u staff_muscat_1:Staff@123 \
  -X PATCH https://appointmentbookingsystem-production-b878.up.railway.app/api/appointments/appt_001/status \
  -H "Content-Type: application/json" \
  -d '{"status": "CHECKED_IN"}'

# Export audit logs as CSV
curl -u admin:Admin@123 \
  https://appointmentbookingsystem-production-b878.up.railway.app/api/admin/audit-logs/export \
  -o audit_logs.csv

# Get live queue for Muscat branch
curl -u admin:Admin@123 \
  https://appointmentbookingsystem-production-b878.up.railway.app/api/admin/queue/br_muscat_001
```

---

## File Storage

| Type | Allowed Formats | Max Size | Who Can Access |
|---|---|---|---|
| Customer ID Image | JPEG, PNG, GIF, WEBP | 5 MB | Admin only |
| Appointment Attachment | JPEG, PNG, GIF, WEBP, PDF | 10 MB | Staff+, or the appointment's customer |

---

## Soft Delete

- Slots support soft delete — a `deleted_at` timestamp is set instead of removing the record
- Soft-deleted slots are hidden from all normal listing endpoints
- Admins can still see soft-deleted slots
- A configurable retention period (default: 30 days) controls when hard deletion occurs
- Hard deletion is handled by a **background service** that runs every hour automatically
- All soft and hard delete actions are recorded in the audit log
- Cleanup is idempotent — running it multiple times is safe

---

## Audit Log

Every sensitive action is recorded with:

| Field | Description |
|---|---|
| `actorId` | ID of the user who performed the action |
| `actorRole` | Role of the actor |
| `actionType` | e.g. `APPOINTMENT_BOOKED`, `SLOT_DELETED`, `SLOT_HARD_DELETED` |
| `entityType` | e.g. `APPOINTMENT`, `SLOT` |
| `entityId` | ID of the affected record |
| `branchId` | Branch context (for manager scoping) |
| `timestamp` | UTC timestamp |
| `metadata` | Optional JSON with extra context |

---

## Bonus Features Implemented

| # | Feature | Status |
|---|---|---|
| 1 | Pagination (`?page=&size=`) + Search (`?term=`) on listing APIs | ✅ |
| 2 | Real-time queue position per branch | ✅ |
| 3 | Rate limiting — max bookings and reschedules per day | ✅ |
| 4 | Background scheduling service for automatic slot cleanup | ✅ |
| 5 | Docker + docker-compose + Railway deployment | ✅ |

---

## Deployment (Railway)




The app auto-migrates and seeds on every startup.