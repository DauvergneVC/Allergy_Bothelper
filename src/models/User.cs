using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class User : IValidatableObject
{
    [BsonId]
    public ObjectId Id { get; set; }

    // For validation
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }
    public string Password { get; set; }

    // To manage if the user has granted access to their allergies to anyone. The string is the token that allow viewers.
    public ShareToken? ShareToken { get; set; }

    // List of allergies associated with the user
    public List<string> Allergies { get; set; }


    public User(string email, string password, List<string> allergies)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(allergies);

        Id = ObjectId.GenerateNewId();
        Email = email;
        Password = password;
        Allergies = allergies;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == ObjectId.Empty)
        {
            yield return new ValidationResult("Id must be generated.", new[] { nameof(Id) });
        }
        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult("Email is required.", new[] { nameof(Email) });
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("Password is required.", new[] { nameof(Password) });
        }
    }


    public void GrantAccess()
    {
        GenerateShareToken();
    }
    public void RevokeAccess()
    {
        ShareToken = null;
    }
    private void GenerateShareToken()
    {
        // Generate a unique token for sharing allergies
        ShareToken = new ShareToken(Guid.NewGuid().ToString());
    }
}