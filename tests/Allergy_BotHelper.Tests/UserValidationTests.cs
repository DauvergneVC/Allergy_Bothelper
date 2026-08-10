using System.ComponentModel.DataAnnotations;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class UserValidationTests
{
    [Fact]
    public void User_ExposesPasswordHash_AndNoPasswordProperty()
    {
        Assert.Null(typeof(User).GetProperty("Password"));
        Assert.NotNull(typeof(User).GetProperty("PasswordHash"));
    }

    [Fact]
    public void Validate_ValidUser_HasNoErrors()
    {
        var user = new User("user@example.com", BcryptFixtures.Password123Hash);

        var errors = Validate(user);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingPasswordHash_ReportsPasswordHash()
    {
        var user = new User("user@example.com", null!);

        var errors = Validate(user);

        var error = Assert.Single(errors);
        Assert.Equal("PasswordHash is required.", error.ErrorMessage);
        Assert.Contains(nameof(User.PasswordHash), error.MemberNames);
    }

    [Fact]
    public void Validate_MissingEmail_ReportsEmail()
    {
        var user = new User("   ", BcryptFixtures.Password123Hash);

        var errors = user.Validate(new ValidationContext(user)).ToList();

        Assert.Contains(errors, e => e.ErrorMessage == "Email is required.");
    }

    [Fact]
    public void Validate_EmptyId_ReportsId()
    {
        var user = new User("user@example.com", BcryptFixtures.Password123Hash)
        {
            Id = MongoDB.Bson.ObjectId.Empty
        };

        var errors = Validate(user);

        Assert.Contains(errors, e => e.ErrorMessage == "Id must be generated.");
    }

    private static List<ValidationResult> Validate(User user)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(user, new ValidationContext(user), errors, validateAllProperties: true);
        return errors;
    }
}
