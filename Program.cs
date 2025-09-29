using Api.Data;
using Api.Hubs;
using Dapper;

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
builder.Services.AddScoped<PartidosRepo>(); // ✅ Partido
builder.Services.AddScoped<CronometroRepo>(); // ✅ NUEVO
builder.Services.AddScoped<CuartosRepo>(); // ✅ falta este para DI
// CORS (dev)
const string CorsDev = "cors-dev";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsDev, p =>
        p.WithOrigins("http://localhost:4200")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// DB wrapper
builder.Services.AddSingleton<Db>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsDev);

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

// Puerto fijo dev
// app.Urls.Add("http://localhost:5080");

app.Run();
