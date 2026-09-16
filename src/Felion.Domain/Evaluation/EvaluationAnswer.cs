using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class EvaluationAnswer
{
    private EvaluationAnswer()
    {
    }

    private EvaluationAnswer(
        Guid id,
        Guid submissionId,
        Guid questionId,
        string questionPromptSnapshot,
        EvaluationQuestionType questionTypeSnapshot,
        decimal? scoreValue,
        string? textValue,
        DateTimeOffset now)
    {
        Id = id;
        SubmissionId = submissionId;
        QuestionId = questionId;
        QuestionPromptSnapshot = questionPromptSnapshot;
        QuestionTypeSnapshot = questionTypeSnapshot;
        ScoreValue = scoreValue;
        TextValue = textValue;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public Guid? QuestionId { get; private set; }

    public string QuestionPromptSnapshot { get; private set; } = string.Empty;

    public EvaluationQuestionType QuestionTypeSnapshot { get; private set; }

    public decimal? ScoreValue { get; private set; }

    public string? TextValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static EvaluationAnswer Create(
        Guid submissionId,
        EvaluationQuestion question,
        decimal? scoreValue,
        string? textValue,
        DateTimeOffset? now = null)
    {
        if (submissionId == Guid.Empty)
        {
            throw new DomainException("Evaluation submission is required.");
        }

        var normalizedText = textValue?.Trim();
        ValidateValue(question, scoreValue, normalizedText);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationAnswer(
            Guid.NewGuid(),
            submissionId,
            question.Id,
            question.Prompt,
            question.Type,
            scoreValue,
            normalizedText,
            timestamp);
    }

    private static void ValidateValue(
        EvaluationQuestion question,
        decimal? scoreValue,
        string? textValue)
    {
        if (question.Type == EvaluationQuestionType.Score)
        {
            if (textValue is not null)
            {
                throw new DomainException("Score answers cannot contain text.");
            }

            if (scoreValue is not null
                && (scoreValue < question.ScoreMin || scoreValue > question.ScoreMax))
            {
                throw new DomainException("Score answer is outside the configured range.");
            }

            return;
        }

        if (question.Type == EvaluationQuestionType.Text)
        {
            if (scoreValue is not null)
            {
                throw new DomainException("Text answers cannot contain a score.");
            }

            if (textValue is not null && textValue.Length > question.TextMaxLength)
            {
                throw new DomainException("Text answer exceeds the configured maximum length.");
            }

            return;
        }

        throw new DomainException("Unknown evaluation question type.");
    }
}
