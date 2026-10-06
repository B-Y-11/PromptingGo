using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class UserNotificationConfiguration
    : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(
        EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        builder.HasKey(notification =>
            notification.Id);

        builder.Property(notification =>
                notification.Type)
            .HasConversion<string>()
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(notification =>
                notification.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(notification =>
                notification.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(notification =>
                notification.Url)
            .HasMaxLength(500);

        builder.Property(notification =>
                notification.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(notification =>
            new
            {
                notification.UserId,
                notification.ReadAtUtc,
                notification.CreatedAtUtc
            });

        builder.HasOne(notification =>
                notification.User)
            .WithMany(user =>
                user.Notifications)
            .HasForeignKey(notification =>
                notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
