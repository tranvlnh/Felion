using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using NetCord;
using NetCord.Rest;

namespace Felion.Bot.Components.Teams;

internal static class TeamAdministrationMessageFactory
{
    public static InteractionMessageProperties Panel(
        IReadOnlyList<ProbationTeamDto> teams,
        bool canMapRoles)
    {
        var options = teams
            .Take(25)
            .Select(team => new StringMenuSelectOptionProperties(
                TrimLabel(team.Name),
                team.Id.ToString("D"))
            {
                Description = $"{team.CandidateIds.Count} candidate(s), {team.MentorMemberIds.Count} mentor(s)"
            })
            .ToArray();

        var description = teams.Count == 0
            ? "Chưa có probation team. Bấm **Tạo team** để bắt đầu."
            : string.Join(
                "\n",
                teams.Take(20).Select(team =>
                    $"• **{team.Name}** — {team.CandidateIds.Count} candidate, {team.MentorMemberIds.Count} mentor"
                    + (team.IsActive ? string.Empty : " _(inactive)_")));

        if (teams.Count > 20)
        {
            description += "\n\n_Chỉ hiển thị 20 team đầu tiên; dùng `/team panel` để làm mới hoặc quản lý team._";
        }

        var components = new List<IMessageComponentProperties>();
        if (options.Length > 0)
        {
            components.Add(
                new StringMenuProperties(
                    TeamInteractionIds.TeamSelect,
                    options)
                    .WithPlaceholder("Chọn một team để quản lý"));
        }

        components.Add(
            new ActionRowProperties(
            [
                new ButtonProperties(TeamInteractionIds.CreateTeam, "Tạo team", ButtonStyle.Success),
                new ButtonProperties(TeamInteractionIds.Refresh, "Làm mới", ButtonStyle.Secondary)
            ]));

        if (canMapRoles)
        {
            components.Add(
                new ActionRowProperties(
                [
                    new ButtonProperties(TeamInteractionIds.MapRole, "Cấu hình map role", ButtonStyle.Primary)
                ]));
        }

        return new InteractionMessageProperties()
            .WithContent("Felion probation administration")
            .WithFlags(MessageFlags.Ephemeral)
            .AddEmbeds(
            [
                new EmbedProperties()
                    .WithTitle("Probation teams")
                    .WithDescription(description)
                    .WithColor(new Color(0x5865F2))
                    .WithFooter(new EmbedFooterProperties().WithText("Mọi thao tác đều được kiểm tra quyền và ghi audit."))
            ])
            .WithComponents(components);
    }

    public static InteractionMessageProperties Detail(
        ProbationTeamDto team,
        bool canMapRoles)
    {
        var candidates = team.Candidates.Count == 0
            ? "_Chưa có candidate._"
            : string.Join("\n", team.Candidates.Select(candidate =>
                $"• `{candidate.StudentId}` — {candidate.FullName}"));
        var mentors = team.Mentors.Count == 0
            ? "_Chưa có mentor._"
            : string.Join("\n", team.Mentors.Select(mentor =>
                $"• `{mentor.StudentId}` — {mentor.FullName}"));

        var components = new List<IMessageComponentProperties>
        {
            new ActionRowProperties(
            [
                new ButtonProperties(TeamInteractionIds.Back, "← Danh sách team", ButtonStyle.Secondary),
                new ButtonProperties(TeamInteractionIds.Refresh, "Làm mới", ButtonStyle.Secondary),
                new ButtonProperties($"{TeamInteractionIds.RenameTeam}:{team.Id:D}", "Đổi tên", ButtonStyle.Primary),
                new ButtonProperties(
                    $"{TeamInteractionIds.ToggleTeam}:{team.Id:D}",
                    team.IsActive ? "Tắt team" : "Bật team",
                    team.IsActive ? ButtonStyle.Danger : ButtonStyle.Success)
            ]),
            new ActionRowProperties(
            [
                new ButtonProperties($"{TeamInteractionIds.AssignCandidate}:{team.Id:D}", "+ Candidate", ButtonStyle.Success),
                new ButtonProperties($"{TeamInteractionIds.RemoveCandidate}:{team.Id:D}", "− Candidate", ButtonStyle.Danger),
                new ButtonProperties($"{TeamInteractionIds.AssignMentor}:{team.Id:D}", "+ Mentor", ButtonStyle.Success),
                new ButtonProperties($"{TeamInteractionIds.RemoveMentor}:{team.Id:D}", "− Mentor", ButtonStyle.Danger)
            ])
        };

        if (canMapRoles)
        {
            components.Add(
                new ActionRowProperties(
                [
                    new ButtonProperties(TeamInteractionIds.MapRole, "Cấu hình map role", ButtonStyle.Primary)
                ]));
        }

        return new InteractionMessageProperties()
            .WithContent($"Quản lý team **{team.Name}**")
            .WithFlags(MessageFlags.Ephemeral)
            .AddEmbeds(
            [
                new EmbedProperties()
                    .WithTitle(team.Name)
                    .WithDescription(team.IsActive ? "Status: **Active**" : "Status: **Inactive**")
                    .WithColor(team.IsActive ? new Color(0x57F287) : new Color(0xED4245))
                    .AddFields(
                    [
                        new EmbedFieldProperties().WithName("Candidates").WithValue(candidates),
                        new EmbedFieldProperties().WithName("Mentors").WithValue(mentors)
                    ])
            ])
            .WithComponents(components);
    }

