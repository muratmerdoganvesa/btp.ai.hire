namespace HireLens.AiGateway;

public interface IAiGateway
{
    Task<AiResult<T>> ExecuteAsync<T>(
        AiTaskType taskType,
        PromptContext context,
        AiOptions? options = null,
        CancellationToken ct = default);
}
