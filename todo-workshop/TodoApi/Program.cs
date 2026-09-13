using Microsoft.EntityFrameworkCore;
using TodoApi.Dtos;
using TodoApi.Data;
using TodoApi.Models;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var todoGroup = app.MapGroup("/api/todos").WithTags("Todos");

#region In-Memory Endpoints

// var todos = new List<TodoGetDto>
// {
//     new(1,"Tharatap",true),
//     new(2,"Thanakorn",true),
//     new(3,"pun",false),
// };
// todoGroup.MapGet("/",() => Results.Ok(todos));


// todoGroup.MapGet("/api/todos/{id}", (int id) =>
// {

//    var todo = todos.FirstOrDefault(t => t.Id == id);
//    return todo is not null ? Results.Ok(todo) : Results.NotFound();
// });

// todoGroup.MapPost("/api/todos",(TodoPostDto dto) =>
// {
//     var nextID = todos.Count == 0 ? 1 : todos.Max(t => t.Id) + 1;
//     var todo = new TodoGetDto(nextID,dto.Title,false);
//     todos.Add(todo);
//     return Results.Created($"/api/todos/{todo.Id}",todo);
// });

// todoGroup.MapPut("/api/todos/{id}",(int id, TodoPutDto dto)=>
// {
//     try
//     {
//         var index = todos.FindIndex(t => t.Id == id);
//         if(index == -1) return Results.NotFound();

//         todos[index] = todos[index] with
//         {
//         Title = dto.Title,
//         IsCompleted = dto.IsCompleted
//         };
//         return Results.Ok(todos[index]);
//     }
//     catch (Exception ex){
//        return Results.Problem(ex.Message);
//     }
    
// });

// todoGroup.MapDelete("/api/todos/{id}", (int id) =>
// {
//     try{
//         var todo = todos.FirstOrDefault(t => t.Id == id);
//         if (todo is null) return Results.NotFound();
        
//         todos.Remove(todo);
//         return Results.NoContent();
//     }
//     catch (ArgumentException ex)
//     {
//         return Results.Problem("Is non");
//     }
//     catch(Exception ex)
//     {
//         return Results.Problem(ex.Message);
//     }
// });

#endregion

#region Database Enpoints
    todoGroup.MapGet("/",async(AppDbContext db)=>
    {
        var todos = await db.Todos.ToListAsync();
        return todos.Count == 0 ? Results.NoContent() :  Results.Ok(todos);
    });

    todoGroup.MapPost("/",async(AppDbContext db,TodoPostDto dto) =>
    {
        var lastTodo = await db.Todos.OrderByDescending(t => t.Id).FirstOrDefaultAsync();
        var nextID = lastTodo is null ? 1 : lastTodo.Id + 1;
        var todo = new TodoItem
        {
            Id = nextID,
            Title = dto.Title,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        var todoGetDto = new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted);
        return Results.Created($"/{todo.Id}",todo);
    });
#endregion

app.Run();
