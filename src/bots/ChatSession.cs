using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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
    [BsonId]
    public long ChatId { get; set; }
    public SessionState State { get; set; }
    public ChatRole Role { get; set; }
    public ObjectId? UserId { get; set; }
    public string? GuestToken { get; set; }
    public string? PendingEmail { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long Version { get; set; }
}
