
using MongoDB.Bson;

public interface IShareService
{
    Task<string> GenerateTokenAsync(ObjectId userId);
    Task RevokeTokenAsync(ObjectId userId);
}
