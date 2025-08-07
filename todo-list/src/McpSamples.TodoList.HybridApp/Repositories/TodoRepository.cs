using McpSamples.TodoList.HybridApp.Data;
using McpSamples.TodoList.HybridApp.Models;

using Microsoft.EntityFrameworkCore;

namespace McpSamples.TodoList.HybridApp.Repositories;

/// <summary>
/// This provides interfaces for the to-do database.
/// </summary>
public interface ITodoRepository
{
    /// <summary>
    /// Adds a new to-do item to the database.
    /// </summary>
    /// <param name="todoItem"><see cref="TodoItem"/> object.</param>
    /// <returns>Returns the <see cref="TodoItem"/> object added.</returns>
    Task<TodoItem> AddTodoItemAsync(TodoItem todoItem);

    /// <summary>
    /// Gets all to-do items from the database.
    /// </summary>
    /// <returns>Returns a list of <see cref="TodoItem"/> objects.</returns>
    Task<IEnumerable<TodoItem>> GetAllTodoItemsAsync();

    /// <summary>
    /// Updates an existing to-do item in the database.
    /// </summary>
    /// <param name="todoItem"><see cref="TodoItem"/> object.</param>
    /// <returns>Returns the <see cref="TodoItem"/> object updated.</returns>
    Task<TodoItem> UpdateTodoItemAsync(TodoItem todoItem);

    /// <summary>
    /// Marks a to-do item as completed in the database.
    /// </summary>
    /// <param name="todoItem"><see cref="TodoItem"/> object.</param>
    /// <returns>Returns the <see cref="TodoItem"/> object completed.</returns>
    Task<TodoItem> CompleteTodoItemAsync(TodoItem todoItem);

    /// <summary>
    /// Deletes a to-do item from the database.
    /// </summary>
    /// <param name="id">To-do item ID.</param>
    /// <returns>Returns the deleted <see cref="TodoItem"/> object.</returns>
    Task<TodoItem> DeleteTodoItemAsync(int id);
}

/// <summary>
/// This represents the repository for managing to-do items in the database.
/// </summary>
public class TodoRepository(TodoDbContext db) : ITodoRepository
{
    /// <inheritdoc />
    public async Task<TodoItem> AddTodoItemAsync(TodoItem todoItem)
    {
        await db.TodoItems.AddAsync(todoItem).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return todoItem;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TodoItem>> GetAllTodoItemsAsync()
    {
        var items = await db.TodoItems.ToListAsync().ConfigureAwait(false);

        return items;
    }

    /// <inheritdoc />
    public async Task<TodoItem> UpdateTodoItemAsync(TodoItem todoItem)
    {
        var record = await db.TodoItems.SingleOrDefaultAsync(p => p.Id == todoItem.Id)
                                       .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        record.Text = todoItem.Text;

        await db.TodoItems.Where(p => p.Id == todoItem.Id)
                          .ExecuteUpdateAsync(p => p.SetProperty(x => x.Text, todoItem.Text))
                          .ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);

        return record;
    }

    /// <inheritdoc />
    public async Task<TodoItem> CompleteTodoItemAsync(TodoItem todoItem)
    {
        var record = await db.TodoItems.SingleOrDefaultAsync(p => p.Id == todoItem.Id)
                                       .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        record.IsCompleted = todoItem.IsCompleted;

        await db.TodoItems.Where(p => p.Id == todoItem.Id)
                          .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsCompleted, todoItem.IsCompleted))
                          .ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);

        return record;
    }

    /// <inheritdoc />
    public async Task<TodoItem> DeleteTodoItemAsync(int id)
    {
        var record = await db.TodoItems.SingleOrDefaultAsync(p => p.Id == id)
                                       .ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        await db.TodoItems.Where(p => p.Id == id)
                          .ExecuteDeleteAsync()
                          .ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return record;
    }
}
