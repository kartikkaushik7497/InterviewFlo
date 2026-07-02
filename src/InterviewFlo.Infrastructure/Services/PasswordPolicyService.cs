using InterviewFlo.Core.Application.Abstractions;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class PasswordPolicyService : IPasswordPolicyService
{
    public bool IsValid(string password, out string validationError)
    {
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            validationError = "Password must be at least 10 characters.";
            return false;
        }

        if (!password.Any(char.IsUpper))
        {
            validationError = "Password must include at least one uppercase letter.";
            return false;
        }

        if (!password.Any(char.IsLower))
        {
            validationError = "Password must include at least one lowercase letter.";
            return false;
        }

        if (!password.Any(char.IsDigit))
        {
            validationError = "Password must include at least one digit.";
            return false;
        }

        var symbols = password.Any(ch => !char.IsLetterOrDigit(ch));
        if (!symbols)
        {
            validationError = "Password must include at least one special character.";
            return false;
        }

        return true;
    }

    public string DescribePolicy()
    {
        return "Minimum 10 characters with uppercase, lowercase, digit, and special character.";
    }
}
