# TaskManager Mobile (.NET MAUI)

Native mobile client for the TaskManager API (same backend as the Blazor web app).

**Roadmap:** [`docs/MOBILE_IMPLEMENTATION_PLAN.md`](../../../docs/MOBILE_IMPLEMENTATION_PLAN.md)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MAUI workload: `dotnet workload install maui`
- Visual Studio 2022 with **.NET Multi-platform App UI development** workload (optional)

## Project structure

```
TaskManager.Mobile/
├── Configuration/     # API base URL (DEBUG vs Release)
├── Helpers/           # AppRoles (matches API JWT roles)
├── Services/          # Auth, API, secure token storage
├── ViewModels/        # MVVM screens
├── Views/             # XAML pages
└── Platforms/         # Android, iOS, Mac Catalyst, Windows
```

References **TaskManager.Shared** for DTOs (`LoginDto`, `TaskDto`, etc.).

## Run the API first

From `src/TaskManager/TaskManager/TaskManager`:

```bash
dotnet run --launch-profile https
```

Default URLs: `https://localhost:7294` and `http://localhost:5018`

## Run the mobile app

```bash
cd src/TaskManager/TaskManager.Mobile
dotnet build -t:Run -f net10.0-android
```

Or open `TaskManager.sln` in Visual Studio and set **TaskManager.Mobile** as startup project.

### API URL

| Build | Platform | Default base URL |
|-------|----------|------------------|
| **DEBUG** | Android emulator | `http://10.0.2.2:5018/` |
| **DEBUG** | iOS simulator / Mac | `http://localhost:5018/` |
| **DEBUG** | Windows | `https://localhost:7294/` |
| **Release** | All | `https://taskmanager-app-plt1.onrender.com/` |

**Physical device (debug):** use your PC’s LAN IP, e.g. `http://192.168.1.10:5018/`, and run the API listening on `0.0.0.0`.

Override at runtime:

```csharp
Preferences.Default.Set("api_base_url", "http://192.168.1.10:5018/");
// or production:
Preferences.Default.Set("api_base_url", "https://taskmanager-app-plt1.onrender.com/");
```

### Default login (seed data)

- Username: `admin`
- Password: `Admin@123`

API roles: `OrganizationAdmin`, `Manager`, `User`, `SuperAdmin` (not the legacy string `Admin`).

## Features (current)

- JWT login / logout (SecureStorage) + automatic token refresh on 401
- Register workspace, forgot/reset password, accept invite
- Onboarding checklist for new org admins (name → invite → first project)
- Role-based UI (`OrganizationAdmin` / `Manager` see Projects, Users, Templates)
- Dashboard, tasks list with status/project filters
- **Board** (Kanban columns, ←/→ status move; gated by `board_view`)
- **Calendar** (month due-date grid; gated by `calendar_view`)
- **Templates** (apply task template → create task)
- Task detail: checklist, comments (`@username`), history
- Projects, profile

## Solution

Added to `src/TaskManager/TaskManager.sln` as **TaskManager.Mobile**.
