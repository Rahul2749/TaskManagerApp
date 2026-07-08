# TaskManager – User Guide

A multi-tenant task & project management platform with a web app (Blazor), a REST API (ASP.NET Core), and a native mobile app (.NET MAUI). This guide explains how to run the software and how each type of user works with it day to day.

---

## 1. What the application does

TaskManager lets organizations plan projects, break them into tasks and subtasks, assign work to team members, track progress through a status workflow, and report on completion. It supports **multiple organizations (tenants)** on a single deployment, with data isolation between them.

### Key capabilities
- **Multi-tenant organizations** – each organization only sees its own users, projects, and tasks.
- **Role-based access** – four roles with different permissions (see below).
- **Projects & tasks** – create projects, assign a manager, create tasks, assign to users.
- **Task workflow** – statuses from `NotAssigned` → `Closed`, with priorities and due dates.
- **Subtasks** – checklist items inside a task.
- **Comments** – discussion thread on each task (with optional replies).
- **Tags** – colored labels per organization that can be attached to tasks.
- **Attachments & watchers** – files on tasks and users who follow a task.
- **Dashboards & reports** – per-role dashboards and reporting pages.
- **JWT authentication** – secure login with refresh tokens; password reset flow.

---

## 2. Roles & permissions

| Role | Scope | Can do |
|------|-------|--------|
| **SuperAdmin** | Whole platform (no organization) | Manage all organizations, view all users, view platform-wide reports |
| **OrganizationAdmin** | One organization | Manage managers, view organization dashboard & reports |
| **Manager** | Projects they manage | Create/edit projects & tasks, manage team members, map users to projects |
| **User** | Their own tasks | View and update the tasks assigned to them |

Roles are defined in `Roles.cs`. Content management (creating projects/tasks) is limited to SuperAdmin, OrganizationAdmin, and Manager.

---

## 3. Getting started (running the software)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (the connection string is configured in `appsettings.json`)
- For mobile: MAUI workloads (`dotnet workload install maui`) and, for Android, an emulator or device

### 3.1 Run the backend + web app

From the repository root:

```bash
cd src/TaskManager/TaskManager/TaskManager
dotnet run --launch-profile https
```

Default URLs:
- Web app / API (HTTPS): `https://localhost:7294`
- Web app / API (HTTP): `http://localhost:5018`
- Swagger API explorer (Development only): `https://localhost:7294/swagger`

On first startup the database is migrated and seeded automatically (see default logins below).

> The database connection string lives in `src/TaskManager/TaskManager/TaskManager/appsettings.json` under `ConnectionStrings:DefaultConnection`. Point it at your own SQL Server if needed.

### 3.2 Run the mobile app

```bash
cd src/TaskManager/TaskManager.Mobile
dotnet build -t:Run -f net10.0-android
```

Or open `src/TaskManager/TaskManager.sln` in Visual Studio 2022 and set **TaskManager.Mobile** as the startup project.

API base URL per platform (debug):

| Platform | Default base URL |
|----------|------------------|
| Android emulator | `http://10.0.2.2:5018/` |
| iOS simulator / Mac | `http://localhost:5018/` |
| Windows | `https://localhost:7294/` |

You can override the API URL at runtime by setting the `api_base_url` preference (see `Configuration/ApiSettings.cs`).

---

## 4. Default logins (seed data)

These accounts are created automatically on first run. **Change these passwords after first login.**

| Role | Username | Password |
|------|----------|----------|
| Platform SuperAdmin | `superadmin` | `Admin@123` |
| Organization Admin (Acme Corp) | `orgadmin` | `Admin@123` |
| Manager (Acme Corp) | `manager` | `Manager@123` |
| User (Acme Corp) | `user` | `User@123` |

The seeded demo organization is **Acme Corporation** (slug `acme`).

---

## 5. Signing in

1. Open the web app (`https://localhost:7294`) or launch the mobile app.
2. Enter your **username** and **password** on the Login screen.
3. On success you are routed to the dashboard for your role.
4. Use **Logout** (bottom of the sidebar on web, profile screen on mobile) to sign out.

**Forgot your password?** Use the *Forgot Password* link on the login page to start the reset flow, then set a new password on the *Reset Password* page.

---

## 6. Using the web app by role

The left sidebar changes based on your role. The account/profile area is at the bottom of the sidebar.

