using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class AuthorSupportMethodConfiguration
    : IEntityTypeConfiguration<AuthorSupportMethod>
{
    public void Configure(
        EntityTypeBuilder<AuthorSupportMethod> builder)
    {
        builder.ToTable(
            "AuthorSupportMethods");

        builder.HasKey(method =>
            method.Id);

        builder.Property(method =>
                method.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(method =>
                method.ExternalReference)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(method =>
                method.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(method =>
                method.CreatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.Property(method =>
                method.UpdatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.HasIndex(method => new
        {
            method.UserId,
            method.Provider
        })
        .IsUnique();

        builder.HasOne(method =>
                method.User)
            .WithMany(user =>
                user.SupportMethods)
            .HasForeignKey(method =>
                method.UserId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}
