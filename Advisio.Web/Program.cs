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
builder.Services.AddScoped<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddScoped<IKufarService, KufarService>();
builder.Services.AddHttpClient<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddHttpClient<IKufarService, KufarService>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
