using Heartbeat.Server.Data;
using Heartbeat.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<AppService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<InputEventService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddHealthChecks();

// 鉴权已暂时禁用（用户要求 "暂时不需要登录功能"）。
// 恢复时取消以下注释并添加对应 using：
//   using Microsoft.AspNetCore.Authentication.JwtBearer;
//   using Microsoft.IdentityModel.Tokens;
// var authSection = builder.Configuration.GetSection("AuthService");
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options => { ... });
// 同时恢复 app.UseAuthentication(); app.UseAuthorization(); 以及 controller 上的 [Authorize] 属性。

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 全环境启动时自动应用迁移（见 ADR-013，取代 ADR-007）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// app.UseAuthentication(); // 暂时禁用
// app.UseAuthorization();  // 暂时禁用

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
