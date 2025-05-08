using System.ComponentModel;

using McpTodoList.ContainerApp.Models;
using McpTodoList.ContainerApp.Repositories;

using ModelContextProtocol.Server;

namespace McpTodoList.ContainerApp.Tools;

[McpServerToolType]
public class TodoTool(ITodoRepository todo, ILogger<TodoTool> logger)
{
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

    [McpServerTool(Name = "complete_todo_item", Title = "Complete a to-do item")]
    [Description("Completes a to-do item in the database.")]
    public async Task<string> CompleteTodoItemAsync(
        [Description("The to-do item ID")] int id
    )
    {
        var todoItem = new TodoItem() { Id = id, IsCompleted = true };
        var completed = await todo.UpdateTodoItemAsync(todoItem).ConfigureAwait(false);
        if (completed is null)
        {
            logger.LogWarning("Todo item with ID '{id}' not found.", id);

            return $"Todo item with ID '{id}' not found.";
        }

        logger.LogInformation("Completed todo item: '{id}'", id);

        return $"Todo item completed: '{id}'";
    }

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
