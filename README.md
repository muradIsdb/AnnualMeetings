# IsDB Hospitality Platform

This repository contains the full-stack source code for the IsDB Hospitality Platform, a comprehensive VIP guest management system designed for the IsDB Annual Meetings 2026.

## Overview

The platform provides real-time tracking and management of VIP guests through their entire journey: airport arrival, ground transportation, hotel accommodation, and departure. It features role-based access control for different hospitality teams and a central control room for a complete operational overview.

## Features

- **Real-time Guest Tracking:** Monitor guest status from airport arrival to hotel check-in and departure.
- **Role-Based Dashboards:** Dedicated interfaces for Airport, Transport, Hotel, and Control Room teams.
- **Automated Data Sync:** Integrates with EventsAir for guest data and Aviationstack for flight status updates.
- **Checklist Management:** Ensure all protocol steps are completed for each guest arrival.
- **Vehicle Dispatch:** Assign vehicles to guests and track assignments.
- **Alert System:** Create and manage alerts for incidents or special requirements.
- **Public Departure Form:** Allows guests to submit their departure transport requests via a public link or QR code.

## Technology Stack

- **Backend:** ASP.NET Core 8, Clean Architecture, EF Core, MediatR (CQRS), Serilog
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, TanStack Query, Zustand
- **Database:** SQL Server (configurable)
- **APIs:** EventsAir (GraphQL), Aviationstack (REST)

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 22+ & pnpm
- SQL Server (or other compatible database)
- API Keys for EventsAir and Aviationstack

### Backend Setup

1.  **Clone the repository.**
2.  **Configure `appsettings.json`:**
    -   Navigate to `src/IsDB.Hospitality.API`.
    -   Open `appsettings.json` and fill in your `ConnectionStrings`, `Jwt` secret, `EventsAir` credentials, and `Aviationstack` API key.
3.  **Database Migration:**
    -   Open a terminal in the `src/IsDB.Hospitality.Infrastructure` directory.
    -   Run `dotnet ef database update --context AppDbContext`.
4.  **Run the backend:**
    -   Navigate to `src/IsDB.Hospitality.API`.
    -   Run `dotnet run`.
    -   The API will be available at `http://localhost:5000`.

### Frontend Setup

1.  **Install dependencies:**
    -   Navigate to the `frontend` directory.
    -   Run `pnpm install`.
2.  **Run the frontend:**
    -   Run `pnpm dev`.
    -   The application will be available at `http://localhost:5173`.

## Default Login Credentials

The database is seeded with default users for each role. The password for all users is `Password123!`.

- **Administrator:** `admin@isdb.org`
- **Control Room:** `control@isdb.org`
- **Airport Specialist:** `airport@isdb.org`
- **Transport Specialist:** `transport@isdb.org`
- **Hotel Specialist:** `hotel@isdb.org`
