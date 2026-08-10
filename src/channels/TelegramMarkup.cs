using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Builds inline keyboard markup from a flat button list. One button per row,
/// matching the behaviour previously embedded in <c>TelegramChannel</c>.
/// </summary>
public static class TelegramMarkup
{
    public static InlineKeyboardMarkup? Build(IReadOnlyList<BotButton>? buttons)
    {
        if (buttons is null || buttons.Count == 0)
        {
            return null;
        }

        var rows = buttons.Select(b => new[] { InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackValue) });
        return new InlineKeyboardMarkup(rows);
    }
}
