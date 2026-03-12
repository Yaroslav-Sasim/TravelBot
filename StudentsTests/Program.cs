using Microsoft.EntityFrameworkCore;
using StudentsTests.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ======================
// SERVICES (ВСЁ ДО Build)
// ======================

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<TelegramService>();

// ======================

var app = builder.Build();

// ======================
// PORT для Render
// ======================

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

// ======================

if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "StudentsTests API is running!");

app.Run();