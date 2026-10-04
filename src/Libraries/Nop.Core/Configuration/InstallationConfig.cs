namespace Nop.Core.Configuration;

/// <summary>
/// Represents installation configuration parameters
/// </summary>
public partial class InstallationConfig : IConfig
{
    /// <summary>
    /// Gets or sets the initial administrator email shown by the installer
    /// </summary>
    public string AdminEmail { get; set; } = "admin@yourStore.com";

    /// <summary>
    /// Gets or sets the initial database provider name
    /// </summary>
    public string DataProvider { get; set; } = "SqlServer";

    /// <summary>
    /// Gets or sets an existing database server supplied by the deployment
    /// </summary>
    public string ServerName { get; set; }

    /// <summary>
    /// Gets or sets the existing database name and credentials
    /// </summary>
    public string DatabaseName { get; set; }
    public string Username { get; set; }

    // Bootstrap credentials come from deployment configuration, not saved defaults.
    [Newtonsoft.Json.JsonIgnore]
    public string Password { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    public bool DatabaseConfigured => !string.IsNullOrWhiteSpace(ServerName);

    /// <summary>
    /// Gets or sets a value indicating whether a store owner can install sample data during installation
    /// </summary>
    public bool DisableSampleData { get; protected set; } = false;

    /// <summary>
    /// Gets or sets a list of plugins ignored during nopCommerce installation
    /// </summary>
    public string DisabledPlugins { get; protected set; } = "Misc.AzureBlob,Misc.CloudflareImages";

    /// <summary>
    /// Gets or sets a value indicating whether to download and setup the regional language pack during installation
    /// </summary>
    public bool InstallRegionalResources { get; protected set; } = true;
}
