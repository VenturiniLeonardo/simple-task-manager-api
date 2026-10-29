using Microsoft.EntityFrameworkCore;
using TaskManager.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Library")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.Migrate();
}

app.MapGet("/tasks", async (TaskDbContext db) => await db.Tasks.ToListAsync());

app.MapGet("/tasks/{id:int}", async (int id, TaskDbContext db) =>
{
    TaskItem? task = await db.Tasks.FindAsync(id);
    return task is not null ? Results.Ok(task) : Results.NotFound();
});

app.MapPost("/tasks", async (CreateTaskRequest request, TaskDbContext db) =>
{
    try
    {
        var createdTask = new TaskItem(0, request.Title, request.Description);
        db.Tasks.Add(createdTask);
        await db.SaveChangesAsync();
        return Results.Created($"/tasks/{createdTask.Id}", createdTask);
    }
    catch (ArgumentException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapPut("/tasks/{id:int}", async (int id, CreateTaskRequest request, TaskDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Il titolo è obbligatorio." });
    }

    int rowsAffected = await db.Tasks
        .Where(t => t.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(t => t.Title, request.Title)
            .SetProperty(t => t.Description, request.Description));

    return rowsAffected > 0
        ? Results.Ok()
        : Results.NotFound(new { error = $"Task con ID {id} non trovato." });
});

app.MapDelete("/tasks/{id:int}", async (int id, TaskDbContext db) =>
{
    int rowsAffected = await db.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync();

    return rowsAffected > 0
        ? Results.Ok()
        : Results.NotFound(new { error = $"Task con ID {id} non trovato." });
});

app.MapPatch("/tasks/{id:int}/complete", async (int id, TaskDbContext db) =>
{
    int rowsAffected = await db.Tasks
        .Where(t => t.Id == id)
        .ExecuteUpdateAsync(t => t.SetProperty(t => t.IsCompleted, true));

    return rowsAffected > 0
        ? Results.Ok()
        : Results.NotFound(new { error = $"Task con ID {id} non trovato." });
});

app.Run();
