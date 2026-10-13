using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sonrisa.EventAlertSystem.Api.Domain;

namespace Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleKeywordConfiguration : IEntityTypeConfiguration<AlertRuleKeyword>
{
    public void Configure(EntityTypeBuilder<AlertRuleKeyword> builder)
    {
        builder.ToTable("AlertRuleKeywords");
        builder.HasKey(keyword => keyword.Id);
        builder.Property(keyword => keyword.Keyword).HasMaxLength(100).UseCollation("NOCASE").IsRequired();
        builder.HasIndex(keyword => new { keyword.AlertRuleId, keyword.Keyword }).IsUnique();
    }
}