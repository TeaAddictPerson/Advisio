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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Services
builder.Services.AddScoped<IPlaceImageSearchService, PlaceImageSearchService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddHttpClient(PredictHqEventsImportService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddScoped<IPredictHqEventsImportService, PredictHqEventsImportService>();

// Places Sync Service (Background Service)
builder.Services.AddHostedService<PlacesSyncService>();
builder.Services.AddHostedService<PlaceMaintenanceStartupHostedService>();
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
