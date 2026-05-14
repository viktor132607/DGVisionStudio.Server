# DG Vision Studio Server

ASP.NET Core Web API for DG Vision Studio, a photography portfolio and client gallery platform.

## Tech stack

- ASP.NET Core
- Entity Framework Core
- ASP.NET Identity
- PostgreSQL
- Docker
- Serilog
- Swagger / OpenAPI
- ImageSharp

## Main areas

- Authentication and user accounts
- Admin portfolio management
- Public portfolio API
- Client gallery access
- File upload/storage services
- Contact/email service
- Audit logging
- Expired gallery cleanup background service

## Project structure

```text
DGVisionStudio.Api             Web API entry point, controllers, middleware
DGVisionStudio.Application     Application interfaces and service contracts
DGVisionStudio.Domain          Domain entities
DGVisionStudio.Infrastructure  EF Core, Identity, services, storage, seeding
```

## Local setup

Restore and run:

```bash
dotnet restore
dotnet run --project DGVisionStudio.Api
```

Default local API URL:

```text
http://localhost:10000
```

Swagger is available in Development mode:

```text
http://localhost:10000/swagger
```

## Required configuration

Set these values in local user secrets, environment variables, or deployment settings:

```text
ConnectionStrings__DefaultConnection
Frontend__Url
Api__Url
Resend__ApiKey
Resend__FromEmail
Resend__FromName
Resend__OwnerEmail
Seed__AdminPassword
Seed__PrimaryAdminEmail
Seed__SecondaryAdminEmail
```

## Docker

Build:

```bash
docker build -t dgvisionstudio-server .
```

Run:

```bash
docker run --rm -p 10000:10000 dgvisionstudio-server
```

## Deployment

The repository includes Render deployment configuration in `render.yaml`.

## Related repository

Frontend client:

```text
https://github.com/viktor132607/DGVisionStudio.client
```
