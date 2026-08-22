# Prompts

Prompt files live at `prompts/{taskType}/{version}.md` and are loaded by version, never inlined in C#.

Task types: `CvExtraction`, `SkillNormalization`, `JdCvMatching`, `InterviewQuestionGen`, `InterviewLiveTurn`, `InterviewEvaluation`, `RecruiterSummary`, `Embedding`.

Phase 0 ships only this layout. Templates arrive in Phase 1. Each file that is added must have a `PromptVersion` row (content hash, created-at, active flag). Scores store the prompt version that produced them and are not recomputed when a prompt changes.
