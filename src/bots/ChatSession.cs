using MongoDB.Bson;

public enum SessionState
{
    Idle,
    AwaitingRegisterEmail,
    AwaitingRegisterPassword,
    AwaitingLoginEmail,
    AwaitingLoginPassword
}

public enum ChatRole
{
    None,
    Owner,
    Guest
}

public sealed class ChatSession
{
    public SessionState State { get; set; }
    public ChatRole Role { get; set; }
    public ObjectId? UserId { get; set; }
    public string? GuestToken { get; set; }
    public string? PendingEmail { get; set; }
}
