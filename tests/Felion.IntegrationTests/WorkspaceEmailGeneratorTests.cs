using Felion.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Felion.IntegrationTests;

public sealed class WorkspaceEmailGeneratorTests
{
    [Fact]
    public void GeneratesGivenNameAndFamilyMiddleInitialsInWorkspaceDomain()
    {
        var configuration = new ConfigurationManager
        {
            ["Authentication:Google:WorkspaceDomain"] = "gdscptit.dev"
        };
        var generator = new WorkspaceEmailGenerator(configuration);

        Assert.Equal("vinhth@gdscptit.dev", generator.Generate("Trần Hữu Vinh"));
    }
}
