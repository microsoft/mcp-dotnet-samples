using McpSamples.TodoList.HybridApp.Models;

using Microsoft.EntityFrameworkCore;

namespace McpSamples.TodoList.HybridApp.Data;

/// <summary>
/// This represents the database context for the to-do list application.
/// </summary>
/// <param name="options"><see cref="DbContextOptions{TContext}"/> instance.</param>
public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the collection of to-do items in the database.
    /// </summary>
    public DbSet<TodoItem> TodoItems { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>().ToTable("TodoItems")
                                       .HasKey(t => t.Id);
        modelBuilder.Entity<TodoItem>().Property(t => t.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<TodoItem>().Property(t => t.Text).IsRequired().HasMaxLength(255);
        modelBuilder.Entity<TodoItem>().Property(t => t.IsCompleted).IsRequired();
    }
}
