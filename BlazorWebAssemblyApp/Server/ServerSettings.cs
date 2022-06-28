using System.Text;

namespace BlazorWebAssemblyApp.Server;

public class ServerSettings
{

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public bool AssumeHttps { get; set; } = false;
    public string PublisherId { get; set; } = "p";
    public string MicrosoftAccountClientId { get; set; } = "8fbe6088-e463-4efa-b2fe-7fdac83f9040";
    public string MicrosoftAccountClientSecret { get; set; } =
        Encoding.UTF8.GetString(Convert.FromBase64String(
            "M0JnOFF+OTRycn5hQkplZFdiY1B2dVYzRlZMM3JtX1M0MWh0cGFsdA=="));


    public string GitHubClientId { get; set; } = "4e41e755c20d7f025ace";

    public string GitHubClientSecret { get; set; } = Encoding.UTF8.GetString(Convert.FromBase64String(Base64Encode("c7f75d38e50b568cfced0fdf30d8cbb65f68bacd")));

}
