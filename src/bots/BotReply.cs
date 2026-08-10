public sealed record BotReply(string Text, IReadOnlyList<BotButton>? Buttons = null);

public sealed record BotButton(string Text, string CallbackValue);
