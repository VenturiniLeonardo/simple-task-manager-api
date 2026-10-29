using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api;

namespace TaskManager.Tests;

public class ApiTests
{
    [Fact]
    public async Task Api_ValidatesInput_PersistsData_AndWorksAfterRestart()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TaskManager-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "tasks.db");

        try
        {
            int id;
            string createdAt;
            using (var api = new TestApi(database))
            {
                await api.WaitUntilReady();
                Assert.Empty((await api.Client.GetFromJsonAsync<JsonElement[]>("/tasks"))!);

                var response = await api.Client.PostAsJsonAsync("/tasks",
                    new { title = "Prima task", description = "Descrizione" });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                var task = await response.Content.ReadFromJsonAsync<JsonElement>();
                id = task.GetProperty("id").GetInt32();
                createdAt = task.GetProperty("createdAt").GetString()!;
                Assert.EndsWith("Z", createdAt);
                Assert.Equal($"/tasks/{id}", response.Headers.Location!.ToString());

                foreach (string? title in new string?[] { "", "  ", null })
                {
                    response = await api.Client.PostAsJsonAsync("/tasks", new { title });
                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                    var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                    Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("error").GetString()));

                    response = await api.Client.PutAsJsonAsync($"/tasks/{id}", new { title });
                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }

                response = await api.Client.PostAsJsonAsync("/tasks", new { description = "Senza titolo" });
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                task = await api.Client.GetFromJsonAsync<JsonElement>($"/tasks/{id}");
                Assert.Equal("Prima task", task.GetProperty("title").GetString());
                Assert.Equal(createdAt, task.GetProperty("createdAt").GetString());

                response = await api.Client.PutAsJsonAsync($"/tasks/{id}",
                    new { title = "Aggiornata", description = "Nuova descrizione" });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                response = await api.Client.PatchAsync($"/tasks/{id}/complete", null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                response = await api.Client.PatchAsync($"/tasks/{id}/complete", null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/tasks/999")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await api.Client.DeleteAsync("/tasks/999")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PatchAsync("/tasks/999/complete", null)).StatusCode);
                response = await api.Client.PutAsJsonAsync("/tasks/999", new { title = "Assente" });
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }

            using (var api = new TestApi(database))
            {
                await api.WaitUntilReady();
                var task = await api.Client.GetFromJsonAsync<JsonElement>($"/tasks/{id}");
                Assert.Equal("Aggiornata", task.GetProperty("title").GetString());
                Assert.Equal("Nuova descrizione", task.GetProperty("description").GetString());
                Assert.True(task.GetProperty("isCompleted").GetBoolean());
                Assert.Equal(createdAt, task.GetProperty("createdAt").GetString());

                var response = await api.Client.PostAsJsonAsync("/tasks", new { title = "Dopo riavvio" });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                task = await response.Content.ReadFromJsonAsync<JsonElement>();
                int secondId = task.GetProperty("id").GetInt32();
                Assert.NotEqual(id, secondId);
                Assert.Equal(2, (await api.Client.GetFromJsonAsync<JsonElement[]>("/tasks"))!.Length);
                Assert.Equal(HttpStatusCode.OK, (await api.Client.DeleteAsync($"/tasks/{id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/tasks/{id}")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await api.Client.DeleteAsync($"/tasks/{secondId}")).StatusCode);
                Assert.Empty((await api.Client.GetFromJsonAsync<JsonElement[]>("/tasks"))!);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Migrations_PreserveExistingItems_AndReadDatesAsUtc()
    {
        string database = Path.Combine(Path.GetTempPath(), "TaskManager-migrations-" + Guid.NewGuid() + ".db");
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseSqlite($"Data Source={database}").Options;
        try
        {
            using (var db = new TaskDbContext(options))
            {
                Assert.False(db.Database.HasPendingModelChanges());
                await db.Database.MigrateAsync();
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Items (Id, Title, Description, CreatedAt, IsCompleted)
                    VALUES (42, 'Task esistente', NULL, '2026-10-29 12:00:00', 0)
                    """);
            }
            using (var db = new TaskDbContext(options))
            {
                await db.Database.MigrateAsync();
                var task = await db.Tasks.SingleAsync();
                Assert.Equal(42, task.Id);
                Assert.Equal("Task esistente", task.Title);
                Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
                Assert.Equal(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc), task.CreatedAt);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(database);
        }
    }

    private class TestApi : IDisposable
    {
        private readonly Process process;
        private readonly System.Text.StringBuilder output = new();
        public HttpClient Client { get; }

        public TestApi(string database)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "TaskManager.slnx")))
            {
                root = root.Parent;
            }
            if (root == null)
            {
                throw new InvalidOperationException("Cartella della soluzione non trovata.");
            }

            string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string project = Path.Combine(root.FullName, "TaskManager.Api");
            string assembly = Path.Combine(project, "bin", configuration, "net10.0", "TaskManager.Api.dll");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = project,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add(Client.BaseAddress.ToString());
            start.ArgumentList.Add("--ConnectionStrings:Library");
            start.ArgumentList.Add($"Data Source={database}");
            process = new Process { StartInfo = start };
            process.OutputDataReceived += (_, e) => { lock (output) { output.AppendLine(e.Data); } };
            process.ErrorDataReceived += (_, e) => { lock (output) { output.AppendLine(e.Data); } };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public async Task WaitUntilReady()
        {
            for (int i = 0; i < 100 && !process.HasExited; i++)
            {
                try
                {
                    var response = await Client.GetAsync("/tasks");
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }
            lock (output)
            {
                throw new InvalidOperationException("Avvio API fallito: " + output);
            }
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
            process.WaitForExit();
            process.Dispose();
            Client.Dispose();
        }
    }
}
