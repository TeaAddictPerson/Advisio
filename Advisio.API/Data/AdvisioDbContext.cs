using Microsoft.EntityFrameworkCore;
using Advisio.API.Models;

namespace Advisio.API.Data;

public class AdvisioDbContext : DbContext
{
    public AdvisioDbContext(DbContextOptions<AdvisioDbContext> options) : base(options)
    {
    }

    public DbSet<Place> Places { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<PlaceCategory> PlaceCategories { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Survey> Surveys { get; set; }
    public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
    public DbSet<SurveyAnswer> SurveyAnswers { get; set; }
    public DbSet<UserSurveyResponse> UserSurveyResponses { get; set; }
    public DbSet<Recommendation> Recommendations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настройка связи многие-ко-многим между Place и Category
        modelBuilder.Entity<PlaceCategory>()
            .HasKey(pc => new { pc.PlaceId, pc.CategoryId });

        modelBuilder.Entity<PlaceCategory>()
            .HasOne(pc => pc.Place)
            .WithMany(p => p.PlaceCategories)
            .HasForeignKey(pc => pc.PlaceId);

        modelBuilder.Entity<PlaceCategory>()
            .HasOne(pc => pc.Category)
            .WithMany(c => c.PlaceCategories)
            .HasForeignKey(pc => pc.CategoryId);

        // Уникальность избранного для пользователя и места
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.PlaceId })
            .IsUnique();

        // Уникальность лайка для пользователя и места
        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.UserId, l.PlaceId })
            .IsUnique();

        // Уникальность ответа пользователя на вопрос
        modelBuilder.Entity<UserSurveyResponse>()
            .HasIndex(usr => new { usr.UserId, usr.QuestionId })
            .IsUnique();

        // Настройка внешних ключей для UserSurveyResponse с явным указанием поведения удаления
        modelBuilder.Entity<UserSurveyResponse>()
            .HasOne(usr => usr.Question)
            .WithMany()
            .HasForeignKey(usr => usr.QuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Индексы для оптимизации запросов
        modelBuilder.Entity<Place>()
            .HasIndex(p => p.Name);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Recommendation>()
            .HasIndex(r => new { r.UserId, r.PlaceId })
            .IsUnique();
    }
}

