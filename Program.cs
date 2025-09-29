// Program.cs
using Api.Data;
using Api.Hubs;
using Dapper;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Swagger & SignalR
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Repos
builder.Services.AddScoped<MarcadorRepo>();
builder.Services.AddScoped<JugadoresRepo>();
builder.Services.AddScoped<FaltasRepo>();
builder.Services.AddScoped<TiemposMuertosRepo>();
builder.Services.AddScoped<PartidosRepo>();
builder.Services.AddScoped<CronometroRepo>();
builder.Services.AddScoped<CuartosRepo>();

// === CORS ===
// Lee de env var / appsettings: AllowedOrigins = "https://uniondeprofesionales.com,https://www.uniondeprofesionales.com"
var allowed = (builder.Configuration["AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Si no viene nada, deja defaults útiles para dev
if (allowed.Length == 0)
{
    allowed = new[]
    {
        "http://localhost:4200",
        "https://localhost:4200",
        "https://uniondeprofesionales.com",
        "https://www.uniondeprofesionales.com"
    };
}

const string CorsPolicy = "default";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p =>
        p.WithOrigins(allowed)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    );
});

// DB wrapper
builder.Services.AddSingleton<Db>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Si estás detrás de Nginx (proxy), respeta los encabezados X-Forwarded-* para detectar HTTPS correctamente.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

app.UseCors(CorsPolicy);

// Endpoints mínimos
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", env = app.Environment.EnvironmentName }));

app.MapGet("/api/tables", async (Db db) =>
{
    using var conn = db.Open();
    var rows = await conn.QueryAsync<string>("SELECT name FROM sys.tables ORDER BY name");
    return Results.Ok(rows);
});

// Hub de marcador
app.MapHub<MarcadorHub>("/hub/marcador");

// Controllers
app.MapControllers();

// ¡No fijes el puerto aquí en prod! Nginx hace el proxy.
// app.Urls.Add("http://localhost:5080");

app.Run();
