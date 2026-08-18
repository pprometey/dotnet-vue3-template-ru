using DotnetVue3TemplateRu.Core.Application.Configuration;
using DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;
using DotnetVue3TemplateRu.Core.Domain.Errors;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace DotnetVue3TemplateRu.Core.Application.UnitTests.Notes.Commands.CreateNote;

/// <summary>
/// Эталонный unit-тест валидатора: проверяет правила напрямую, без HTTP и БД
/// (не нужны ни WebApplicationFactory, ни Testcontainers). Так тестируется
/// логика FluentValidation-валидаторов команд. Text локализован - команда несёт
/// словарь культура -> текст; проверяется непустота набора и каждого перевода.
/// </summary>
public class CreateNoteCommandValidatorTests
{
    private readonly CreateNoteCommandValidator _validator =
        new(Options.Create(new CultureOptions { DefaultCulture = "ru" }));

    [Test]
    public async Task ValidTexts_Passes()
    {
        ValidationResult result = _validator.Validate(new CreateNoteCommand(
            new Dictionary<string, string> { ["ru"] = "Заметка", ["kk"] = "Ескертпе" }));

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task EmptyTexts_FailsWithRequiredCode()
    {
        ValidationResult result = _validator.Validate(new CreateNoteCommand(new Dictionary<string, string>()));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Single().ErrorCode).IsEqualTo(ErrorCodes.Note.TextRequired);
    }

    [Test]
    public async Task WithoutDefaultCulture_FailsWithRequiredCode()
    {
        // Перевод есть, а значения дефолтной культуры нет: писать инлайн нечего,
        // и читателю на культуре без перевода нечем ответить.
        ValidationResult result = _validator.Validate(new CreateNoteCommand(
            new Dictionary<string, string> { ["en"] = "Note" }));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Single().ErrorCode).IsEqualTo(ErrorCodes.Note.TextRequired);
    }

    [Test]
    public async Task EmptyTextValue_FailsWithRequiredCode()
    {
        ValidationResult result = _validator.Validate(new CreateNoteCommand(
            new Dictionary<string, string> { ["ru"] = "" }));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Single().ErrorCode).IsEqualTo(ErrorCodes.Note.TextRequired);
    }

    [Test]
    public async Task TooLongTextValue_FailsWithTooLongCode()
    {
        ValidationResult result = _validator.Validate(new CreateNoteCommand(
            new Dictionary<string, string> { ["ru"] = new string('a', 1001) }));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Single().ErrorCode).IsEqualTo(ErrorCodes.Note.TextTooLong);
    }
}
