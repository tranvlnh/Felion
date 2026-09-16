using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class EvaluationQuestion
{
    private EvaluationQuestion()
    {
    }

    private EvaluationQuestion(
        Guid id,
        Guid formId,
        int order,
        string prompt,
        EvaluationQuestionType type,
        bool isRequired,
        decimal? scoreMin,
        decimal? scoreMax,
        int? textMaxLength,
        DateTimeOffset now)
    {
        Id = id;
        FormId = formId;
        Order = order;
        Prompt = prompt;
        Type = type;
        IsRequired = isRequired;
        ScoreMin = scoreMin;
        ScoreMax = scoreMax;
        TextMaxLength = textMaxLength;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid FormId { get; private set; }

    public int Order { get; private set; }

    public string Prompt { get; private set; } = string.Empty;

    public EvaluationQuestionType Type { get; private set; }

    public bool IsRequired { get; private set; }

    public decimal? ScoreMin { get; private set; }

    public decimal? ScoreMax { get; private set; }

    public int? TextMaxLength { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EvaluationQuestion Create(
        Guid formId,
        int order,
        string prompt,
        EvaluationQuestionType type,
        bool isRequired,
        decimal? scoreMin,
        decimal? scoreMax,
        int? textMaxLength,
        DateTimeOffset? now = null)
    {
        ValidateFormId(formId);
        var normalizedPrompt = NormalizePrompt(prompt);
        ValidateConfiguration(order, type, scoreMin, scoreMax, textMaxLength);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationQuestion(
            Guid.NewGuid(),
            formId,
            order,
            normalizedPrompt,
            type,
            isRequired,
            scoreMin,
            scoreMax,
            textMaxLength,
            timestamp);
    }

    public void Update(
        int order,
        string prompt,
        EvaluationQuestionType type,
        bool isRequired,
        decimal? scoreMin,
        decimal? scoreMax,
        int? textMaxLength,
        DateTimeOffset? now = null)
    {
        var normalizedPrompt = NormalizePrompt(prompt);
        ValidateConfiguration(order, type, scoreMin, scoreMax, textMaxLength);

        Order = order;
        Prompt = normalizedPrompt;
        Type = type;
        IsRequired = isRequired;
        ScoreMin = scoreMin;
        ScoreMax = scoreMax;
        TextMaxLength = textMaxLength;
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static void ValidateFormId(Guid formId)
    {
        if (formId == Guid.Empty)
        {
            throw new DomainException("Evaluation form is required.");
        }
    }

    private static string NormalizePrompt(string prompt)
    {
        var normalized = IdentityNormalizer.RequiredText(prompt, nameof(Prompt));
        if (normalized.Length > 2000)
        {
            throw new DomainException("Evaluation question prompt must be at most 2000 characters.");
        }

        return normalized;
    }

    private static void ValidateConfiguration(
        int order,
        EvaluationQuestionType type,
        decimal? scoreMin,
        decimal? scoreMax,
        int? textMaxLength)
    {
        if (order <= 0)
        {
            throw new DomainException("Evaluation question order must be positive.");
        }

        if (type == EvaluationQuestionType.Score)
        {
            if (scoreMin is null || scoreMax is null)
            {
                throw new DomainException("Score questions require both minimum and maximum values.");
            }

            if (scoreMax < scoreMin)
            {
                throw new DomainException("Score question maximum must be greater than or equal to its minimum.");
            }

            if (textMaxLength is not null)
            {
                throw new DomainException("Text maximum length is only valid for Text questions.");
            }

            return;
        }

        if (type == EvaluationQuestionType.Text)
        {
            if (textMaxLength is null or <= 0)
            {
                throw new DomainException("Text questions require a positive maximum length.");
            }

            if (scoreMin is not null || scoreMax is not null)
            {
                throw new DomainException("Score settings are only valid for Score questions.");
            }

            return;
        }

        throw new DomainException("Unknown evaluation question type.");
    }
}
