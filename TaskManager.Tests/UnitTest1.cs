using TaskManager.Api;

namespace TaskManager.Tests;

public class TaskTests
{
    [Fact]
    public void NewTask_IsNotCompleted()
    {
        var task = new TaskItem(1, "Task 1", "Descrizione");

        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void NewTask_CreatedAt_IsSet()
    {
        var before = DateTime.UtcNow;
        var task = new TaskItem(1, "Task 1", "Descrizione");
        var after = DateTime.UtcNow;

        Assert.True(task.CreatedAt >= before && task.CreatedAt <= after);
    }

    [Fact]
    public void NewTask_EmptyTitle()
    {
        Assert.Throws<ArgumentException>(() => new TaskItem(1, "", "Descrizione"));
    }

    [Fact]
    public void NewTask_Completed()
    {
        var task = new TaskItem(1, "Task 1", "Descrizione");
        task.TaskCompleted();
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void NewTask_AlreadyCompleted()
    {
        var task = new TaskItem(1, "Task 1", "Descrizione");
        task.IsCompleted = true;
        Assert.Throws<ArgumentException>(() => task.TaskCompleted());
    }
}
