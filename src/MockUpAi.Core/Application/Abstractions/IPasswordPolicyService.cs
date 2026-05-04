namespace MockUpAi.Core.Application.Abstractions;

public interface IPasswordPolicyService
{
    bool IsValid(string password, out string validationError);

    string DescribePolicy();
}
