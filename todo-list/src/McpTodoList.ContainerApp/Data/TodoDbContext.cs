using System;

using McpTodoList.ContainerApp.Models;

using Microsoft.EntityFrameworkCore;

namespace McpTodoList.ContainerApp.Data;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>().ToTable("TodoItems")
                                       .HasKey(t => t.Id);
        modelBuilder.Entity<TodoItem>().Property(t => t.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<TodoItem>().Property(t => t.Text).IsRequired().HasMaxLength(255);
        modelBuilder.Entity<TodoItem>().Property(t => t.IsCompleted).IsRequired();
    }
}
