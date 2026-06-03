# TaskManager Mobile (.NET MAUI)

Native mobile client for the TaskManager API (same backend as the Blazor web app).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MAUI workload: `dotnet workload install maui`
- Visual Studio 2022 with **.NET Multi-platform App UI development** workload (optional)

## Project structure

```
TaskManager.Mobile/
├── Configuration/     # API base URL per platform
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

### API URL by platform (debug)

| Platform | Default base URL |
|----------|------------------|
| Android emulator | `http://10.0.2.2:5018/` |
| iOS simulator / Mac | `http://localhost:5018/` |
| Windows | `https://localhost:7294/` |

**Physical device:** use your PC’s LAN IP, e.g. `http://192.168.1.10:5018/`, and run the API listening on `0.0.0.0`.

Override at runtime:

```csharp
Preferences.Default.Set("api_base_url", "http://192.168.1.10:5018/");
```

### Default login (seed data)

- Username: `admin`
- Password: `Admin@123`

## Features

- JWT login / logout (SecureStorage)
- Automatic token refresh on 401
- Role-based UI (Projects tab hidden for **User** role)
- Dashboard, tasks list, task status update, projects (Admin/Manager), profile

## Solution

Added to `src/TaskManager/TaskManager.sln` as **TaskManager.Mobile**.
