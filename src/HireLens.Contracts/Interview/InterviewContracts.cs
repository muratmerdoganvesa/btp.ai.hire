namespace HireLens.Contracts.Interview;

public sealed record InterviewInviteRequest(Guid CandidateId, Guid PositionId, string? VideoMeetingUrl = null);

public sealed record InterviewInviteDto(
    Guid SessionId,
    string InviteUrl,
    DateTimeOffset ExpiresAt,
    string? VideoMeetingUrl = null);

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
    string? Summary,
    string? VideoMeetingUrl = null,
    DateTimeOffset? ExpiresAt = null);

public sealed record InterviewAnswerRequest(string Text);

public sealed record InterviewPrepDto(
    string WhatToExpect,
    int EstimatedMinutes,
    string DataUse,
    bool DisclosureRequired,
    string? VideoMeetingUrl = null,
    DateTimeOffset? ExpiresAt = null);
