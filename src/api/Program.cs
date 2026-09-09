using System.Text.Json.Serialization;
using EventReservation.Api.DependencyInjection;
using EventReservation.Api.Middleware;
using EventReservation.Application.DependencyInjection;
using EventReservation.Infrastructure.DependencyInjection;

if (File.Exists(".env"))
    DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

var messagingAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

// Add services to the container.
builder.Services
    .Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddInfrastructure(builder.Configuration)
    .AddMessaging(builder.Configuration, messagingAssembly)
    .AddApplication()
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Version = "v1",
            Title = "Event Reservation API",
            Description = "An ASP.NET Core Web API for an Event Reservation application"
        });
    })
    .AddCustomCors(
        "AllowLocalhost",
        "http://127.0.0.1:5045",
        "http://localhost:5045",
        "https://localhost:7123"
    );
    if (!builder.Environment.IsEnvironment("Container"))
    {
        builder.Services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = 7123;
        });
    }

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Container"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseCors("AllowLocalhost");

if (!app.Environment.IsEnvironment("Container"))
    app.UseHttpsRedirection();

app.UseExceptionHandler();
app.MapEndpoints();
app.Run();