### 6.1 SuperAdmin
- **Dashboard** (`superadmin/dashboard`) – platform-wide overview.
- **Organizations** (`superadmin/organizations`) – create and manage tenant organizations.
- **All Users** (`superadmin/users`) – view users across every organization.
- **Reports** (`superadmin/reports`) – platform-wide reporting.

### 6.2 Organization Admin
- **Dashboard** (`admin/dashboard`) – overview for your organization.
- **Managers** (`admin/managers`) – add/manage the managers in your organization.
- **Reports** (`admin/reports`) – organization-level reporting.

### 6.3 Manager
- **Dashboard** (`manager/dashboard`) – project and task summary.
- **Projects** (`manager/projects`) – create/edit projects, set status and dates.
- **Tasks** (`manager/tasks`) – create tasks, assign to team members, set priority/due dates.
- **Team Members** (`manager/users`) – manage users on your team.
- **Project Mapping** (`manager/project-users`) – map which users belong to which projects.

### 6.4 User
- **Dashboard** (`user/dashboard`) – your personal task summary.
- **My Tasks** (`user/tasks`) – the tasks assigned to you; open a task to see details, update status, add subtask progress and comments.

### 6.5 Account pages (all roles)
- **Profile** – view/update your name and details.
- **Settings** – account preferences.
- **Notifications** – view notifications.

---

## 7. Working with tasks

### Task status workflow
Tasks move through these statuses:

`NotAssigned` → `Assigned` → `InProgress` → `Completed` → `Tested` → `Closed`

### Priorities
`Low`, `Medium`, `High`, `Critical`

### Typical flow
1. A **Manager** creates a **Project** and assigns themselves (or is assigned) as project manager.
2. The Manager creates **Tasks** in the project and assigns each to a **User**, setting priority, estimated hours, and a due date.
3. The **User** opens **My Tasks**, works the task, and updates its **status** as they progress.
4. Managers/Admins track progress on their dashboards and reports.

### Task extras
- **Subtasks** – add checklist items to a task and tick them off as you finish.
- **Comments** – discuss the task; comments can have replies.
- **Tags** – attach colored labels (defined once per organization) to categorize tasks.
- **Attachments** – attach files to a task.
- **Watchers** – users can follow a task to stay informed.

---

## 8. Using the mobile app

The mobile app talks to the same backend API. After logging in you get:

- **Dashboard** – summary of your tasks/projects.
- **Tasks** – list of tasks with detail view and status updates.
- **Projects** – visible to Admin/Manager roles (hidden for the User role).
- **Users** – team management for authorized roles.
- **Profile** – view your account and log out.

Features: JWT login/logout with secure token storage, automatic token refresh on expiry, and role-based navigation (menus adapt to your role).

---

## 9. Multi-tenancy notes

- Every user (except SuperAdmin) belongs to exactly one organization.
- Data is automatically filtered to the current user's organization, so users never see another organization's projects, tasks, or people.
- SuperAdmin is not tied to an organization and can see across all tenants.

---

## 10. Troubleshooting

| Problem | What to check |
|---------|---------------|
| Can't log in | Confirm username/password; the seeded accounts are in section 4. Ensure the API is running. |
| Web app loads but data is empty | Confirm the database is reachable and migrations ran (watch the API console on startup). |
| Mobile app can't reach API | Verify the API base URL for your platform (section 3.2). Android emulator uses `10.0.2.2`, not `localhost`. |
| 401 / logged out unexpectedly | Access tokens expire (60 min); the app refreshes automatically. If it persists, log in again. |
| CORS errors in browser | Ensure your web origin is listed under `Cors:AllowedOrigins` in `appsettings.json`. |
| Password reset not working | Complete both steps: request reset (Forgot Password) then set a new one (Reset Password). |

---

## 11. Project structure (for reference)

```
src/TaskManager/
├── TaskManager.sln
├── TaskManager.Shared/          # DTOs & pagination models shared by API, web, mobile
├── TaskManager/
│   ├── TaskManager/             # ASP.NET Core backend (API + Blazor Server host)
│   │   ├── Controllers/         # Tasks, Projects, Users, Dashboard, Comments, Subtasks, Tags, ...
│   │   ├── Models/              # Domain entities (TaskItem, Project, User, Tag, Organization, ...)
│   │   ├── Data/                # DbContext, migrations, repositories, seeding
│   │   └── Services/            # Auth, tokens, tenant resolution
│   └── TaskManager.Client/      # Blazor WebAssembly UI (pages per role)
└── TaskManager.Mobile/          # .NET MAUI mobile client (Android/iOS/Windows/Mac)
```
