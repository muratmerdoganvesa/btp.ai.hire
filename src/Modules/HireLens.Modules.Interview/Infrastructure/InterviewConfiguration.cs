using HireLens.Modules.Interview.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Interview.Infrastructure;

public sealed class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> builder)
    {
        builder.ToTable("InterviewSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Status).HasMaxLength(32).IsRequired();
        builder.Property(s => s.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasMany(s => s.Questions).WithOne().HasForeignKey(q => q.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Turns).WithOne().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Questions).HasField("_questions").AutoInclude();
        builder.Navigation(s => s.Turns).HasField("_turns").AutoInclude();
    }
}

public sealed class InterviewQuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> builder)
    {
        builder.ToTable("InterviewQuestions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Prompt).HasMaxLength(2000).IsRequired();
    }
}

public sealed class InterviewTurnConfiguration : IEntityTypeConfiguration<InterviewTurn>
{
    public void Configure(EntityTypeBuilder<InterviewTurn> builder)
    {
        builder.ToTable("InterviewTurns");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Role).HasMaxLength(16).IsRequired();
        // HANA NVARCHAR max identifier length is 5000; longer values need NCLOB.
        builder.Property(t => t.Text).HasMaxLength(5000).IsRequired();
    }
}
