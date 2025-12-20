using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.Services;

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
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddHttpClient<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddHttpClient<ITranslationService, TranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IPredictHQService, PredictHQService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IPredictHQService>(sp =>
{
    var context = sp.GetRequiredService<AdvisioDbContext>();
    var placeService = sp.GetRequiredService<IPlaceService>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IPredictHQService));
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetService<ILogger<PredictHQService>>();
    var translationService = sp.GetService<ITranslationService>();
    return new PredictHQService(context, placeService, httpClient, configuration, logger, translationService);
});

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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

app.UseRouting();

app.UseCors("AllowAll");
app.UseSession();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
