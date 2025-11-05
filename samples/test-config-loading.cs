using System;
using System.IO;
using System.Threading.Tasks;
using Conduit.Configuration;

// Simple test program to verify YAML configuration loading
class Program
{
    static async Task Main(string[] args)
    {
        var loader = new YamlConfigurationLoader();

        try
        {
            Console.WriteLine("Testing Conduit YAML Configuration Loader");
            Console.WriteLine("=========================================");

            // Test minimal configuration
            Console.WriteLine("\n1. Loading minimal configuration...");
            var minimalConfig = await loader.LoadFromFileAsync("conduit-minimal.yaml");
            Console.WriteLine($"   Application: {minimalConfig.ApplicationName}");
            Console.WriteLine($"   Environment: {minimalConfig.Environment}");
            Console.WriteLine($"   Gateway Enabled: {minimalConfig.Gateway.Enabled}");
            Console.WriteLine("   ✓ Minimal configuration loaded successfully");

            // Test full configuration
            Console.WriteLine("\n2. Loading full configuration...");
            var fullConfig = await loader.LoadFromFileAsync("conduit-full.yaml");
            Console.WriteLine($"   Application: {fullConfig.ApplicationName}");
            Console.WriteLine($"   Gateway Port: {fullConfig.Gateway.Port}");
            Console.WriteLine($"   Transport Count: {(fullConfig.Transports.Http.Enabled ? 1 : 0) + (fullConfig.Transports.Tcp.Enabled ? 1 : 0)}");
            Console.WriteLine($"   Custom Settings: {fullConfig.CustomSettings.Count} items");
            Console.WriteLine("   ✓ Full configuration loaded successfully");

            // Test environment-specific configuration
            Console.WriteLine("\n3. Loading environment-specific configuration...");
            var prodConfig = await loader.LoadWithEnvironmentAsync(
                "conduit-base.yaml",
                "Production",
                "conduit-base.production.yaml"
            );
            Console.WriteLine($"   Application: {prodConfig.ApplicationName}");
            Console.WriteLine($"   Environment: {prodConfig.Environment}");
            Console.WriteLine($"   Gateway Max Connections: {prodConfig.Gateway.MaxConcurrentConnections}");
            Console.WriteLine("   ✓ Environment-specific configuration loaded successfully");

            // Test default configuration creation
            Console.WriteLine("\n4. Creating default configuration...");
            var defaultConfig = await loader.CreateDefaultConfigurationAsync(
                "conduit-generated-default.yaml",
                "Test Application"
            );
            Console.WriteLine($"   Generated file: conduit-generated-default.yaml");
            Console.WriteLine($"   Application: {defaultConfig.ApplicationName}");
            Console.WriteLine("   ✓ Default configuration created successfully");

            Console.WriteLine("\n🎉 All configuration tests passed!");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }
}

// To run this test:
// 1. Navigate to the samples directory
// 2. Ensure you have the Conduit.Configuration assembly available
// 3. Compile and run: dotnet run test-config-loading.cs