using System.ComponentModel;

using McpSamples.TodoList.HybridApp.Models;
using McpSamples.TodoList.HybridApp.Repositories;

using ModelContextProtocol.Server;

namespace McpSamples.TodoList.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the todo tool.
/// </summary>
public interface ITodoTool
{
    /// <summary>
    /// Adds a new to-do item.
    /// </summary>
    /// <param name="todoItemText">To-do item text.</param>
    /// <returns>Returns the result of adding the to-do item.</returns>
    Task<string> AddTodoItemAsync(string todoItemText);

    /// <summary>
    /// Gets a list of to-do items.
    /// </summary>
    /// <returns>Returns a list of to-do items.</returns>
    Task<IEnumerable<string>> GetTodoItemsAsync();

    /// <summary>
    /// Updates a to-do item.
    /// </summary>
    /// <param name="id">The to-do item ID.</param>
    /// <param name="todoItemText">The to-do item text.</param>
    /// <returns>Returns the result of updating the to-do item.</returns>
    Task<string> UpdateTodoItemAsync(int id, string todoItemText);

    /// <summary>
    /// Completes a to-do item.
    /// </summary>
    /// <param name="id">The to-do item ID.</param>
    /// <returns>Returns the result of completing the to-do item.</returns>
    Task<string> CompleteTodoItemAsync(int id);

    /// <summary>
    /// Deletes a to-do item.
    /// </summary>
    /// <param name="id">The to-do item ID.</param>
    /// <returns>Returns the result of deleting the to-do item.</returns>
    Task<string> DeleteTodoItemAsync(int id);
}

/// <summary>
/// This represents the tool entity for managing to-do items.
/// </summary>
/// <param name="todo"><see cref="ITodoRepository"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class TodoTool(ITodoRepository todo, ILogger<TodoTool> logger) : ITodoTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "add_todo_item", Title = "Add a to-do item")]
    [Description("Adds a to-do item to database.")]
    public async Task<string> AddTodoItemAsync(
        [Description("The to-do item text")] string todoItemText
    )
    {
        var todoItem = new TodoItem { Text = todoItemText };
        await todo.AddTodoItemAsync(todoItem).ConfigureAwait(false);

        logger.LogInformation("Todo item added: '{todoItemText}' (ID: {Id})", todoItemText, todoItem.Id);

        return $"Todo item added: '{todoItemText}' (ID: {todoItem.Id})";
    }

    /// <inheritdoc />
    [McpServerTool(Name = "get_todo_items", Title = "Get a list of to-do items")]
    [Description("Gets a list of to-do items from database.")]
    public async Task<IEnumerable<string>> GetTodoItemsAsync()
    {
        var todoItems = await todo.GetAllTodoItemsAsync().ConfigureAwait(false);

        logger.LogInformation("Retrieved {Count} todo items.", todoItems.Count());

        return todoItems.Any()
               ? todoItems.Select(p => $"ID: {p.Id}, Text: {p.Text}, Completed: {p.IsCompleted}")
               : [ "No todo items found." ];
    }

    /// <inheritdoc />
    [McpServerTool(Name = "update_todo_item", Title = "Update a to-do item")]
    [Description("Updates a to-do item in the database.")]
    public async Task<string> UpdateTodoItemAsync(
        [Description("The to-do item ID")] int id,
        [Description("The to-do item text")] string todoItemText
    )
    {
        var todoItem = new TodoItem { Id = id, Text = todoItemText };
        var updated = await todo.UpdateTodoItemAsync(todoItem).ConfigureAwait(false);
        if (updated is null)
        {
            logger.LogWarning("Todo item with ID '{id}' not found.", id);

            return $"Todo item with ID '{id}' not found.";
        }

        logger.LogInformation("Updated todo item: '{id}' with text: '{todoItem}'", id, todoItem);

        return $"Todo item updated: '{id}' with text: '{todoItem}'";
    }

    /// <inheritdoc />
    [McpServerTool(Name = "complete_todo_item", Title = "Complete a to-do item")]
    [Description("Completes a to-do item in the database.")]
    public async Task<string> CompleteTodoItemAsync(
        [Description("The to-do item ID")] int id
    )
    {
        var todoItem = new TodoItem() { Id = id, IsCompleted = true };
        var completed = await todo.CompleteTodoItemAsync(todoItem).ConfigureAwait(false);
        if (completed is null)
        {
            logger.LogWarning("Todo item with ID '{id}' not found.", id);

            return $"Todo item with ID '{id}' not found.";
        }

        logger.LogInformation("Completed todo item: '{id}'", id);

        return $"Todo item completed: '{id}'";
    }

    /// <inheritdoc />
    [McpServerTool(Name = "delete_todo_item", Title = "Delete a to-do item")]
    [Description("Deletes a to-do item from the database.")]
    public async Task<string> DeleteTodoItemAsync(
        [Description("The to-do item ID")] int id
    )
    {
        var deleted = await todo.DeleteTodoItemAsync(id).ConfigureAwait(false);
        if (deleted is null)
        {
            logger.LogWarning("Todo item with ID '{id}' not found.", id);

            return $"Todo item with ID '{id}' not found.";
        }

        logger.LogInformation("Deleted todo item: '{id}'", id);

        return $"Todo item deleted: '{id}'";
    }
}
