using ContainerManager.Middleware;
using ContainerManager.Services;
using ContainerManager.Services.Mock;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var useMock = builder.Configuration.GetValue<bool>("AppSettings:UseMock");
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ShellService>();
builder.Services.AddSingleton<DbService>();
builder.Services.AddSingleton<StartupSyncService>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (useMock)
{
    builder.Services.AddSingleton<IMigService, MockMigService>();
    builder.Services.AddSingleton<ILxcService, MockLxcService>();
}
else
{
    builder.Services.AddSingleton<IMigService, MigService>();
    builder.Services.AddSingleton<ILxcService, LxcService>();
}


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sync = scope.ServiceProvider.GetRequiredService<StartupSyncService>();
    sync.Sincronizza();
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseRouting();
app.UseMiddleware<IpWhitelistMiddleware>();
app.UseMiddleware<HmacAuthMiddleware>();
app.MapControllers();

app.Run();
