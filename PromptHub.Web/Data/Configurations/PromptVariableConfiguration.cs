using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptVariableConfiguration
    : IEntityTypeConfiguration<PromptVariable>
{
    public void Configure(EntityTypeBuilder<PromptVariable> builder)
    {
        builder.ToTable("PromptVariables");

        builder.HasKey(variable => variable.Id);

        builder.Property(variable => variable.Key)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(variable => variable.Label)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(variable => variable.VariableType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(variable => variable.Placeholder)
            .HasMaxLength(300)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(variable => variable.HelpText)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(variable => variable.DefaultValue)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(variable => variable.OptionsJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();

        builder.Property(variable => variable.IsRequired)
            .HasDefaultValue(true);

        builder.Property(variable => variable.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(variable => new
        {
            variable.PromptVersionId,
            variable.Key
        })
        .IsUnique();

        builder.HasIndex(variable => new
        {
            variable.PromptVersionId,
            variable.SortOrder
        });

        builder.HasOne(variable => variable.PromptVersion)
            .WithMany(version => version.Variables)
            .HasForeignKey(variable => variable.PromptVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
