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
    public DbSet<PlaceCollection> PlaceCollections { get; set; }
    public DbSet<PlaceCollectionItem> PlaceCollectionItems { get; set; }
    public DbSet<PlaceView> PlaceViews { get; set; }
    public DbSet<PlaceReview> PlaceReviews { get; set; }
    public DbSet<PlaceSubscription> PlaceSubscriptions { get; set; }
    public DbSet<CategorySubscription> CategorySubscriptions { get; set; }
    public DbSet<UserFilterUsage> UserFilterUsages { get; set; }
    public DbSet<PlaceVisit> PlaceVisits { get; set; }
    public DbSet<PlaceComment> PlaceComments { get; set; }

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

        modelBuilder.Entity<Place>()
            .Property(p => p.Latitude)
            .HasPrecision(9, 6);
        modelBuilder.Entity<Place>()
            .Property(p => p.Longitude)
            .HasPrecision(10, 7);

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
            .Property(r => r.Score)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Recommendation>()
            .HasIndex(r => new { r.UserId, r.PlaceId })
            .IsUnique();

        // Уникальность отзыва: один пользователь — один отзыв на место
        modelBuilder.Entity<PlaceReview>()
            .HasIndex(pr => new { pr.UserId, pr.PlaceId })
            .IsUnique();

        // Уникальность подписки на место
        modelBuilder.Entity<PlaceSubscription>()
            .HasIndex(ps => new { ps.UserId, ps.PlaceId })
            .IsUnique();

        // Уникальность подписки на категорию
        modelBuilder.Entity<CategorySubscription>()
            .HasIndex(cs => new { cs.UserId, cs.CategoryId })
            .IsUnique();

        // Уникальность посещения: один пользователь — одно посещение места (можно обновлять дату)
        modelBuilder.Entity<PlaceVisit>()
            .HasIndex(pv => new { pv.UserId, pv.PlaceId })
            .IsUnique();
    }
}

