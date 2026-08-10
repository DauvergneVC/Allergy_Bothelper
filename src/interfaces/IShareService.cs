
using MongoDB.Bson;

public interface IShareService
{
    Task ShareAsync(ObjectId userId);
}
