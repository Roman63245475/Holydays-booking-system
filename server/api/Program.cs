using api;
using database;
using service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("React", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(document =>
{
    document.Title = "Holidays booking API";
});

var connection_string = builder.Configuration.GetConnectionString("connection_string")
    ?? throw new InvalidOperationException("Connection string 'connection_string' not found.");
using var db = DatabaseConfiguration.Create(connection_string);
DatabaseInitializer.Initialize(db);
builder.Services.AddScoped<Database>(
    _ => DatabaseConfiguration.Create(connection_string)
);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
    options.InstanceName = "MyApp:";
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.Name = "MyApp.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<LoginController>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<HolidayAccountsService>();
builder.Services.AddScoped<HolidayHistoryService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccuralService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseCors("React");
app.UseOpenApi();
app.UseSwaggerUi();
app.UseSession();
app.MapControllers();
app.Run();
