using be;
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
// all rule sets are kept in memory, calculations ask HolidayParameters for the rules of a date
HolidayParameters.load(db.rule_sets.ToList());
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
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RulesetService>();
builder.Services.AddScoped<CurrentDateService>();
// DayOffService gets an HttpClient for the public holidays API (Nager.Date, free, no key needed)
builder.Services.AddHttpClient<DayOffService>(client =>
{
    client.BaseAddress = new Uri("https://date.nager.at/api/v3/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RequestValidator>();
builder.Services.AddScoped<RequestService>();
builder.Services.AddScoped<ProjectionService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<AccuralService>();
builder.Services.AddScoped<AccountStatusService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddControllers();

var app = builder.Build();

// only an admin can create users, so on an empty db the first admin (system owner) is created here.
// email and password come from the environment (docker-compose), with defaults for local development
using (var scope = app.Services.CreateScope())
{
    Database scoped_db = scope.ServiceProvider.GetRequiredService<Database>();
    if (!scoped_db.users.Any(u => u.role == Role.Admin))
    {
        string admin_email = builder.Configuration["ADMIN_EMAIL"] ?? "admin@be.dk";
        string admin_password = builder.Configuration["ADMIN_PASSWORD"] ?? "admin";
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        // the admin is an employee too, they get the company's standard working week
        Schedule schedule = HolidayParameters.get_rule_set(today).get_standard_schedule();
        User admin = new User("System owner", admin_email, admin_password, Role.Admin, today);
        await scope.ServiceProvider.GetRequiredService<UserService>().create_user(admin, schedule);
    }
}

app.UseCors("React");
app.UseOpenApi();
app.UseSwaggerUi();
app.UseSession();
app.MapControllers();
app.Run();
