namespace McpSamples.TodoList.HybridApp.Models;

/// <summary>
/// This represents a to-do item in the to-do list application.
/// </summary>
public class TodoItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the to-do item.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the text of the to-do item.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the to-do item is completed.
    /// </summary>
    public bool IsCompleted { get; set; } = false;
}
