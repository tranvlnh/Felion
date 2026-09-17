using System.Globalization;
using Felion.Application.Probation;
using NetCord;
using NetCord.Rest;

namespace Felion.Bot.Components.Evaluation;

internal static class EvaluationMessageFactory
{
    public static InteractionMessageProperties Created(EvaluationPeriodDto period) =>
        Message(
            "Đã tạo một bản đánh giá",
            string.Format(
                CultureInfo.InvariantCulture,
                "**Tên:** {0}\n**Trạng thái:** {1}\n**Bắt đầu:** {2}\n\nMember dùng /evaluation peer; mentor dùng /evaluation mentor.",
                period.Name,
                period.Status,
                Format(period.OpenedAt)),
            0x57F287);

    public static InteractionMessageProperties Current(EvaluationStatusDto status)
    {
        var progress = status.Progress;
        return Message(
            "Bản đánh giá hiện tại - " + status.Period.Name,
            string.Format(
                CultureInfo.InvariantCulture,
                "**Trạng thái:** {0}\n**Bắt đầu:** {1}\n\n**Peer progress:** {2}/{3}\n**Mentor progress:** {4}/{5}",
                status.Period.Status,
                Format(status.Period.OpenedAt),
                progress.PeerSubmitted,
                progress.PeerExpected,
                progress.MentorSubmitted,
                progress.MentorExpected),
            0x5865F2);
    }

    public static InteractionMessageProperties Status(EvaluationStatusDto status)
    {
        var progress = status.Progress;
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "**Trạng thái:** {0}\n**Bắt đầu:** {1}\n\n**Peer:** {2}/{3}\n**Mentor:** {4}/{5}",
            status.Period.Status,
            Format(status.Period.OpenedAt),
            progress.PeerSubmitted,
            progress.PeerExpected,
            progress.MentorSubmitted,
            progress.MentorExpected);

        if (progress.Teams.Count > 0)
        {
            description += "\n\n" + string.Join(
                "\n",
                progress.Teams.Select(team => string.Format(
                    CultureInfo.InvariantCulture,
                    "**Team: {0}** — Peer {1}/{2}, Mentor {3}/{4}",
                    team.TeamName,
                    team.PeerSubmitted,
                    team.PeerExpected,
                    team.MentorSubmitted,
                    team.MentorExpected)));
        }

