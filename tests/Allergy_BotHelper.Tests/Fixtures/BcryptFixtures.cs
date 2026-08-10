namespace Allergy_BotHelper.Tests.Fixtures;

/// <summary>
/// Precomputed bcrypt hashes (BCrypt.Net-Next 4.2.0, work factor 12, $2a$ prefix)
/// for the known plaintexts used across the auth-service tests. Values were generated
/// once with BCrypt.HashPassword(plaintext, 12) and are verified by the tests via
/// BCrypt.Verify.
/// </summary>
public static class BcryptFixtures
{
    public const string Password123Hash = "$2a$12$xcwjdgfI9MmdhPFQCA0hpOt/YF3ouBvZ/NSeqcSlEwNl1jsPtAywi";
    public const string SpacedPasswordHash = "$2a$12$SROSVx1NOX.xigkwjNo5Pu8HWdvf.AbEiDy0j4j7UnMxSTVZQ7c1q";
    public const string LongPassphraseHash = "$2a$12$l6qjk7LpE8Ek78hPYwJqUuF70ljAje3NTMI.C49.Mez66g4V4TPBm";

    /// <summary>
    /// Valid work-factor-12 hash of random data. Verifies false for every real input;
    /// used by AuthService to keep unknown-email and wrong-password timings comparable.
    /// </summary>
    public const string DummyHash = "$2a$12$yRa.f4SebU8lvFnVjSRXE.1PZ5JlxyQPTAtnfVQbfKGQuAkkGh0RO";
}
