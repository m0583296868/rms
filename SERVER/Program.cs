using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RMS.BL;
using RMS.DAL;
using RMS;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Configuration.GetValue<bool>("UseSqlite"))
{
    builder.Services.AddDbContext<RmsDBContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IRequestDal, RequestDal>();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddMemoryCache();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "ניהול פניות - API",
        Description = "API לניהול פניות מארגונים ומעסיקים"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ex is OperationCanceledException || context.RequestAborted.IsCancellationRequested)
            return;
        if (context.Response.HasStarted)
            return;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        int status;
        string title;
        string detail;

        switch (ex)
        {
            case NotFoundException:
                status = StatusCodes.Status404NotFound;
                title = "לא נמצא";
                detail = ex.Message;
                break;
            case AppValidationException:
                status = StatusCodes.Status400BadRequest;
                title = "קלט לא תקין";
                detail = ex.Message;
                break;
            case ConflictException:
                status = StatusCodes.Status409Conflict;
                title = "קונפליקט";
                detail = ex.Message;
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                title = "שגיאת שרת";
                detail = "אירעה שגיאה. נסה שוב.";
                logger.LogError(ex, "Unhandled error");
                break;
        }

        if (status != StatusCodes.Status500InternalServerError)
            logger.LogWarning("Request failed {Status}: {Detail}", status, detail);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        });
    });
});

if (app.Configuration.GetValue<bool>("AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RmsDBContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
