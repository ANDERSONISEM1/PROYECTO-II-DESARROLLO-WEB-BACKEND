namespace Api.Models.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string Username { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
}
