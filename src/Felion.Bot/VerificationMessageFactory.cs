using NetCord;
using NetCord.Rest;

namespace Felion.Bot;

public static class VerificationMessageFactory
{
    public static MessageProperties Create()
    {
        return new MessageProperties()
            .WithContent("Nhập mã sinh viên để nhận role")
            .AddComponents(
            [
                new ActionRowProperties(
                [
                    new ButtonProperties(
                        VerificationInteractionIds.LinkButton,
                        "Nhận Role",
                        ButtonStyle.Primary)
                ])
            ]);
    }
}
