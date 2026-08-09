using System.ComponentModel.DataAnnotations;

public class ShareToken
{
    [Required]
    public string Token { get; set; }

    public ShareToken(string token)
    {
        Token = token;
    }

}