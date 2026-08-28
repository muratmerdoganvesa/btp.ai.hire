using HireLens.SharedKernel;

namespace HireLens.Modules.Review.Domain;

public sealed class Offer : ITenantEntity
{
    public const int PackageMaxLength = 2000;
    public const int NoteMaxLength = 2000;

    private Offer()
    {
        Status = "draft";
        PackageText = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid PositionId { get; private set; }

    public string Status { get; private set; }

    public string PackageText { get; private set; }

    public string? Note { get; private set; }

    public int? ScoreSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public static Result<Offer> Draft(
        Guid tenantId,
        Guid candidateId,
        Guid positionId,
        string packageText,
        string? note,
        int? scoreSnapshot,
        DateTimeOffset now)
    {
        var package = NormalizePackage(packageText);
        if (package.IsFailure)
        {
            return Result.Failure<Offer>(package.Error);
        }

        var noteResult = NormalizeNote(note);
        if (noteResult.IsFailure)
        {
            return Result.Failure<Offer>(noteResult.Error);
        }

        return Result.Success(new Offer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            PositionId = positionId,
            Status = "draft",
            PackageText = package.Value,
            Note = noteResult.Value,
            ScoreSnapshot = scoreSnapshot,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public Result UpdateDraft(string packageText, string? note, DateTimeOffset now)
    {
        if (Status is not "draft")
        {
            return Result.Failure(Error.Conflict("Only a draft offer can be edited."));
        }

        var package = NormalizePackage(packageText);
        if (package.IsFailure)
        {
            return Result.Failure(package.Error);
        }

        var noteResult = NormalizeNote(note);
        if (noteResult.IsFailure)
        {
            return Result.Failure(noteResult.Error);
        }

        PackageText = package.Value;
        Note = noteResult.Value;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Send(DateTimeOffset now)
    {
        if (Status is not "draft")
        {
            return Result.Failure(Error.Conflict("Only a draft offer can be sent."));
        }

        Status = "sent";
        SentAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Accept(DateTimeOffset now)
    {
        if (Status is not "sent")
        {
            return Result.Failure(Error.Conflict("Only a sent offer can be accepted."));
        }

        Status = "accepted";
        RespondedAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Decline(DateTimeOffset now)
    {
        if (Status is not "sent")
        {
            return Result.Failure(Error.Conflict("Only a sent offer can be declined."));
        }

        Status = "declined";
        RespondedAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Withdraw(DateTimeOffset now)
    {
        if (Status is not ("draft" or "sent"))
        {
            return Result.Failure(Error.Conflict("Only a draft or sent offer can be withdrawn."));
        }

        Status = "withdrawn";
        UpdatedAt = now;
        return Result.Success();
    }

    private static Result<string> NormalizePackage(string packageText)
    {
        if (string.IsNullOrWhiteSpace(packageText))
        {
            return Result.Failure<string>(Error.Validation("Package text is required."));
        }

        var trimmed = packageText.Trim();
        if (trimmed.Length > PackageMaxLength)
        {
            return Result.Failure<string>(Error.Validation($"Package text cannot exceed {PackageMaxLength} characters."));
        }

        return Result.Success(trimmed);
    }

    private static Result<string?> NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return Result.Success<string?>(null);
        }

        var trimmed = note.Trim();
        if (trimmed.Length > NoteMaxLength)
        {
            return Result.Failure<string?>(Error.Validation($"Note cannot exceed {NoteMaxLength} characters."));
        }

        return Result.Success<string?>(trimmed);
    }
}
