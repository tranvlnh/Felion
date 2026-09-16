using NetCord;
using NetCord.Rest;

namespace Felion.IntegrationTests;

public sealed class VerificationTransportTests
{
    [Fact]
    public void VerificationMessageContainsLinkButton()
    {
        var message = Felion.Bot.VerificationMessageFactory.Create();

        Assert.Equal(
            "Nhập mã sinh viên để nhận role",
            message.Content);

        var row = Assert.IsType<ActionRowProperties>(Assert.Single(message.Components!));
        var button = Assert.IsType<ButtonProperties>(Assert.Single(row.Components!));

        Assert.Equal(Felion.Bot.VerificationInteractionIds.LinkButton, button.CustomId);
        Assert.Equal("Nhận Role", button.Label);
        Assert.Equal(ButtonStyle.Primary, button.Style);
    }

    [Fact]
    public void ConfiguredGuildOnlyAcceptsTheConfiguredGuild()
    {
        var configuredGuild = new Felion.Bot.ConfiguredDiscordGuild(123456789UL);

        Assert.True(configuredGuild.Matches(123456789UL));
        Assert.False(configuredGuild.Matches(null));
        Assert.False(configuredGuild.Matches(987654321UL));
    }
}