    public static InteractionMessageProperties CandidateSelection(
        Guid teamId,
        IReadOnlyList<ProbationCandidateDto> candidates,
        bool removing)
    {
        var options = candidates
            .Take(25)
            .Select(candidate => new StringMenuSelectOptionProperties(
                TrimLabel($"{candidate.StudentId} — {candidate.FullName}"),
                candidate.Id.ToString("D")))
            .ToArray();
        var customId = removing
            ? $"{TeamInteractionIds.RemoveCandidateSelect}:{teamId:D}"
            : $"{TeamInteractionIds.AssignCandidateSelect}:{teamId:D}";

        return SelectionMessage(
            removing ? "Chọn candidate cần gỡ" : "Chọn candidate cần gán",
            options,
            options.Length == 0
                ? null
                : new StringMenuProperties(customId, options)
                    .WithPlaceholder(removing ? "Chọn candidate để gỡ khỏi team" : "Chọn candidate chưa có team"));
    }

    public static InteractionMessageProperties MentorSelection(
        Guid teamId,
        IReadOnlyList<ProbationMentorOption> mentors,
        bool removing)
    {
        var options = mentors
            .Take(25)
            .Select(mentor => new StringMenuSelectOptionProperties(
                TrimLabel($"{mentor.StudentId} — {mentor.FullName}"),
                mentor.MemberId.ToString("D")))
            .ToArray();
        var customId = removing
            ? $"{TeamInteractionIds.RemoveMentorSelect}:{teamId:D}"
            : $"{TeamInteractionIds.AssignMentorSelect}:{teamId:D}";

        return SelectionMessage(
            removing ? "Chọn mentor cần gỡ" : "Chọn mentor cần gán",
            options,
            options.Length == 0
                ? null
                : new StringMenuProperties(customId, options)
                    .WithPlaceholder(removing ? "Chọn mentor để gỡ" : "Chọn active Member làm mentor"));
    }

    public static InteractionMessageProperties MappingKindSelection()
    {
        var options = Enum.GetValues<DiscordRoleMappingKind>()
            .Select(kind => new StringMenuSelectOptionProperties(
                kind.ToString(),
                kind.ToString()))
            .ToArray();
        return SelectionMessage(
            "Chọn dimension cần map role",
            options,
            new StringMenuProperties(TeamInteractionIds.MapKindSelect, options)
                .WithPlaceholder("Position, Department, Generation, ..."));
    }

    public static InteractionMessageProperties MappingSubjectSelection(
        DiscordRoleMappingKind kind,
        IReadOnlyList<(string Label, string Value)> subjects)
    {
        var options = subjects
            .Take(25)
            .Select(subject => new StringMenuSelectOptionProperties(
                TrimLabel(subject.Label),
                subject.Value))
            .ToArray();
        return SelectionMessage(
            $"Chọn subject cho `{kind}`",
            options,
            options.Length == 0
                ? null
                : new StringMenuProperties($"{TeamInteractionIds.MapSubjectSelect}:{kind}", options)
                    .WithPlaceholder("Chọn subject cần map"));
    }

    public static InteractionMessageProperties RoleSelection(
        DiscordRoleMappingKind kind,
        string subjectKey)
    {
        var menu = new RoleMenuProperties($"{TeamInteractionIds.MapRoleSelect}:{kind}:{subjectKey}")
            .WithPlaceholder("Chọn role Discord hiện có")
            .WithMinValues(1)
            .WithMaxValues(1);

        return SelectionMessage(
            "Chọn role Discord",
            [],
            menu);
    }

    public static InteractionCallbackProperties CreateTeamModal()
    {
        return InteractionCallback.Modal(
            new ModalProperties(TeamInteractionIds.CreateTeamModal, "Tạo probation team")
                .AddComponents(
                [
                    new LabelProperties(
                        "Tên team",
                        new TextInputProperties("team-name", TextInputStyle.Short)
                            .WithPlaceholder("Ví dụ: Team Alpha")
                            .WithRequired(true))
                ]));
    }

    public static InteractionCallbackProperties RenameTeamModal(Guid teamId, string currentName)
    {
        return InteractionCallback.Modal(
            new ModalProperties($"{TeamInteractionIds.RenameTeamModal}:{teamId:D}", "Đổi tên team")
                .AddComponents(
                [
                    new LabelProperties(
                        "Tên team",
                        new TextInputProperties("team-name", TextInputStyle.Short)
                            .WithValue(currentName)
                            .WithRequired(true))
                ]));
    }

    private static InteractionMessageProperties SelectionMessage(
        string title,
        StringMenuSelectOptionProperties[] options,
        IMessageComponentProperties? menu)
    {
        var description = options.Length == 0
            ? "Không có lựa chọn phù hợp. Hãy làm mới panel và thử lại."
            : "Chọn một mục bên dưới. Panel sẽ được cập nhật sau khi thao tác hoàn tất.";
        var components = new List<IMessageComponentProperties>();
        if (menu is not null)
        {
            components.Add(menu);
        }

        components.Add(
            new ActionRowProperties(
            [
                new ButtonProperties(TeamInteractionIds.Back, "Hủy", ButtonStyle.Secondary)
            ]));

        return new InteractionMessageProperties()
            .WithContent(title)
            .WithFlags(MessageFlags.Ephemeral)
            .AddEmbeds(
            [
                new EmbedProperties()
                    .WithTitle(title)
                    .WithDescription(description)
                    .WithColor(new Color(0x5865F2))
            ])
            .WithComponents(components);
    }

    private static string TrimLabel(string value)
    {
        return value.Length <= 100 ? value : value[..97] + "...";
    }
}
