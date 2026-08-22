using HireLens.SharedKernel;

namespace HireLens.Modules.Interview.Domain;

public sealed class InterviewSession : ITenantEntity
{
    private readonly List<InterviewQuestion> _questions = [];
    private readonly List<InterviewTurn> _turns = [];

    private InterviewSession()
    {
        Status = "invited";
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid PositionId { get; private set; }

    public string Status { get; private set; }

    public string TokenHash { get; private set; }

    public bool DisclosureAccepted { get; private set; }

    public int? InterviewScore { get; private set; }

    public string? Summary { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<InterviewQuestion> Questions => _questions;

    public IReadOnlyCollection<InterviewTurn> Turns => _turns;

    public void BindToken(string tokenHash) => TokenHash = Guard.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));

    public static InterviewSession Invite(
        Guid tenantId,
        Guid candidateId,
        Guid positionId,
        string tokenHash,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            PositionId = positionId,
            TokenHash = tokenHash,
            Status = "invited",
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

    public Result AcceptDisclosure()
    {
        DisclosureAccepted = true;
        Status = "disclosed";
        return Result.Success();
    }

    public Result Start()
    {
        if (!DisclosureAccepted)
        {
            return Result.Failure(Error.Validation("AI disclosure consent is required before the interview starts."));
        }

        Status = "in_progress";
        return Result.Success();
    }

    public Result Pause()
    {
        if (Status is not "in_progress")
        {
            return Result.Failure(Error.Validation("Only an in-progress interview can be paused."));
        }

        Status = "paused";
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status is not "paused")
        {
            return Result.Failure(Error.Validation("Only a paused interview can be resumed."));
        }

        Status = "in_progress";
        return Result.Success();
    }

    public Result AddQuestion(Guid criterionId, string prompt, int order)
    {
        if (criterionId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Every interview question must be bound to a criterion."));
        }

        _questions.Add(InterviewQuestion.Create(TenantId, Id, criterionId, prompt, order));
        return Result.Success();
    }

    public InterviewTurn AddTurn(string role, string text, Guid? questionId, DateTimeOffset now)
    {
        var turn = InterviewTurn.Create(TenantId, Id, role, text, questionId, now);
        _turns.Add(turn);
        return turn;
    }

    public void Complete(int? score, string? summary)
    {
        InterviewScore = score;
        Summary = summary;
        Status = "completed";
    }
}

public sealed class InterviewQuestion : ITenantEntity
{
    private InterviewQuestion()
    {
        Prompt = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid CriterionId { get; private set; }

    public string Prompt { get; private set; }

    public int Order { get; private set; }

    public static InterviewQuestion Create(Guid tenantId, Guid sessionId, Guid criterionId, string prompt, int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            CriterionId = criterionId,
            Prompt = Guard.NotNullOrWhiteSpace(prompt, nameof(prompt)),
            Order = order
        };
}

public sealed class InterviewTurn : ITenantEntity
{
    private InterviewTurn()
    {
        Role = string.Empty;
        Text = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid? QuestionId { get; private set; }

    public string Role { get; private set; }

    public string Text { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static InterviewTurn Create(
        Guid tenantId,
        Guid sessionId,
        string role,
        string text,
        Guid? questionId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            QuestionId = questionId,
            Role = role,
            Text = Guard.NotNullOrWhiteSpace(text, nameof(text)),
            CreatedAt = now
        };
}
