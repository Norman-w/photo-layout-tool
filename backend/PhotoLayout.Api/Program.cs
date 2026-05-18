using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PhotoLayout.Api.Data;
using PhotoLayout.Api.Options;
using PhotoLayout.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MattingOptions>(builder.Configuration.GetSection(MattingOptions.SectionName));
builder.Services.AddSingleton<OnnxMattingService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=photolayout.db"));

builder.Services.AddScoped<ImageProcessingService>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddOpenApi();

builder.Services.AddCors(o =>
{
    o.AddPolicy("frontend", p =>
    {
        p.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:10000",
                "https://norman.wang",
                "http://norman.wang")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("frontend");
app.UseAuthorization();
app.MapControllers();

var wwwRoot = Path.Combine(app.Environment.ContentRootPath, "www");
if (Directory.Exists(wwwRoot))
{
    var wwwFiles = new PhysicalFileProvider(wwwRoot);
    var defaultFiles = new DefaultFilesOptions { FileProvider = wwwFiles };
    defaultFiles.DefaultFileNames.Add("index.html");
    app.UseDefaultFiles(defaultFiles);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = wwwFiles });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = wwwFiles });
}

app.Run();
