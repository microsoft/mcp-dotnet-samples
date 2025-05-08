using McpTodoList.ContainerApp.Data;
using McpTodoList.ContainerApp.Models;

using Microsoft.EntityFrameworkCore;

namespace McpTodoList.ContainerApp.Repositories;

public interface ITodoRepository
{
    Task<TodoItem> AddTodoItemAsync(TodoItem todoItem);
    Task<IEnumerable<TodoItem>> GetAllTodoItemsAsync();
    Task<TodoItem> UpdateTodoItemAsync(TodoItem todoItem);
    Task<TodoItem> DeleteTodoItemAsync(int id);
}

public class TodoRepository(TodoDbContext db) : ITodoRepository
{
    public async Task<TodoItem> AddTodoItemAsync(TodoItem todoItem)
    {
        await db.TodoItems.AddAsync(todoItem).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return todoItem;
    }

    public async Task<IEnumerable<TodoItem>> GetAllTodoItemsAsync()
    {
        var items = await db.TodoItems.ToListAsync().ConfigureAwait(false);

        return items;
    }

    public async Task<TodoItem> UpdateTodoItemAsync(TodoItem todoItem)
    {
        var record = await db.TodoItems.SingleOrDefaultAsync(p => p.Id == todoItem.Id)
                                       .ConfigureAwait(false);
        if (record is null)
        {
            return default!;
        }

        record.Text = todoItem.Text;
        record.IsCompleted = todoItem.IsCompleted;

        await db.TodoItems.Where(p => p.Id == todoItem.Id)
                          .ExecuteUpdateAsync(p => p.SetProperty(x => x.Text, todoItem.Text)
                                                    .SetProperty(x => x.IsCompleted, todoItem.IsCompleted))
                          .ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);

        return record;
    }

    public async Task<TodoItem> DeleteTodoItemAsync(int id)
    {
        var record = await db.TodoItems.SingleOrDefaultAsync(p => p.Id == id)
                                       .ConfigureAwait(false);
        if (record is null)
        {
            return default!;
        }

        await db.TodoItems.Where(p => p.Id == id)
                          .ExecuteDeleteAsync()
                          .ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return record;
    }
}
