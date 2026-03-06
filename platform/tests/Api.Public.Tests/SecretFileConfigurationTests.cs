using Core.Configuration;
using Core.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Api.Public.Tests;

[TestFixture]
[NonParallelizable]
public class SecretFileConfigurationTests
{
    [Test]
    public void ApplyFileBackedSecrets_UsesFileValue_WhenConfigured()
    {
        var envName = "RagCore__InternalSecret_FILE";
        var previous = Environment.GetEnvironmentVariable(envName);
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "secret-from-file\n");
            Environment.SetEnvironmentVariable(envName, tempFile);

            var configuration = new ConfigurationManager();
            configuration["RagCore:InternalSecret"] = "secret-from-config";

            configuration.ApplyFileBackedSecrets("RagCore:InternalSecret");

            configuration["RagCore:InternalSecret"].Should().Be("secret-from-file");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void ApplyFileBackedSecrets_Throws_WhenFileDoesNotExist()
    {
        var envName = "RagCore__InternalSecret_FILE";
        var previous = Environment.GetEnvironmentVariable(envName);
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        try
        {
            Environment.SetEnvironmentVariable(envName, missingPath);

            var configuration = new ConfigurationManager();
            var act = () => configuration.ApplyFileBackedSecrets("RagCore:InternalSecret");

            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
        }
    }

    [Test]
    public void AppDbContextFactory_PrefersDatabaseUrlFile_OverPlainDatabaseUrl()
    {
        var fileEnv = "DATABASE_URL_FILE";
        var plainEnv = "DATABASE_URL";
        var previousFile = Environment.GetEnvironmentVariable(fileEnv);
        var previousPlain = Environment.GetEnvironmentVariable(plainEnv);
        var tempFile = Path.GetTempFileName();
        const string expected =
            "Host=localhost;Port=5433;Database=supportlayer;Username=supportlayer;Password=from-file";

        try
        {
            File.WriteAllText(tempFile, $"{expected}\n");
            Environment.SetEnvironmentVariable(fileEnv, tempFile);
            Environment.SetEnvironmentVariable(
                plainEnv,
                "Host=localhost;Port=5433;Database=supportlayer;Username=supportlayer;Password=from-env");

            var factory = new AppDbContextFactory();
            using var db = factory.CreateDbContext([]);

            db.Database.GetDbConnection().ConnectionString.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(fileEnv, previousFile);
            Environment.SetEnvironmentVariable(plainEnv, previousPlain);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
