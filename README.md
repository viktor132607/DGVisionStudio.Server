# DG Vision Studio Server

[![Server CI](https://github.com/viktor132607/DGVisionStudio.Server/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/viktor132607/DGVisionStudio.Server/actions/workflows/ci.yml)
[![Backend tests](https://github.com/viktor132607/DGVisionStudio.Server/actions/workflows/backend-tests.yml/badge.svg?branch=main)](https://github.com/viktor132607/DGVisionStudio.Server/actions/workflows/backend-tests.yml)

ASP.NET Core Web API for DG Vision Studio, a photography portfolio and client gallery platform.

## Tech stack

- ASP.NET Core
- Entity Framework Core
- ASP.NET Identity
- PostgreSQL
- Docker
- Serilog
- Swagger / OpenAPI / Scalar
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
DGVisionStudio.Tests           Unit tests and controller tests
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

Scalar API reference is also available in Development mode:

```text
http://localhost:10000/scalar/v1
```

## Health endpoints

Simple application ping:

```text
GET /api/health
```

Readiness check with database connectivity:

```text
GET /api/health/ready
```

`/api/health/ready` returns `503 Service Unavailable` when the API cannot connect to the configured PostgreSQL database.

## Required configuration

Set these values in local user secrets, environment variables, or deployment settings:

```text
DATABASE_URL
POSTGRES_URL
ConnectionStrings__DefaultConnection
ConnectionStrings__Postgres
Frontend__Url
Frontend__AdditionalOrigins__0
Api__Url
Resend__ApiKey
Resend__FromEmail
Resend__FromName
Resend__OwnerEmail
Seed__AdminPassword
Seed__PrimaryAdminEmail
Seed__SecondaryAdminEmail
Storage__Provider
Upload__MaxFileSizeBytes
Upload__MaxFilesPerRequest
```

Database configuration is resolved in this order:

1. `DATABASE_URL`
2. `POSTGRES_URL`
3. `ConnectionStrings:DefaultConnection`
4. `ConnectionStrings:Postgres`

For non-development environments, localhost database hosts are rejected so production does not accidentally deploy with a local placeholder connection string.

## Storage provider

The API supports two storage modes:

```text
Storage__Provider=FileSystem
Storage__Provider=Cloudinary
```

If `Storage__Provider` is empty or `FileSystem`, uploaded files are stored by the local file storage service. If it is `Cloudinary`, the Cloudinary-backed storage service is used.

## Upload limits

Upload limits are configurable:

```text
Upload__MaxFileSizeBytes=20971520
Upload__MaxFilesPerRequest=100
```

The effective request limit is calculated as:

```text
Upload__MaxFileSizeBytes * Upload__MaxFilesPerRequest
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

For Render, configure the database through `DATABASE_URL` or `POSTGRES_URL`, and use `/api/health/ready` as the readiness endpoint when database connectivity should be verified.

## Related repository

Frontend client:

```text
https://github.com/viktor132607/DGVisionStudio.client
```
