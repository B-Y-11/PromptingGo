using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PhoneOtpChallengeConfiguration
    : IEntityTypeConfiguration<PhoneOtpChallenge>
{
    public void Configure(
        EntityTypeBuilder<PhoneOtpChallenge> builder)
    {
        builder.ToTable("PhoneOtpChallenges");

        builder.HasKey(challenge =>
            challenge.Id);

        builder.Property(challenge =>
                challenge.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(challenge =>
                challenge.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(challenge =>
                challenge.CodeHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(challenge =>
                challenge.CreatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.Property(challenge =>
                challenge.FailedAttempts)
            .HasDefaultValue(0);

        // ---------------------------------------------------------------------
        // EN: Existing user is optional because registration happens
        //     before an ApplicationUser exists.
        // RU: Пользователь необязателен, потому что при регистрации
        //     ApplicationUser ещё не существует.
        // ---------------------------------------------------------------------

        builder.HasOne(challenge =>
                challenge.User)
            .WithMany()
            .HasForeignKey(challenge =>
                challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---------------------------------------------------------------------
        // EN: Supports resend limits and request-rate checks per phone.
        // RU: Индекс для cooldown и лимитов запросов по номеру телефона.
        // ---------------------------------------------------------------------

        builder.HasIndex(challenge =>
            new
            {
                challenge.PhoneNumber,
                challenge.Purpose,
                challenge.CreatedAtUtc
            });

        // ---------------------------------------------------------------------
        // EN: Supports cleanup queries for expired OTP challenges.
        // RU: Используется запросами очистки просроченных OTP-проверок.
        // ---------------------------------------------------------------------

        builder.HasIndex(challenge =>
            challenge.ExpiresAtUtc);

        // ---------------------------------------------------------------------
        // EN: At most one current challenge per phone and purpose.
        // RU: Не более одной текущей проверки для номера и назначения.
        // ---------------------------------------------------------------------

        builder.HasIndex(challenge =>
            new
            {
                challenge.PhoneNumber,
                challenge.Purpose
            })
            .IsUnique()
            .HasFilter(
                "\"ConsumedAtUtc\" IS NULL AND " +
                "\"InvalidatedAtUtc\" IS NULL");
    }
}
