namespace TaskManager.Api;

public class TaskItem
{
    public int Id { get; private set; }
    public string Title {get;set;}

    public string? Description {get;set;}

    public DateTime CreatedAt { get; private set; }

    public bool IsCompleted { get; set; } = false;

    public TaskItem(int id, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
        throw new ArgumentException("Il titolo è obbligatorio.", nameof(title));
        }
        Id = id;
        Description=description;
        Title=title;
        CreatedAt = DateTime.UtcNow;
    }


    public void TaskCompleted()
    {
        if (IsCompleted)
        {
            throw new ArgumentException("Task already completed");
        }
        IsCompleted = true;
    }

    public override string ToString()
    {
        return $"ID : {Id}, Title : {Title}, Description: {Description ?? "Non presente"} Creation Date : {CreatedAt.ToLocalTime()}, Completed: {IsCompleted}";
    }

}

