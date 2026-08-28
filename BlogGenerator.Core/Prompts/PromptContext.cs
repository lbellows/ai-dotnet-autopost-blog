namespace BlogGenerator.Core.Prompts;

public sealed record PromptContext(
    DateOnly Today,
    DateOnly RecentStartDate,
    string SystemPrompt,
    string UserPrompt,
    string GuidanceBlock);
