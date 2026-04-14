using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ProductHub.Infrastructure.Security;

public static class KeyVaultConfiguration
{
    public static IConfigurationBuilder AddKeyVaultIfConfigured(
        this IConfigurationBuilder builder,
        IHostEnvironment environment)
    {
        var configuration = builder.Build();
        var keyVaultUri = configuration["KeyVault:Uri"];

        if (string.IsNullOrWhiteSpace(keyVaultUri))
        {
            Log.Information("Key Vault URI not configured — skipping Key Vault integration.");
            return builder;
        }

        Log.Information("Configuring Azure Key Vault: {KeyVaultUri}", keyVaultUri);

        var credential = ResolveCredential(environment);
        var client = new SecretClient(new Uri(keyVaultUri), credential);

        builder.AddAzureKeyVault(client, new KeyVaultSecretManager());

        Log.Information("Azure Key Vault configured successfully.");

        return builder;
    }

    private static DefaultAzureCredential ResolveCredential(IHostEnvironment environment)
    {
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = environment.IsProduction(),
            ExcludeVisualStudioCredential = environment.IsProduction(),
            ExcludeVisualStudioCodeCredential = environment.IsProduction()
        };

        return new DefaultAzureCredential(options);
    }
}
