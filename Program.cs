using OcrDashboardMvc.Database;
using OcrDashboardMvc.Services;
using OcrDashboardMvc.Repositories;
using OcrDashboardMvc.ModelBinders;
using Serilog;
using Serilog.Events;
using System;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Extensions", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("OcrDashboardMvc", LogEventLevel.Information)
    // Loại bỏ Data Protection warnings
    .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection.KeyManagement", LogEventLevel.Error)
    // Loại bỏ HTTPS redirect warnings
    .MinimumLevel.Override("Microsoft.AspNetCore.HttpsPolicy", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/ocr-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();
    
    // Configure culture for date format (dd/MM/yyyy)
    var cultureInfo = new CultureInfo("vi-VN");
    cultureInfo.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
    CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
    CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
    
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture(cultureInfo);
        options.SupportedCultures = new[] { cultureInfo };
      options.SupportedUICultures = new[] { cultureInfo };
    });
    
    builder.Services.AddControllersWithViews(options =>
    {
        // Thêm custom model binder cho date fields
        options.ModelBinderProviders.Insert(0, new DateModelBinderProvider());
    });

    builder.Services.AddHttpClient<ISqlApiProxyDatabase, SqlApiProxyDatabase>(client =>
    {
        client.BaseAddress = new Uri("http://ocr-webservice-895703810.ap-southeast-1.elb.amazonaws.com/"); //47.130.48.97
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddScoped<IDatabaseService, DatabaseService>();
    builder.Services.AddScoped<ISFTPOcrFileRepository, SFTPOcrFileRepository>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    builder.Services.AddScoped<PetaPoco.IDatabase>(sp =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        // Thay đổi ProviderName phù hợp (ví dụ: "System.Data.SqlClient" hoặc "Microsoft.Data.SqlClient")
        return new PetaPoco.Database(connectionString, "System.Data.SqlClient");
    });

    var app = builder.Build();

    // Use localization
    app.UseRequestLocalization();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseAuthorization();
    app.MapStaticAssets();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng khởi động thất bại");
}
finally
{
    Log.CloseAndFlush();
}
