using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class ShareToken
{
    [BsonId]
    public ObjectId OwnerId { get; set; }
    [Required]
    public string Token { get; set; }

    public ShareToken(ObjectId ownerId, string token)
    {
        OwnerId = ownerId;
        Token = token;
    }

}