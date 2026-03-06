namespace Core.Services;

public static class PromptInjectionDetector
{
    private static readonly string[] Patterns =
    [
        "ignore previous instructions",
        "ignore all instructions",
        "system prompt",
        "you are now",
        "jailbreak",
        "override your",
        "disregard",
        "forget everything",
    ];

    public static bool IsInjection(string input) =>
        Patterns.Any(p => input.Contains(p, StringComparison.OrdinalIgnoreCase));
}
