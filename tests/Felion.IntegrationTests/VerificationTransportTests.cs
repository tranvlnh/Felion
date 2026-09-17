using NetCord;
using NetCord.Rest;

namespace Felion.IntegrationTests;

public sealed class VerificationTransportTests
{
    [Fact]
    public void VerificationMessageContainsLinkButton()
    {
        var message = Felion.Bot.Components.Verification.VerificationMessageFactory.Create();

        Assert.Equal(
            "Nhập mã sinh viên để nhận role",
            message.Content);

        var row = Assert.IsType<ActionRowProperties>(Assert.Single(message.Components!));
        var button = Assert.IsType<ButtonProperties>(Assert.Single(row.Components!));

        Assert.Equal(Felion.Bot.Components.Verification.VerificationInteractionIds.LinkButton, button.CustomId);
        Assert.Equal("Nhận Role", button.Label);
        Assert.Equal(ButtonStyle.Primary, button.Style);
    }

    [Fact]
    public void ConfiguredGuildOnlyAcceptsTheConfiguredGuild()
    {
        var configuredGuild = new Felion.Bot.Configuration.ConfiguredDiscordGuild(123456789UL);

        Assert.True(configuredGuild.Matches(123456789UL));
        Assert.False(configuredGuild.Matches(null));
        Assert.False(configuredGuild.Matches(987654321UL));
    }
}
