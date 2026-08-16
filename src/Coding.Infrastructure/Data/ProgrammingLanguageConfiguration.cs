using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class ProgrammingLanguageConfiguration : IEntityTypeConfiguration<ProgrammingLanguage>
{
    public void Configure(EntityTypeBuilder<ProgrammingLanguage> builder)
    {
        builder.ToTable("ProgrammingLanguages");
        builder.HasQueryFilter(language => !language.IsDeleted);
        builder.Property(language => language.Name).HasMaxLength(50).IsRequired();
        builder.Property(language => language.Slug).HasMaxLength(50).IsRequired();
        builder.HasIndex(language => language.Name).IsUnique();
        builder.HasIndex(language => language.Slug).IsUnique();
        builder.HasData(
            Language("10000000-0000-0000-0000-000000000001", "TypeScript", "typescript", 10),
            Language("10000000-0000-0000-0000-000000000002", "C#", "csharp", 20),
            Language("10000000-0000-0000-0000-000000000003", "Python", "python", 30),
            Language("10000000-0000-0000-0000-000000000004", "Java", "java", 40),
            Language("10000000-0000-0000-0000-000000000005", "Go", "go", 50),
            Language("10000000-0000-0000-0000-000000000006", "Other", "other", 1000));
    }

    private static ProgrammingLanguage Language(string id, string name, string slug, int order) => new()
    {
        ID = Guid.Parse(id), Name = name, Slug = slug, SortOrder = order, IsActive = true,
        CreatAt = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
    };
}
