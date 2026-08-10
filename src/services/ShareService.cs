using MongoDB.Bson;


public class ShareService : IShareService
{
    private readonly UserRepository _userRepository;

    public ShareService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task ShareAsync(ObjectId userId)
    {
        throw new NotImplementedException();
    }
}
