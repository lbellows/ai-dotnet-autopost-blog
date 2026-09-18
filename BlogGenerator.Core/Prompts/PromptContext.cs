using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Core.Prompts;

public sealed record PromptContext(
    DateOnly Today,
    DateOnly RecentStartDate,
    string SystemPrompt,
    string UserPrompt,
    string GuidanceBlock,
    // What the blog already published, so no stage of a run repeats the last one. Empty on a
    // checkout with no _posts/ yet, which every prompt treats as "nothing to avoid".
    IReadOnlyList<PublishedPost> RecentPosts,
    string RecentCoverageRule);
