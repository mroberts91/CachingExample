
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

CreateLogger();

builder.WebHost.UseSerilog();


builder.Services.AddDbContext<ApplicationDbContext>(options => 
{
    options.UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking);
    options.UseSqlite("Data Source=Data/zip_code_data.db");

}, ServiceLifetime.Transient);
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Caching.Service", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Caching.Service v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

try
{
    LogStartupInfo(app);

    await app.RunAsync();
    await app.WaitForShutdownAsync().ContinueWith(task =>
    {
        Log.Information("{msg}", "Host Shutting Down ...");
        return task;
    });
}
catch (Exception ex)
{
    Log.Fatal(ex, "{msg}", "Host Terminated Unexpectedly ...");
    await app.StopAsync();
}
finally
{
    Log.CloseAndFlush();
}


static void CreateLogger()
{
    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Caching.Service");
    if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);

    Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(new(LogEventLevel.Information))
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    theme: AnsiConsoleTheme.Literate)
                .WriteTo.File(
                    path: Path.Combine(logPath, "log-.log"),
                    rollOnFileSizeLimit: true,
                    rollingInterval: RollingInterval.Hour,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(10),
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
                .CreateLogger();
}

static void LogStartupInfo(WebApplication? app)
{
    if (app is null)
        return;

    var urls =
        app.Urls is ICollection<string> appUrls && appUrls.Any()
        ? appUrls
        : app.Configuration.GetValue<string>("ASPNETCORE_URLS")?.Split(";") ?? Enumerable.Empty<string>();

    Log.Logger.Information("Application Name: {name}", app.Environment.ApplicationName);
    Log.Logger.Information("Environment Name: {name}", app.Environment.EnvironmentName);
    Log.Logger.Information("Application Available at: {urls}", string.Join(";", urls));
    Log.Logger.Information("Web Root Path: {path}", app.Environment.WebRootPath);
    Log.Logger.Information("Content Root Path: {path}", app.Environment.ContentRootPath);
}