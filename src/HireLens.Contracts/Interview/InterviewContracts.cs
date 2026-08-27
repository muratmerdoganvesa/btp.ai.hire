namespace HireLens.Contracts.Interview;

public sealed record InterviewInviteRequest(Guid CandidateId, Guid PositionId, string? VideoMeetingUrl = null);

public sealed record InterviewInviteDto(
    Guid SessionId,
    string InviteUrl,
    DateTimeOffset ExpiresAt,
    string? VideoMeetingUrl = null);

public sealed record InterviewQuestionDto(Guid Id, Guid CriterionId, string Prompt, int Order);

public sealed record InterviewTurnDto(Guid Id, string Role, string Text, Guid? QuestionId, DateTimeOffset CreatedAt);

public sealed record InterviewFrameDto(
    Guid Id,
    Guid? QuestionId,
    Guid? TurnId,
    string ContentType,
    string ImageBase64,
    DateTimeOffset CapturedAt);

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
    DateTimeOffset? ExpiresAt = null,
    IReadOnlyList<InterviewFrameDto>? Frames = null,
    string? CandidateName = null,
    string? PositionTitle = null,
    DateTimeOffset? CreatedAt = null);

/// <summary>Recruiter board row for sent AI pre-interviews.</summary>
public sealed record InterviewBoardItemDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    Guid PositionId,
    string PositionTitle,
    string Status,
    int? InterviewScore,
    int QuestionCount,
    int AnswerCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record InterviewAnswerRequest(string Text, IReadOnlyList<string>? FramesBase64 = null);

public sealed record InterviewPrepDto(
    string WhatToExpect,
    int EstimatedMinutes,
    string DataUse,
    bool DisclosureRequired,
    string? VideoMeetingUrl = null,
    DateTimeOffset? ExpiresAt = null);
