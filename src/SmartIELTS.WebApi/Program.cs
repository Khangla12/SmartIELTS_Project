using SmartIELTS.Infrastructure;
using SmartIELTS.Infrastructure.Data;
using SmartIELTS.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to container
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SmartIELTS Examination System API",
        Version = "v1",
        Description = "Hệ thống Khảo thí Tiếng Anh Toàn diện IELTS 4 Kỹ năng (SWD392 Architecture & PRN222 .NET 8)"
    });
});

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. Register Clean Architecture Infrastructure Services (Dual Database Support: SQLite default / SQL Server)
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// 3. Auto initialize database and seed sample IELTS Academic 4-skill exam
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SmartIeltsDbSeeder.SeedAsync(db);
        Console.WriteLine("[SmartIELTS] Database and seed data initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SmartIELTS] Database initialization error: {ex.Message}");
    }
}

// 4. Configure HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartIELTS API V1");
    c.RoutePrefix = string.Empty; // Open Swagger UI directly at root "/"
});

app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ExamHub>("/hubs/exam");
app.MapHub<ProctoringHub>("/hubs/proctoring");

app.Run();
