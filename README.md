# IsDB Annual Meetings Hospitality System

The IsDB Annual Meetings Hospitality System is a comprehensive, real-time logistics and guest management platform designed to orchestrate the end-to-end journey of VIPs, delegates, and officials attending the Islamic Development Bank's Annual Meetings.

It bridges the gap between registration data (sourced from EventsAir) and on-the-ground operational execution across airport reception, fleet management, and hotel coordination teams.

## Technical Documentation

The complete technical documentation, architecture diagrams, API specs, and database schema are maintained in the **Technical Handover Package**.

👉 **[View the Technical Handover Package (v4.2.0)](docs/architecture/Technical_Handover_Package_v4.2.0.md)**

## Tech Stack

- **Backend:** ASP.NET Core 8.0, Entity Framework Core, MediatR (CQRS), SignalR
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, Zustand, TanStack React Query
- **Database:** PostgreSQL
- **Integrations:** EventsAir (Guest Data), Aviationstack (Flight Tracking)

## Local Development Setup

### Prerequisites
- .NET 8.0 SDK
- Node.js 20+ and `pnpm`
- SQLite (for local development)

### Backend
```bash
cd src/IsDB.Hospitality.API
dotnet restore
dotnet run
```
The API will start on `http://localhost:5000` and automatically apply SQLite migrations on startup. Swagger UI is available at `/swagger`.

### Frontend
```bash
cd frontend
pnpm install
pnpm dev
```
The React SPA will start on `http://localhost:5173` and proxy API requests to the backend.

## Deployment

The application is deployed via a multi-stage `Dockerfile` managed by Railway. Pushing to the `master` branch triggers an automatic build and deployment.

For detailed deployment architecture, refer to Section 2.8 of the Handover Package.
