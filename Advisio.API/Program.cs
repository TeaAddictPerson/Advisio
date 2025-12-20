using System;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AdvisioDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IExternalPlaceService, ExternalPlaceService>();
builder.Services.AddHttpClient<IExternalPlaceService, ExternalPlaceService>();

// Translation Service
builder.Services.AddHttpClient<ITranslationService, TranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// PredictHQ Service
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

// Places Sync Service (Background Service)
builder.Services.AddHostedService<PlacesSyncService>();
builder.Services.AddScoped<IPlacesSyncService, PlacesSyncService>();

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Отключено для работы без SSL
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
