using Microsoft.EntityFrameworkCore;
using ProjectManagerWebApi.Data;
using Microsoft.AspNetCore.Mvc;
using Task = ProjectManagerWebApi.Models.Tasks;
using Project = ProjectManagerWebApi.Models.Projects;
using ProjectManagerWebApi.Models;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddSqlServer<ProjectTrackerContext>(builder.Configuration.GetConnectionString("DefaultConnection"));

var keyVaultEndpoint = new Uri(builder.Configuration["VaultKey"]);
var secretClient = new SecretClient(keyVaultEndpoint, new DefaultAzureCredential());
KeyVaultSecret kvs = secretClient.GetSecret("azuresql");
builder.Services.AddDbContext<ProjectTrackerContext>(o => o.UseSqlServer(kvs.Value));

//var connString = builder.Configuration.GetConnectionString("DefaultConnection");
//builder.Services.AddDbContext<ProjectTrackerContext>(o => o.UseSqlServer(connString));
builder.Services.AddScoped<ProjectTrackerContextProcedures>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("api/tasks", async ([FromServices] ProjectTrackerContext db) =>
{ return await db.Tasks.ToListAsync(); });


app.MapGet("api/task_sp/{id}", async ([FromServicesAttribute] ProjectTrackerContextProcedures db, int id) =>
{
    var op = new OutputParameter<int>();
    return await db.sp_Select_TaskAsync(id, op);
});

app.MapPost("api/task_dto", async ([FromServices] ProjectTrackerContext db, [FromBody] Task task) =>
{
    var newTask = new Task()
    {
        TaskName = task.TaskName,
        DateUpdated = DateTime.Now,
        DateDue = task.DateDue,
        ProjectId = task.ProjectId,
        AssignedToEmail = task.AssignedToEmail,
        Priority = task.Priority,
    };
    await db.Tasks.AddAsync(newTask);
    await db.SaveChangesAsync();
    return TypedResults.Ok(newTask);
});

app.MapPost("api/task", async ([FromServices] ProjectTrackerContext db, Task task) =>
{
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Ok(task);
});

app.MapPut("api/task", async ([FromServices] ProjectTrackerContext db, [FromBody] Task task) =>
{

    var dbTask = await db.Tasks.FindAsync(task.TaskId);
    if (dbTask == null)
    {
        //context.Response.StatusCode = StatusCodes.Status404NotFound;
        return TypedResults.Ok(dbTask);
    }

    //var dbTask = await db.Tasks.FindAsync(task.TaskId);
    dbTask.ProjectId = task.ProjectId;
    dbTask.TaskName = task.TaskName;
    dbTask.DateUpdated = System.DateTime.Now;
    dbTask.DateDue = task.DateDue;
    dbTask.AssignedToEmail = task.AssignedToEmail;
    dbTask.Priority = task.Priority;
    await db.SaveChangesAsync();
    return TypedResults.Ok(dbTask);
});


app.MapPut("/api/task/{taskId}", async (HttpContext context, int taskId, [FromServices] ProjectTrackerContext db) =>
{
    var dbTask = await db.Tasks.FindAsync(taskId);
    if (dbTask == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return TypedResults.Ok(dbTask);
    }

    var task = await context.Request.ReadFromJsonAsync<Task>();
    dbTask.ProjectId = task.ProjectId;
    dbTask.TaskName = task.TaskName;
    dbTask.DateUpdated = DateTime.Now;
    dbTask.DateDue = task.DateDue;
    dbTask.AssignedToEmail = task.AssignedToEmail;
    dbTask.Priority = task.Priority;

    await db.SaveChangesAsync();
    context.Response.StatusCode = StatusCodes.Status204NoContent;
    return TypedResults.Ok(dbTask);
});


app.MapPost("api/task_sp", async ([FromServices] ProjectTrackerContextProcedures db, Task task) =>
{
    var op = new OutputParameter<int>();
    List<sp_Insert_TaskResult> lst = new List<sp_Insert_TaskResult>();
    lst = await db.sp_Insert_TaskAsync(task.TaskName, task.DateUpdated, task.DateDue,
          task.ProjectId, task.AssignedToEmail, task.Priority, op);
    return lst[0];
});

app.MapPut("api/task_sp", async ([FromServices] ProjectTrackerContextProcedures db, Task task) =>
{
    var op = new OutputParameter<int>();
    var err = new OutputParameter<int?>();
    List<sp_Update_TaskResult> lst = new List<sp_Update_TaskResult>();
    lst = await db.sp_Update_TaskAsync(task.TaskId, task.TaskName, System.DateTime.Now,
            task.DateDue, task.ProjectId, task.AssignedToEmail, task.Priority, err, op);
    return lst[0];   
});

app.MapDelete("api/task_sp/{id}", async ([FromServices] ProjectTrackerContextProcedures db, int id) =>
{
    var op = new OutputParameter<int>();
    await db.sp_Delete_TaskAsync(id, op);
    return await db.sp_Select_TaskAsync(id, op);
});

app.MapGet("api/projects_sp", async ([FromServices] ProjectTrackerContextProcedures db) =>
{
    var op = new OutputParameter<int>();
    return await db.sp_Select_ProjectsAsync(op);
});

app.MapGet("api/project_sp/{id}", async ([FromServices] ProjectTrackerContextProcedures db, int id) =>
{
    var op = new OutputParameter<int>();
    return await db.sp_Select_ProjectAsync(id, op);
});

app.MapPost("api/project_sp", async ([FromServices] ProjectTrackerContextProcedures db, Project project) =>
{
    var op = new OutputParameter<int>();
    return await db.sp_Insert_ProjectAsync(project.ProjectName, op);
});

app.MapPut("api/project_sp", async ([FromServices] ProjectTrackerContextProcedures db, Project project) =>
{
    var op = new OutputParameter<int>();
    var err = new OutputParameter<int?>();
    List<sp_Update_ProjectResult> lst = new List<sp_Update_ProjectResult>();
    if (project.ProjectId != 0 && project.ProjectName != "")
        lst = await db.sp_Update_ProjectAsync(project.ProjectName, project.ProjectId, err, op);
    return lst[0];
});

app.UseHttpsRedirection();
app.Run();