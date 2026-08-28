namespace BlogGenerator.Core.Prompts;

public sealed record PromptContext(
    DateOnly Today,
    DateOnly RecentStartDate,
    string ModeInstructions,
    string SystemPrompt,
    string UserPrompt,
    List<string> UserInstructionItems,
    string PrimaryLinkLine,
    string GuidanceBlock);
