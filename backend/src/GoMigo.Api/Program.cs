using System.Reflection;
using System.Text.Json.Serialization;
using GoMigo.Api.Errors;
using GoMigo.Application;
using GoMigo.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o =>
{
    o.IncludeScopes = true;
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    o.UseUtcTimestamp = true;
});

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GoMigo API",
        Version = "v1",
        Description = "GoMigo: Your next move, figured out. Local discovery research runs."
    });
    foreach (var xml in new[] { "GoMigo.Api.xml", "GoMigo.Application.xml" })
    {
        var path = Path.Combine(AppContext.BaseDirectory, xml);
        if (File.Exists(path)) o.IncludeXmlComments(path);
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location")));

builder.Services.AddGoMigoApplication();
builder.Services.AddGoMigoInfrastructure(builder.Configuration);

var app = builder.Build();

// CORS first, so error responses (4xx/5xx) also carry the headers the browser needs to read them.
app.UseCors();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "GoMigo API");
}

app.MapControllers();

app.Logger.LogInformation(
    "GoMigo API {Version} starting with research provider {ResearchProvider} and persistence {Persistence}",
    Assembly.GetExecutingAssembly().GetName().Version,
    GoMigo.Infrastructure.DependencyInjection.GetResearchProvider(app.Configuration),
    GoMigo.Infrastructure.DependencyInjection.GetPersistenceProvider(app.Configuration));

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
