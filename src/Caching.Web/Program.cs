
var builder = WebApplication.CreateBuilder(args);

CreateLogger();

builder.WebHost.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<ICityDataCache, CityDataCache>();
builder.Services.AddHttpClient<IZipCodeServiceClient, ZipCodeServiceClient>(client => 
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ZipCodeServiceUrl"));
});
builder.Services.AddTransient<IZipCodeService, ZipCodeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

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
    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Caching.Web");
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
