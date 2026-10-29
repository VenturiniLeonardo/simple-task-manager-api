# Simple Task Manager API

A small REST API for creating, listing, updating, completing, and deleting tasks, with data stored in SQLite.
Built as a personal project to learn C#, .NET, Entity Framework Core, and automated testing.

## Technologies

- C# and ASP.NET Core Minimal APIs targeting .NET 10.
- Entity Framework Core 10 with SQLite and versioned database migrations.
- xUnit, Microsoft.NET.Test.Sdk, and the Coverlet collector for testing.

## Requirements (.NET 10 SDK)

- .NET 10 SDK (check with `dotnet --version`).
- Git and network access to restore NuGet packages and the local EF Core tool.
- PowerShell for the HTTP examples below.
- Access to this repository if it is private.

SQLite runs in-process; no separate database server is needed. The repository includes a local tool manifest that pins `dotnet-ef` to 10.0.14.

## How to run

Run these commands from a directory where you want to clone the project:

```powershell
git clone https://github.com/VenturiniLeonardo/simple-task-manager-api.git
cd simple-task-manager-api
dotnet tool restore
dotnet restore TaskManager.slnx
cd TaskManager.Api
dotnet ef database update
dotnet run --no-launch-profile -- --urls http://localhost:5000
```

Keep that terminal running. From a second PowerShell terminal, call the API:

```powershell
Invoke-RestMethod -Method Get -Uri http://localhost:5000/tasks | ConvertTo-Json -AsArray
```

A fresh database returns `[]`. Create a task and retrieve it:

```powershell
$task = Invoke-RestMethod -Method Post -Uri http://localhost:5000/tasks -ContentType 'application/json' -Body '{"title":"Learn C#","description":"Build a small API"}'
Invoke-RestMethod -Method Get -Uri "http://localhost:5000/tasks/$($task.id)"
```

Stop the API with `Ctrl+C`.

The active connection string is `ConnectionStrings:Library` in `TaskManager.Api/appsettings.json`, pointing to `library.db`. Running the database update and the API from `TaskManager.Api` keeps their relative database paths aligned. The API also applies pending migrations at startup, so the explicit update documents the database setup and is safe to repeat. Database files and build output are excluded from Git.

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/tasks` | List all tasks; returns `200` and a JSON array. |
| GET | `/tasks/{id}` | Get one task; returns `200` or `404`. |
| POST | `/tasks` | Create a task; returns `201`, the task, and a `Location` header, or `400` for an invalid title. |
| PUT | `/tasks/{id}` | Replace title and description; returns `200`, `400`, or `404`. |
| PATCH | `/tasks/{id}/complete` | Mark a task as completed; returns `200` or `404`. Repeated completion returns `200`. |
| DELETE | `/tasks/{id}` | Delete a task; returns `200` or `404`. |

IDs are integers. POST and PUT accept the same JSON body:

```json
{
  "title": "Learn C#",
  "description": "Build a small API"
}
```

The title is required and cannot be empty or whitespace. Description is optional and nullable; omitting it in PUT clears the existing description. Completion is changed through PATCH. Tasks expose `id`, `title`, `description`, `createdAt` (UTC), and `isCompleted`. PUT, PATCH, and DELETE return an empty body on success. Existing validation error messages are in Italian.

Additional requests are available in `TaskManager.Api/TaskManager.Api.http`; set `TaskId` to an ID returned by POST.

## Tests

From the cloned repository root, run:

```powershell
dotnet test TaskManager.slnx
```

The nine tests cover:

- Task defaults, UTC creation timestamps, title validation, and completion rules.
- Deletion and invalid updates in the standalone in-memory `TaskService` learning exercise. The HTTP API uses EF Core directly rather than this service.
- SQLite migration compatibility, preservation of existing records, and UTC timestamp reads.
- HTTP creation, listing, retrieval, updates, completion, deletion, invalid titles, missing IDs, and persistence after restarting a real API process.

Integration tests start their own API processes on temporary ports and use temporary SQLite databases, leaving the development database untouched. Run them from a source checkout with the .NET SDK available.

## Design choices

- **Minimal APIs:** keep a small six-endpoint application easy to follow, without adding controller or repository layers before they are useful.
- **SQLite and EF Core migrations:** provide persistent relational storage with little local setup, while keeping schema changes versioned and reproducible.
- **Separate request model:** POST and PUT accept only title and description, keeping IDs, creation timestamps, and completion out of those request contracts.
- **UTC timestamps and repeatable completion:** timestamps retain their UTC meaning when read from SQLite, and repeating the completion HTTP request leaves the task completed with a successful response.

## Possible improvements

- Add filtering, pagination, and explicit ordering to the task list.
- Introduce consistent validation and error responses in English, with documented OpenAPI contracts.
- Add authentication and per-user task ownership before exposing personal tasks to other users.
- Expand tests for malformed requests and omitted descriptions, add CI, and retire the standalone in-memory service once its learning purpose is complete.
