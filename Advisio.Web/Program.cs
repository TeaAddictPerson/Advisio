using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Sessions для аутентификации
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Database
builder.Services.AddDbContext<AdvisioDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), 
        b => b.MigrationsAssembly("Advisio.Web")
             .EnableRetryOnFailure()));

// Services
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddHttpClient<IExternalPlaceService, ExternalPlaceService>();

// Background Service для синхронизации мест
var syncEnabled = builder.Configuration.GetValue<bool>("PlacesSync:Enabled", true);
if (syncEnabled)
{
    builder.Services.AddHostedService<PlacesSyncService>();
}

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5194")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

// app.UseHttpsRedirection(); // Отключено для работы без SSL
app.UseStaticFiles();

// Serve React app static files (if built)
if (Directory.Exists(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "dist")))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "dist")),
        RequestPath = "/dist"
    });
}

app.UseRouting();

app.UseCors("AllowAll");
app.UseSession();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Catch-all route for React app (client-side routing)
// Only map if React app is built
if (Directory.Exists(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "dist")))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "dist"))
    });
}

app.Run();