        return Message("Evaluation — " + status.Period.Name, description, 0x5865F2);
    }

    public static InteractionMessageProperties Targets(EvaluationTargetListDto targetList, bool mentor)
    {
        var components = new List<IMessageComponentProperties>();
        var allTargets = targetList.Teams.SelectMany(team => team.Targets).ToArray();
        var options = allTargets
            .Take(25)
            .Select(target => new StringMenuSelectOptionProperties(
                TrimLabel(target.StudentId + " — " + target.FullName),
                target.CandidateId.ToString("D"))
            {
                Description = target.HasSubmission ? "✅ Đã đánh giá — chọn để sửa" : "⬜ Chưa đánh giá"
            })
            .ToArray();
        if (options.Length > 0)
        {
            var customId = (mentor ? EvaluationInteractionIds.MentorSelect : EvaluationInteractionIds.PeerSelect)
                + ":" + targetList.Period.Id.ToString("D");
            components.Add(
                new StringMenuProperties(customId, options)
                    .WithPlaceholder(mentor ? "Chọn member cần đánh giá" : "Chọn member trong team của bạn"));
        }

        var targets = allTargets;
        var submitted = targets.Count(target => target.HasSubmission);
        var description = string.Join(
            "\n",
            targetList.Teams.Select(team =>
                "**Team: " + team.TeamName + "**\n" +
                string.Join(
                    "\n",
                    team.Targets.Select(target =>
                        (target.HasSubmission ? "✅ " : "⬜ ") + target.FullName))));
        description += string.Format(
            CultureInfo.InvariantCulture,
            "\n\n**Tiến độ:** {0}/{1}",
            submitted,
            targets.Length);
        if (targets.Length > 25)
        {
            description += "\nChỉ 25 member đầu tiên có thể chọn trong một Discord select menu.";
        }

        return Message(
            mentor ? "Mentor Evaluation — " + targetList.Period.Name : "Đánh giá chéo — " + targetList.Period.Name,
            description,
            mentor ? 0xFEE75C : 0x5865F2,
            components);
    }

    public static InteractionCallbackProperties PeerModal(Guid periodId, Guid targetCandidateId, PeerEvaluationSubmissionDto? existing) =>
        Modal(
            EvaluationInteractionIds.PeerModal + ":" + periodId.ToString("D") + ":" + targetCandidateId.ToString("D"),
            "Đánh giá chéo",
            [
                Input("Đóng góp (1-5)", "contribution", TextInputStyle.Short, existing?.Contribution.ToString(CultureInfo.InvariantCulture)),
                Input("Giao tiếp (1-5)", "communication", TextInputStyle.Short, existing?.Communication.ToString(CultureInfo.InvariantCulture)),
                Input("Thái độ (1-5)", "attitude", TextInputStyle.Short, existing?.Attitude.ToString(CultureInfo.InvariantCulture)),
                Input("Nhận xét (không bắt buộc)", "note", TextInputStyle.Paragraph, existing?.Note, required: false)
            ]);

    public static InteractionCallbackProperties MentorModal(Guid periodId, Guid targetCandidateId, MentorEvaluationSubmissionDto? existing) =>
        Modal(
            EvaluationInteractionIds.MentorModal + ":" + periodId.ToString("D") + ":" + targetCandidateId.ToString("D"),
            "Mentor Evaluation",
            [
                Input("Tác phong (1-10)", "attendance", TextInputStyle.Short, existing?.Attendance.ToString(CultureInfo.InvariantCulture)),
                Input("Tiến độ công việc (1-10)", "task-completion", TextInputStyle.Short, existing?.TaskCompletion.ToString(CultureInfo.InvariantCulture)),
                Input("Tinh thần học hỏi (1-10)", "learning-initiative", TextInputStyle.Short, existing?.LearningInitiative.ToString(CultureInfo.InvariantCulture)),
                Input("Nhận xét (không bắt buộc)", "note", TextInputStyle.Paragraph, existing?.Note, required: false)
            ]);

    public static InteractionMessageProperties CloseConfirmation(EvaluationStatusDto status)
    {
        var progress = status.Progress;
        var missing = progress.MissingPeerEvaluations.Count + progress.MissingMentorEvaluations.Count;
        return Message(
            "Đóng evaluation — " + status.Period.Name,
            string.Format(
                CultureInfo.InvariantCulture,
                "**Peer progress:** {0}/{1}\n**Mentor progress:** {2}/{3}\n**Còn thiếu:** {4}\n\nBạn vẫn có thể đóng period dù chưa đủ submission.",
                progress.PeerSubmitted,
                progress.PeerExpected,
                progress.MentorSubmitted,
                progress.MentorExpected,
                missing),
            0xED4245,
            [
                new ActionRowProperties([
                    new ButtonProperties(EvaluationInteractionIds.CloseConfirm + ":" + status.Period.Id.ToString("D"), "Xác nhận đóng", ButtonStyle.Danger),
                    new ButtonProperties(EvaluationInteractionIds.CloseCancel, "Hủy", ButtonStyle.Secondary)
                ])
            ]);
    }

    public static InteractionMessageProperties Closed(EvaluationPeriodDto period) =>
        Message(
            "Evaluation Closed — " + period.Name,
            string.Format(
                CultureInfo.InvariantCulture,
                "**Status:** {0}\n**Closed:** {1}\nDữ liệu đã được khóa, không thể submit hoặc chỉnh sửa.",
                period.Status,
                Format(period.ClosedAt)),
            0xED4245);

    public static InteractionMessageProperties Submitted(string kind, EvaluationSubmissionReceiptDto receipt) =>
        Message(
            kind + " evaluation " + (receipt.Updated ? "updated" : "submitted"),
            receipt.Updated ? "Submission đã được cập nhật." : "Submission đã được lưu.",
            0x57F287);

    public static InteractionMessageProperties Summary(EvaluationSummaryDto summary)
    {
        var description = string.Join(
            "\n\n",
            summary.Candidates.Select(candidate =>
                "**" + candidate.FullName + "** (" + candidate.StudentId + ")\n" +
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Peer: {0}/5 · {1}/5 · {2}/5 ({3})\nMentor: {4}/10 · {5}/10 · {6}/10 ({7})",
                    FormatAverage(candidate.Peer.AverageContribution),
                    FormatAverage(candidate.Peer.AverageCommunication),
                    FormatAverage(candidate.Peer.AverageAttitude),
                    candidate.Peer.EvaluationCount,
                    FormatAverage(candidate.Mentor.AverageAttendance),
                    FormatAverage(candidate.Mentor.AverageTaskCompletion),
                    FormatAverage(candidate.Mentor.AverageLearningInitiative),
                    candidate.Mentor.EvaluationCount)));

        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Không có candidate active trong phạm vi đã chọn.";
        }

        return Message(
            "Evaluation Summary — " + summary.Period.Name,
            description.Length > 4000 ? description[..3997] + "..." : description,
            0x5865F2);
    }

    public static InteractionMessageProperties Detail(EvaluationDetailDto detail)
    {
        var candidate = detail.Candidate;
        var description =
            "**Candidate:** " + candidate.FullName + " (" + candidate.StudentId + ")\n" +
            "**Team:** " + candidate.TeamName + "\n\n" +
            string.Format(
                CultureInfo.InvariantCulture,
                "**Peer average:** {0}/5 · {1}/5 · {2}/5\n**Mentor average:** {3}/10 · {4}/10 · {5}/10",
                FormatAverage(candidate.Peer.AverageContribution),
                FormatAverage(candidate.Peer.AverageCommunication),
                FormatAverage(candidate.Peer.AverageAttitude),
                FormatAverage(candidate.Mentor.AverageAttendance),
                FormatAverage(candidate.Mentor.AverageTaskCompletion),
                FormatAverage(candidate.Mentor.AverageLearningInitiative));

        var fields = new List<EmbedFieldProperties>();
        if (detail.PeerSubmissions.Count > 0)
        {
            fields.Add(new EmbedFieldProperties()
                .WithName("Peer Evaluation")
                .WithValue(string.Join(
                    "\n\n",
                    detail.PeerSubmissions.Select(evaluation =>
                        "**Evaluator:** " + evaluation.EvaluatorName + "\n" +
                        "Đóng góp: " + evaluation.Contribution + " · Giao tiếp: " + evaluation.Communication + " · Thái độ: " + evaluation.Attitude +
                        (string.IsNullOrWhiteSpace(evaluation.Note) ? string.Empty : "\nNhận xét: " + evaluation.Note)))));
        }

        if (detail.MentorSubmissions.Count > 0)
        {
            fields.Add(new EmbedFieldProperties()
                .WithName("Mentor Evaluation")
                .WithValue(string.Join(
                    "\n\n",
                    detail.MentorSubmissions.Select(evaluation =>
                        "**Mentor:** " + evaluation.MentorName + "\n" +
                        "Tác phong: " + evaluation.Attendance + " · Tiến độ: " + evaluation.TaskCompletion + " · Học hỏi: " + evaluation.LearningInitiative +
                        (string.IsNullOrWhiteSpace(evaluation.Note) ? string.Empty : "\nNhận xét: " + evaluation.Note)))));
        }

        var embed = new EmbedProperties()
            .WithTitle("Evaluation Detail — " + detail.Period.Name)
            .WithDescription(description)
            .WithColor(new Color(0x5865F2));
        if (fields.Count > 0)
        {
            embed = embed.AddFields(fields);
        }

        return new InteractionMessageProperties()
            .WithFlags(MessageFlags.Ephemeral)
            .AddEmbeds([embed]);
    }

    private static InteractionMessageProperties Message(
        string title,
        string description,
        int color,
        IReadOnlyList<IMessageComponentProperties>? components = null)
    {
        var message = new InteractionMessageProperties()
            .WithFlags(MessageFlags.Ephemeral)
            .AddEmbeds([new EmbedProperties().WithTitle(title).WithDescription(description).WithColor(new Color(color))]);
        return components is null ? message : message.WithComponents(components);
    }

    private static InteractionCallbackProperties<ModalProperties> Modal(string customId, string title, IReadOnlyList<LabelProperties> inputs) =>
        InteractionCallback.Modal(new ModalProperties(customId, title).AddComponents(inputs));

    private static LabelProperties Input(
        string label,
        string customId,
        TextInputStyle style,
        string? value,
        bool required = true)
    {
        var input = new TextInputProperties(customId, style).WithRequired(required);
        if (value is not null)
        {
            input = input.WithValue(value);
        }

        return new LabelProperties(label, input);
    }

    private static string Format(DateTimeOffset? value) =>
        value is null ? "—" : "<t:" + value.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + ":f>";

    private static string FormatAverage(decimal? value) =>
        value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";

    private static string TrimLabel(string value) =>
        value.Length <= 100 ? value : value[..97] + "...";
}
