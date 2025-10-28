namespace SharedLib.Models;

public class UpdatePlayerNameRequest
{
    public int ProfileId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}

public class UpdateBioRequest
{
    public int ProfileId { get; set; }
    public string NewBio { get; set; } = string.Empty;
}

public class UpdateAvatarRequest
{
    public int ProfileId { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
}