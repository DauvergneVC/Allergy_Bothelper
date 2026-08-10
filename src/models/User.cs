using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class User : IValidatableObject
{
    [BsonId]
    public ObjectId Id { get; set; }

    // For validation
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }
    public string? PasswordHash { get; set; }

    // To manage if the user has granted access to their allergies to anyone. The string is the token that allow viewers.
    public string? ShareToken { get; set; }

    // List of allergies associated with the user
    public List<string>? Allergies { get; set; }


    public User(string email, string passwordHash)
    {
        Id = ObjectId.GenerateNewId();
        Email = email;
        PasswordHash = passwordHash;
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
        if (string.IsNullOrWhiteSpace(PasswordHash))
        {
            yield return new ValidationResult("PasswordHash is required.", new[] { nameof(PasswordHash) });
        }
    }
}
