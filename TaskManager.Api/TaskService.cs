namespace TaskManager.Api;

public class TaskService
{
    private readonly List<TaskItem> _tasks = new();

    public IReadOnlyList<TaskItem> Tasks => _tasks;

    public bool HasItems => _tasks.Count > 0;

    private int LastIndex = 1;

    public TaskItem Create(string title, string? description)
    {
        var item = new TaskItem(LastIndex, title, description);
        LastIndex += 1;
        _tasks.Add(item);
        return item;
    }

    public IReadOnlyList<TaskItem> GetAll()
    {
        return Tasks;
    }

    public TaskItem? GetById(int id)
    {
        return _tasks.FirstOrDefault(x => x.Id == id);
    }

    public bool Update(int id, string title, string? description)
    {
        var task = GetById(id);
        if (task == null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Il titolo è obbligatorio.");
        }
        task.Title = title;
        task.Description = description;
        return true;
    }

    public bool MarkAsCompleted(int id)
    {
        var task = GetById(id);
        if (task == null)
        {
            return false;
        }
        task.TaskCompleted();
        return true;
    }

    public bool Delete(int id)
    {
        var task = GetById(id);
        if (task == null)
        {
            return false;
        }
        _tasks.Remove(task);
        return true;
    }
}
