using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests.Integration;

public class AllergyServiceIntegrationTests : IntegrationTestBase
{
    private AllergyService CreateService() => new(Repository);

    [MongoFact]
    public async Task Add_PersistsToMongo_WithCanonicalKeyAndDisplay()
    {
        var user = new User("allergy-test@example.com", BcryptFixtures.Password123Hash);
        await Repository.CreateUserAsync(user);

        var service = CreateService();
        var stored = await service.AddAsync(user.Id, "Maní", "maní");

        Assert.True(stored);

        // Verify persistence in Mongo (not just in-memory fake)
        var persisted = await Repository.GetUserByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "peanut" }, persisted!.Allergies);
        Assert.Equal(new[] { "maní" }, persisted.AllergyDisplay);
    }

    [MongoFact]
    public async Task Add_MultipleAllergies_PersistsAllToMongo()
    {
        var user = new User("multi-allergy@example.com", BcryptFixtures.Password123Hash);
        await Repository.CreateUserAsync(user);

        var service = CreateService();
        await service.AddAsync(user.Id, "peanut", "maní");
        await service.AddAsync(user.Id, "gluten", "trigo");
        await service.AddAsync(user.Id, "lactose", "leche");

        var persisted = await Repository.GetUserByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "peanut", "gluten", "lactose" }, persisted!.Allergies);
        Assert.Equal(new[] { "maní", "trigo", "leche" }, persisted.AllergyDisplay);
    }

    [MongoFact]
    public async Task Add_DuplicateInMongo_IsIdempotent()
    {
        var user = new User("dup-allergy@example.com", BcryptFixtures.Password123Hash);
        await Repository.CreateUserAsync(user);

        var service = CreateService();
        var first = await service.AddAsync(user.Id, "peanut", "maní");
        var second = await service.AddAsync(user.Id, "cacahuete", "cacahuete"); // synonym

        Assert.True(first);
        Assert.False(second); // idempotent

        var persisted = await Repository.GetUserByIdAsync(user.Id);
        Assert.Single(persisted!.Allergies!);
        Assert.Single(persisted.AllergyDisplay!);
        Assert.Equal("peanut", persisted.Allergies![0]);
        Assert.Equal("maní", persisted.AllergyDisplay![0]);
    }

    [MongoFact]
    public async Task GetAllergies_ReadsFromMongo()
    {
        var user = new User("get-allergy@example.com", BcryptFixtures.Password123Hash);
        await Repository.CreateUserAsync(user);

        var service = CreateService();
        await service.AddAsync(user.Id, "peanut", "maní");
        await service.AddAsync(user.Id, "gluten", "trigo");

        var allergies = await service.GetAllergiesAsync(user.Id);

        Assert.Equal(new[] { "peanut", "gluten" }, allergies);
    }

    [MongoFact]
    public async Task GetAllergies_UnknownUser_ReturnsEmpty()
    {
        var service = CreateService();

        var allergies = await service.GetAllergiesAsync(MongoDB.Bson.ObjectId.GenerateNewId());

        Assert.Empty(allergies);
    }
}
