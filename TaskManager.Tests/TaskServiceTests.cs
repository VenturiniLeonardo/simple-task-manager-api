using TaskManager.Api;

namespace TaskManager.Tests;

public class TaskServiceTests
{
    [Fact]
    public void Delete_ExistingTask_ReturnsTrueAndRemovesTask()
    {
        var service = new TaskService();
        var task = service.Create("Task", null);

        Assert.True(service.Delete(task.Id));
        Assert.Empty(service.GetAll());
        Assert.False(service.Delete(task.Id));
    }

    [Fact]
    public void Update_InvalidTitle_DoesNotChangeTask()
    {
        var service = new TaskService();
        var task = service.Create("Originale", "Descrizione");

        Assert.Throws<ArgumentException>(() => service.Update(task.Id, "  ", null));
        Assert.Equal("Originale", task.Title);
        Assert.Equal("Descrizione", task.Description);
        Assert.Null(service.GetById(999));
    }
}
