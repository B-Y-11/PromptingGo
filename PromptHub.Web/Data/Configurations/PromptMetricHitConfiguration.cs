using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptMetricHitConfiguration
    : IEntityTypeConfiguration<PromptMetricHit>
{
    public void Configure(
        EntityTypeBuilder<PromptMetricHit> builder)
    {
        builder.ToTable(
            "PromptMetricHits");

        builder.HasKey(hit =>
            hit.Id);

        builder.Property(hit =>
                hit.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(hit =>
                hit.ActorKeyHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(hit =>
                hit.BucketStartUtc)
            .IsRequired();

        builder.Property(hit =>
                hit.CreatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        // ---------------------------------------------------------------------
        // EN: Only one metric hit per actor/prompt/type/time bucket.
        // RU: Только одно событие на пользователя/промпт/тип/временное окно.
        // ---------------------------------------------------------------------

        builder.HasIndex(hit => new
        {
            hit.PromptId,
            hit.Kind,
            hit.ActorKeyHash,
            hit.BucketStartUtc
        })
            .IsUnique();

        builder.HasIndex(hit =>
            hit.CreatedAtUtc);

        builder.HasOne(hit =>
                hit.Prompt)
            .WithMany()
            .HasForeignKey(hit =>
                hit.PromptId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}
