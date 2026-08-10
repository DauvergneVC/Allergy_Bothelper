public enum AuthErrorCode
{
    InvalidInput,
    DuplicateEmail,
    UnknownEmail,
    WrongPassword,
    InvalidToken
}

public class AuthException : Exception
{
    private static readonly IReadOnlyDictionary<AuthErrorCode, string> DefaultMessages =
        new Dictionary<AuthErrorCode, string>
        {
            [AuthErrorCode.DuplicateEmail] = "el email ya está registrado",
            [AuthErrorCode.UnknownEmail] = "email no registrado",
            [AuthErrorCode.WrongPassword] = "contraseña incorrecta",
            [AuthErrorCode.InvalidInput] = "email y contraseña son obligatorios",
            [AuthErrorCode.InvalidToken] = "token inválido o no autorizado",
        };

    public AuthErrorCode Code { get; }

    public AuthException(AuthErrorCode code)
        : base(DefaultMessages[code])
    {
        Code = code;
    }

    public AuthException(AuthErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }
}
