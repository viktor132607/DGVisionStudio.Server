using System.Text.RegularExpressions;
using DGVisionStudio.Application.DTOs;

namespace DGVisionStudio.Api.Services;

public sealed record ContactRequestSubmissionInput(
    string Name,
    string Email,
    string Phone,
    string? Subject,
    string Message);

public sealed record ContactRequestValidationResult(
    ContactRequestSubmissionInput? Input,
    ControllerServiceResult? Error)
{
    public bool IsValid => Input is not null && Error is null;
}

public sealed class ContactRequestInputValidator
{
    private static readonly Regex PhoneRegex = new(
        @"^\+?[0-9\s().-]{7,20}$",
        RegexOptions.Compiled);

    public ContactRequestValidationResult Validate(
        CreateContactRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Phone))
        {
            return Invalid(new
            {
                message = "Name, email and phone are required."
            });
        }

        var normalizedPhone = dto.Phone.Trim();
        var phoneDigitsCount = normalizedPhone.Count(char.IsDigit);

        if (!PhoneRegex.IsMatch(normalizedPhone) ||
            phoneDigitsCount is < 7 or > 15)
        {
            return Invalid(new
            {
                message = "Invalid phone number."
            });
        }

        return new ContactRequestValidationResult(
            new ContactRequestSubmissionInput(
                dto.Name.Trim(),
                dto.Email.Trim(),
                normalizedPhone,
                NormalizeOptional(dto.Subject),
                string.IsNullOrWhiteSpace(dto.Message)
                    ? "-"
                    : dto.Message.Trim()),
            null);
    }

    private static ContactRequestValidationResult Invalid(object error) =>
        new(
            null,
            ControllerServiceResult.BadRequest(error));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
