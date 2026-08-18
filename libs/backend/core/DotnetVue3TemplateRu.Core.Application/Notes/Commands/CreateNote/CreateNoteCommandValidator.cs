using DotnetVue3TemplateRu.Core.Application.Configuration;
using DotnetVue3TemplateRu.Core.Domain.Errors;
using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;

/// <summary>
/// Эталонный валидатор команды. Wolverine-middleware (UseFluentValidation)
/// прогоняет его перед хендлером CreateNoteCommand; при провале бросается
/// FluentValidation.ValidationException, которую GlobalExceptionHandler в API
/// преобразует в ответ 400 со словарём errors (RFC 9457 / ValidationProblemDetails).
///
/// Правило несёт только код ошибки (WithErrorCode) из общего каталога ErrorCodes -
/// тот же код, что и у доменного инварианта; текст сообщения по коду резолвится на
/// границе из ресурсов (см. ADR 0024). Так код и текст не дублируются между слоями.
/// </summary>
public class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator(IOptions<CultureOptions> cultureOptions)
    {
        string defaultCulture = cultureOptions.Value.DefaultCulture;

        // Хотя бы одна локаль обязательна.
        RuleFor(x => x.Texts)
            .NotEmpty().WithErrorCode(ErrorCodes.Note.TextRequired);

        // Значение дефолтной культуры обязательно: оно ложится в запись инлайн и
        // служит фолбэком при чтении на культуре без перевода (ADR 0021). Тот же
        // инвариант держит домен (defense-in-depth, ADR 0024), но там он даёт общий
        // отказ, а клиенту нужна ошибка поля. Гейт по количеству не даёт правилу
        // сработать вторым сообщением, когда набор пуст: об этом уже сказано выше.
        When(x => x.Texts is not null && x.Texts.Count > 0, () =>
            RuleFor(x => x.Texts!)
                .Must(texts => texts.ContainsKey(defaultCulture))
                .WithErrorCode(ErrorCodes.Note.TextRequired));

        // Каждый перевод непустой и в пределах длины. Гейт When не даёт селектору
        // обратиться к Values, когда Texts не передан (null).
        When(x => x.Texts is not null, () =>
            RuleForEach(x => x.Texts!.Values)
                .NotEmpty().WithErrorCode(ErrorCodes.Note.TextRequired)
                .MaximumLength(Note.MaxTextLength).WithErrorCode(ErrorCodes.Note.TextTooLong));
    }
}
