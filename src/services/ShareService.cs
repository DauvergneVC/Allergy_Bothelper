using MongoDB.Bson;


public class ShareService : IShareService
{
    private readonly IUserRepository _userRepository;

    public ShareService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<string> GenerateTokenAsync(ObjectId userId)
    {
        if (await _userRepository.GetUserByIdAsync(userId) is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        return await _userRepository.GenerateTokenAsync(userId);
    }

    public async Task RevokeTokenAsync(ObjectId userId)
    {
        if (await _userRepository.GetUserByIdAsync(userId) is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        await _userRepository.RevokeTokenAsync(userId);
    }
}
