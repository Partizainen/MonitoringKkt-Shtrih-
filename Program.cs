using KKTMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка Kestrel для работы на всех интерфейсах
builder.WebHost.UseUrls("http://0.0.0.0:5010");

// Добавление сервисов
builder.Services.AddControllers();
builder.Services.AddSingleton<DbContext>();
builder.Services.AddScoped<KktService>();
builder.Services.AddSingleton<KktDriverService>();
builder.Services.AddSingleton<KktStateService>();
builder.Services.AddHostedService<KktPoller>();
builder.Services.AddScoped<LegalEntityService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<ZabbixService>();

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Базовый путь для подпапки (если сайт работает в /kktmonitor)
app.UsePathBase("/kktmonitor");

app.UseCors("AllowAll");

// Статические файлы
app.UseDefaultFiles();
app.UseStaticFiles();

// Маршрутизация
app.UseRouting();

// Контроллеры API
app.MapControllers();

// Fallback для SPA
app.MapFallbackToFile("index.html");

// Логирование запросов в консоль
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
    await next();
});

app.Run();