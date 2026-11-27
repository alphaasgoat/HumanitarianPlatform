```markdown
# Humanitarian Aid & Disaster Response Coordination Platform

A full-stack crisis coordination and emergency aid management web application built with **.NET 8 (Clean Architecture)**,
**React 18 (Vite)**, and **Microsoft SQL Server (LocalDB)**.
The platform streamlines citizen assistance intake, AI-assisted request triage, duplicate detection, resource inventory allocation, shelter capacity tracking, and volunteer task dispatch.

---

## Key Features
* **Multi-Role Access Control (RBAC):** Supports `Citizen`, `Coordinator`, `LogisticsOfficer`, `ShelterManager`, `Volunteer`, and `Admin` roles secured with JWT Bearer authentication and BCrypt password hashing.
* **AI Decision Support & Human-in-the-Loop Triage:** Automatically computes urgency scores, recommends priority tiers (`Low`, `Medium`, `High`, `Critical`), estimates relief supplies, and detects spatio-temporal duplicates while preserving coordinator override controls.
* **Resource Inventory & Allocation:** Tracks relief supplies, flags low-stock thresholds, and performs atomic multi-constraint allocations.
* **Shelter & Volunteer Coordination:** Tracks real-time shelter bed capacities and matches verified volunteers to field tasks based on skills and proximity.
* **Geospatial Mapping & Multi-Channel Notifications:** Integrates OpenStreetMap (Nominatim) geocoding, OSRM routing, interactive Leaflet maps, and asynchronous in-app/email/SMS notifications.

---

## Step 1: Check Installed Versions & Install Prerequisites

Because this project targets **.NET 8.0** (enforced via `global.json`), newer SDKs alone (such as .NET 9 or .NET 10) will not build the solution unless the **.NET 8.0 SDK** is installed side-by-side.

### 1. Check Current Versions on Your Machine
Run these commands in PowerShell / Terminal to check what is already installed:
```bash
dotnet --list-sdks
node -v
npm -v
sqllocaldb info

```

* Ensure **`8.0.xxx`** appears under `dotnet --list-sdks`.
* Ensure **Node.js `v18+**` is installed.
* Ensure **SQL Server LocalDB** (`MSSQLLocalDB`) is available.

### 2. Install Missing Prerequisites via CLI (Windows `winget`)

If any of the above tools are missing, run the corresponding command in **PowerShell (Run as Administrator)**:

* **Install .NET 8.0 SDK:**
```powershell
winget install Microsoft.DotNet.SDK.8

```


* **Install Node.js LTS:**
```powershell
winget install OpenJS.NodeJS.LTS

```


* **Install SQL Server Express LocalDB:**
```powershell
winget install Microsoft.SQLServer.2022.Express

```



*(After installing via CLI, close and reopen your terminal so the new PATH variables take effect).*

### 3. Alternative: Manual Direct Downloads (If CLI Commands Fail or on macOS/Linux)

If `winget` is unavailable on your system or fails, download and install the tools manually from the official links below:

* **.NET 8.0 SDK:** [Download .NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* **Node.js (LTS):** [Download Node.js](https://nodejs.org/en/download)
* **SQL Server LocalDB:** [Download SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) *(Select **Custom** or **Download Media -> LocalDB** during setup)*.

---

## Step 2: Clone & Run the Application

### 1. Clone the Repository

```bash
git clone (https://github.com/alphaasgoat/HumanitarianPlatform.git)
cd HumanitarianPlatform
```
*(Note: If you downloaded and extracted the repository as a ZIP file, navigate into the extracted inner directory (cd HumanitarianPlatform-main) until you see the src, frontend, docs, and HumanitarianPlatform.sln files)*.

### 2. Run the Backend API (.NET 8)
From the repository root folder (where `HumanitarianPlatform.sln` is located), run the API using the HTTPS launch profile. The backend uses SQL Server LocalDB (`Server=(localdb)\MSSQLLocalDB;Database=HumanitarianDb`) by default and **automatically applies EF Core migrations and seeds initial demo data** on startup:

```powershell
dotnet restore
dotnet run --project src\Humanitarian.Api --launch-profile https
Swagger API Documentation: Open the HTTPS URL displayed in your terminal followed by /swagger (e.g., https://localhost:7001/swagger) to inspect and test endpoints.

(Note: If you prefer using a full SQL Server instance instead of LocalDB, update the "Default" connection string in src/Humanitarian.Api/appsettings.json).
```

### 3. Run the Frontend Application (React + Vite)

Open a **new** terminal window, navigate to the `frontend` directory, install `node_modules`, and start the development server:

```bash
cd frontend
npm install
npm run dev

```

* **Frontend Application URL:** `http://localhost:5173` *(click it while holding the CTRL key )*

---

## Running Automated Tests

To run the 221 backend xUnit unit tests from the repository root *(where you see the src, frontend, docs, and HumanitarianPlatform.sln files).)*:

```bash
dotnet test

```

To run frontend Vitest unit tests:

```bash
cd frontend
npm test
npm run dev *(it will open http://localhost:5173)*

```

---

## Project Structure

```text
HumanitarianPlatform/
├── src/
│   ├── Humanitarian.Domain/          # Core entities, enums, and domain rules
│   ├── Humanitarian.Application/     # Business services, AI triage engine, DTOs
│   ├── Humanitarian.Infrastructure/  # EF Core DbContext, migrations, Nominatim/OSRM clients
│   └── Humanitarian.Api/             # REST controllers, JWT auth, background workers
├── frontend/                         # React 18 + Vite SPA & Leaflet map components
├── tests/Humanitarian.Tests/         # xUnit automated test suite
├── docs/                             # Architecture and module documentation
└── global.json                       # Pins .NET 8.0 SDK version

```

---

## Team Members

* **Peheliya Ravindu** (`alphaasgoat`) – Group Leader
* **Irushi Costha** (`IrushiCostha`)
* **Kaushani Botheju** (`KaushaniBotheju`)
* **Amindu** (`dev-amindu`)

```

```
