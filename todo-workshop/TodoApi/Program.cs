using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Scalar.AspNetCore;

using TodoApi.Dtos;
using TodoApi.Data;
using TodoApi.Models;
using TodoApi.Data;
using Microsoft.Extensions.Options;
using System.Text;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
        };

        return Task.CompletedTask;
    });
});
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var jwtkey = builder.Configuration["Jwt:Key"];
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtkey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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
        var todoGetDtos = todos.Select(t => 
        
        new TodoGetDto(t.Id,t.Title,t.IsCompleted));

        return todos.Count == 0 ? Results.NoContent() :  Results.Ok(todoGetDtos);
    })
    .RequireAuthorization();
    
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
    })
    .RequireAuthorization();
#endregion

#region Authentication Endpoints

    app.MapPost("/api/login",(LoginDto dto, IConfiguration configuration) =>
    {
        if (dto.Username != "admin" || dto.Password != "Password") return Results.Unauthorized();
        var claims = new[]
        {
            new Claim(ClaimTypes.Name,  dto.Username),

        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(configuration["Jwt:ExpireDays"])),
            signingCredentials: credentials
        );
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Results.Ok(new {Token = tokenString});
    }).WithTags("Authentication").WithName("Login").Produces<LoginResponseDto>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized);
#endregion
app.Run();
