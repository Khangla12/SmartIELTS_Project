using System.Text;
using System.Text.Json.Serialization;
using SmartIELTS.Domain.Common;
using SmartIELTS.Infrastructure;
using SmartIELTS.Infrastructure.Data;
using SmartIELTS.WebApi.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to container
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartIELTS Examination System API",
        Version = "v1",
        Description = "Hệ thống Khảo thí Tiếng Anh Toàn diện IELTS 4 Kỹ năng (SWD392 Architecture & PRN222 .NET 8)"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT lấy từ /api/auth/login (không cần prefix 'Bearer ').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
});

// 2. JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);

string jwtKey = jwtSection["Key"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(jwtKey))
{
    // Chỉ dùng khóa phát triển khi cấu hình thiếu, để API vẫn chạy được khi demo.
    jwtKey = "SmartIELTS_Development_Signing_Key_Change_In_Production_2026";
    Console.WriteLine("[SmartIELTS] Cảnh báo: Chưa cấu hình Jwt:Key, đang dùng khóa phát triển mặc định.");
}

var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Key = jwtKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSignalR();
builder.Services.AddSingleton<SmartIELTS.WebApi.Realtime.ProctoringSignalRBridge>();

// 3. CORS: chỉ cho phép origin được khai báo (không dùng AllowAnyOrigin + AllowCredentials)
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5294", "https://localhost:7071", "http://localhost:7105" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 4. Register Clean Architecture Infrastructure Services (Dual Database Support: SQLite default / SQL Server)
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Khởi tạo cầu nối Observer -> SignalR ngay khi app chạy để đăng ký subscriber.
_ = app.Services.GetRequiredService<SmartIELTS.WebApi.Realtime.ProctoringSignalRBridge>();

// 5. Auto initialize database and seed sample IELTS Academic 4-skill exam
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SmartIeltsSchemaGuard.EnsureAsync(db,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SmartIELTS.SchemaGuard"));
        await SmartIeltsDbSeeder.SeedAsync(db);
        Console.WriteLine("[SmartIELTS] Database and seed data initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SmartIELTS] Database initialization error: {ex.Message}");
    }
}

// 6. Configure HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartIELTS API V1");
    c.RoutePrefix = "swagger";
});

// Redirect root to /swagger so both http://localhost:5294/ and http://localhost:5294/swagger work
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ExamHub>("/hubs/exam");
app.MapHub<ProctoringHub>("/hubs/proctoring");

app.Run();
