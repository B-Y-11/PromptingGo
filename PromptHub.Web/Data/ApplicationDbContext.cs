using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Prompt> Prompts => Set<Prompt>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<PromptTag> PromptTags => Set<PromptTag>();

    public DbSet<PromptVersion> PromptVersions
    => Set<PromptVersion>();

    public DbSet<PromptVariable> PromptVariables
        => Set<PromptVariable>();

    public DbSet<PromptFavorite> PromptFavorites
    => Set<PromptFavorite>();

    public DbSet<PromptRating> PromptRatings
    => Set<PromptRating>();

    public DbSet<PromptReport> PromptReports
    => Set<PromptReport>();

    public DbSet<PromptComment> PromptComments
    => Set<PromptComment>();

    public DbSet<PromptCommentReport> PromptCommentReports
    => Set<PromptCommentReport>();

    public DbSet<PromptMedia> PromptMediaItems
    => Set<PromptMedia>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<PhoneOtpChallenge> PhoneOtpChallenges
    => Set<PhoneOtpChallenge>();

    public DbSet<PromptMetricHit> PromptMetricHits
    => Set<PromptMetricHit>();

    public DbSet<AuthorSupportMethod> AuthorSupportMethods
    => Set<AuthorSupportMethod>();


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // -----------------------------------------------------------------------------
        // EN: PostgreSQL trigram similarity support for duplicate prompt detection.
        // RU: Поддержка триграмм PostgreSQL для поиска похожих промптов.
        // -----------------------------------------------------------------------------

        builder.HasPostgresExtension(
            "pg_trgm");

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(80)
                .HasDefaultValue(string.Empty);

            entity.Property(user => user.Bio)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);


            entity.Property(user =>
                    user.PhoneNumber)
                .HasMaxLength(20);

            entity.HasIndex(user =>
                    user.PhoneNumber)
                .IsUnique()
                .HasFilter(
                    "\"PhoneNumber\" IS NOT NULL");


            // ---------------------------------------------------------------------
            // EN: Email is optional but unique whenever it exists.
            // RU: Email необязателен, но уникален, если указан.
            // ---------------------------------------------------------------------

            entity.HasIndex(user =>
                    user.NormalizedEmail)
                .HasDatabaseName(
                    "EmailIndex")
                .IsUnique()
                .HasFilter(
                    "\"NormalizedEmail\" IS NOT NULL");




            entity.Property(user =>
                    user.AvatarStorageKey)
                .HasMaxLength(500);

            entity.Property(user =>
                    user.AvatarContentType)
                .HasMaxLength(100);

            entity.Property(user =>
                    user.AvatarFileSizeBytes);

            entity.Property(user =>
                    user.AvatarWidth);

            entity.Property(user =>
                    user.AvatarHeight);

            entity.Property(user =>
                    user.AvatarUpdatedAtUtc);

            entity.Property(user => user.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    // -----------------------------------------------------------------------------
    // EN: Application-managed concurrency tokens
    // RU: Управляемые приложением токены конкурентного доступа
    // -----------------------------------------------------------------------------

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        RefreshPromptMediaConcurrencyTokens();

        return base.SaveChanges(
            acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RefreshPromptMediaConcurrencyTokens();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void RefreshPromptMediaConcurrencyTokens()
    {
        var mediaEntries =
            ChangeTracker
                .Entries<PromptMedia>()
                .Where(entry =>
                    entry.State is
                        EntityState.Added or
                        EntityState.Modified);

        foreach (var entry in mediaEntries)
        {
            entry.Entity.ConcurrencyToken =
                Guid.NewGuid();
        }
    }
}
