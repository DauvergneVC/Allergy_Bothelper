using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;
using MongoDB.Bson;

namespace Allergy_BotHelper.Tests;

public class AllergyServiceTests
{
    private static (AllergyService Service, FakeUserRepository Fake) Create()
    {
        var fake = new FakeUserRepository();
        var service = new AllergyService(fake);
        return (service, fake);
    }

    private static User SeedOwner(FakeUserRepository fake)
    {
        var user = new User("owner@example.com", BcryptFixtures.Password123Hash);
        fake.Seed(user);
        return user;
    }

    [Fact]
    public async Task Add_Synonym_StoresCanonicalKeyAndRawDisplay()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);

        var stored = await service.AddAsync(owner.Id, "Maní", "maní");

        Assert.True(stored);
        var persisted = await fake.GetUserByIdAsync(owner.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "peanut" }, persisted!.Allergies);
        Assert.Equal(new[] { "maní" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_UnmappedItem_IsStoredUnderItsOwnNormalizedForm()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);

        var stored = await service.AddAsync(owner.Id, "xantano", "xantano");

        Assert.True(stored);
        var persisted = await fake.GetUserByIdAsync(owner.Id);
        Assert.Equal(new[] { "xantano" }, persisted!.Allergies);
        Assert.Equal(new[] { "xantano" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_RepeatedExactItem_IsIdempotent_StoredOnce()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);

        Assert.True(await service.AddAsync(owner.Id, "peanut", "peanut"));
        var second = await service.AddAsync(owner.Id, "peanut", "peanut");

        Assert.False(second);
        var persisted = await fake.GetUserByIdAsync(owner.Id);
        Assert.Single(persisted!.Allergies!);
        Assert.Single(persisted.AllergyDisplay!);
        Assert.Equal("peanut", persisted.Allergies![0]);
    }

    [Fact]
    public async Task Add_SynonymDuplicate_StoredOnce()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);
        owner.Allergies = new List<string> { "peanut" };
        owner.AllergyDisplay = new List<string> { "maní" };

        var stored = await service.AddAsync(owner.Id, "cacahuete", "cacahuete");

        Assert.False(stored);
        var persisted = await fake.GetUserByIdAsync(owner.Id);
        Assert.Equal(new[] { "peanut" }, persisted!.Allergies);
        Assert.Equal(new[] { "maní" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_UnknownUser_Throws()
    {
        var (service, fake) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(ObjectId.GenerateNewId(), "peanut", "peanut"));
    }

    [Fact]
    public async Task Add_LegacyDocumentWithoutAllergyDisplay_IsUpgradedWithoutDataLoss()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);
        owner.Allergies = new List<string> { "peanut" };
        owner.AllergyDisplay = null;

        await service.AddAsync(owner.Id, "lácteos", "lácteos");

        var persisted = await fake.GetUserByIdAsync(owner.Id);
        Assert.Equal(new[] { "peanut", "lactose" }, persisted!.Allergies);
        Assert.Equal(new[] { "lácteos" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task GetAllergies_ReturnsCanonicalKeys()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);
        owner.Allergies = new List<string> { "peanut", "gluten" };

        var allergies = await service.GetAllergiesAsync(owner.Id);

        Assert.Equal(new[] { "peanut", "gluten" }, allergies);
    }

    [Fact]
    public async Task GetAllergies_UnknownUser_ReturnsEmpty()
    {
        var (service, _) = Create();

        var allergies = await service.GetAllergiesAsync(ObjectId.GenerateNewId());

        Assert.Empty(allergies);
    }

    [Fact]
    public async Task GetAllergies_UserWithoutAllergies_ReturnsEmpty()
    {
        var (service, fake) = Create();
        var owner = SeedOwner(fake);

        var allergies = await service.GetAllergiesAsync(owner.Id);

        Assert.Empty(allergies);
    }
}
