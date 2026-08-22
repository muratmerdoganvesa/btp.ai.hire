namespace HireLens.Contracts.Interview;

public sealed record InterviewInviteRequest(Guid CandidateId, Guid PositionId);

public sealed record InterviewInviteDto(Guid SessionId, string InviteUrl, DateTimeOffset ExpiresAt);

public sealed record InterviewQuestionDto(Guid Id, Guid CriterionId, string Prompt, int Order);

public sealed record InterviewTurnDto(Guid Id, string Role, string Text, Guid? QuestionId, DateTimeOffset CreatedAt);

public sealed record InterviewSessionDto(
    Guid Id,
    Guid CandidateId,
    Guid PositionId,
    string Status,
    bool DisclosureAccepted,
    int? InterviewScore,
    IReadOnlyList<InterviewQuestionDto> Questions,
    IReadOnlyList<InterviewTurnDto> Turns,
    string? Summary);

public sealed record InterviewAnswerRequest(string Text);

public sealed record InterviewPrepDto(
    string WhatToExpect,
    int EstimatedMinutes,
    string DataUse,
    bool DisclosureRequired);
