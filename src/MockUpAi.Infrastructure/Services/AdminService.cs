using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Common;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Infrastructure.Services;

internal sealed class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly IInterviewRepository _interviews;
    private readonly IPasswordPolicyService _passwordPolicy;
    private readonly ISecretVaultService _secretVault;
    private readonly IReportExportService _reportExport;

    public AdminService(
        IUserRepository users,
        IInterviewRepository interviews,
        IPasswordPolicyService passwordPolicy,
        ISecretVaultService secretVault,
        IReportExportService reportExport)
    {
        _users = users;
        _interviews = interviews;
        _passwordPolicy = passwordPolicy;
        _secretVault = secretVault;
        _reportExport = reportExport;
    }

    public async Task<OperationResult> CreateCandidateAsync(CandidateCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CandidateId) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.JobRole))
        {
            return OperationResult.Failure("Candidate id, password and job role are required.");
        }

        if (!_passwordPolicy.IsValid(request.Password, out var passwordError))
        {
            return OperationResult.Failure(passwordError);
        }

        var userId = request.CandidateId.Trim();
        var exists = await _users.CandidateExistsAsync(userId, cancellationToken);
        if (exists)
        {
            return OperationResult.Failure("Candidate id already exists.");
        }

        var candidate = new AppUser
        {
            UserId = userId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            ActorType = UserActorType.Candidate,
            JobRole = request.JobRole.Trim(),
            JobDescription = (request.JobDescription ?? string.Empty).Trim(),
            InterviewCategory = request.Category,
            InterviewDifficulty = request.Difficulty,
            PassingScore = Math.Clamp(request.PassingScore, 1, 100),
            AiProvider = request.AiProvider,
            InterviewerVoiceProfile = string.IsNullOrWhiteSpace(request.InterviewerVoiceProfile) ? "Windows:Natural" : request.InterviewerVoiceProfile.Trim(),
            IsActive = true,
            ExpiresAtUtc = request.ExpiresAtUtc,
            MustChangePassword = request.MustChangePasswordOnFirstLogin,
            PasswordChangedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await _users.AddAsync(candidate, cancellationToken);

        return OperationResult.Success("Candidate created successfully.");
    }

    public async Task<OperationResult> UpdateCandidateAsync(CandidateUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = await _users.GetByUserIdAsync(request.CandidateId.Trim(), cancellationToken);
        if (candidate is null || candidate.ActorType != UserActorType.Candidate)
        {
            return OperationResult.Failure("Candidate not found.");
        }

        if (string.IsNullOrWhiteSpace(request.JobRole))
        {
            return OperationResult.Failure("Job role is required.");
        }

        candidate.JobRole = request.JobRole.Trim();
        candidate.JobDescription = (request.JobDescription ?? string.Empty).Trim();
        candidate.InterviewCategory = request.Category;
        candidate.InterviewDifficulty = request.Difficulty;
        candidate.PassingScore = Math.Clamp(request.PassingScore, 1, 100);
        candidate.AiProvider = request.AiProvider;
        candidate.InterviewerVoiceProfile = string.IsNullOrWhiteSpace(request.InterviewerVoiceProfile) ? "Windows:Natural" : request.InterviewerVoiceProfile.Trim();
        candidate.ExpiresAtUtc = request.ExpiresAtUtc;
        candidate.UpdatedAtUtc = DateTime.UtcNow;

        await _users.UpdateAsync(candidate, cancellationToken);
        return OperationResult.Success("Candidate profile updated.");
    }

    public async Task<OperationResult> SetCandidateLifecycleAsync(CandidateLifecycleUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = await _users.GetByUserIdAsync(request.CandidateId.Trim(), cancellationToken);
        if (candidate is null || candidate.ActorType != UserActorType.Candidate)
        {
            return OperationResult.Failure("Candidate not found.");
        }

        candidate.IsActive = request.IsActive;
        candidate.ExpiresAtUtc = request.ExpiresAtUtc;
        candidate.UpdatedAtUtc = DateTime.UtcNow;

        await _users.UpdateAsync(candidate, cancellationToken);
        return OperationResult.Success("Candidate lifecycle updated.");
    }

    public async Task<OperationResult> ResetCandidatePasswordAsync(CandidatePasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = await _users.GetByUserIdAsync(request.CandidateId.Trim(), cancellationToken);
        if (candidate is null || candidate.ActorType != UserActorType.Candidate)
        {
            return OperationResult.Failure("Candidate not found.");
        }

        if (!_passwordPolicy.IsValid(request.NewPassword, out var error))
        {
            return OperationResult.Failure(error);
        }

        candidate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        candidate.MustChangePassword = request.MustChangeOnNextLogin;
        candidate.PasswordChangedAtUtc = DateTime.UtcNow;
        candidate.UpdatedAtUtc = DateTime.UtcNow;

        await _users.UpdateAsync(candidate, cancellationToken);
        return OperationResult.Success("Candidate password reset successfully.");
    }

    public async Task<OperationResult> DeleteCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        var normalized = candidateId.Trim();
        var candidate = await _users.GetByUserIdAsync(normalized, cancellationToken);
        if (candidate is null || candidate.ActorType != UserActorType.Candidate)
        {
            return OperationResult.Failure("Candidate not found.");
        }

        await _interviews.DeleteByCandidateUserIdAsync(normalized, cancellationToken);
        await _users.DeleteCandidateAsync(normalized, cancellationToken);
        return OperationResult.Success("Candidate deleted successfully.");
    }

    public async Task<OperationResult> RotateAdminPasswordAsync(AdminPasswordRotateRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await _users.GetByUserIdAsync(request.AdminUserId.Trim(), cancellationToken);
        if (admin is null || admin.ActorType != UserActorType.Admin)
        {
            return OperationResult.Failure("Admin account not found.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, admin.PasswordHash))
        {
            return OperationResult.Failure("Current password is incorrect.");
        }

        if (!_passwordPolicy.IsValid(request.NewPassword, out var error))
        {
            return OperationResult.Failure(error);
        }

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        admin.MustChangePassword = false;
        admin.PasswordChangedAtUtc = DateTime.UtcNow;
        admin.UpdatedAtUtc = DateTime.UtcNow;

        await _users.UpdateAsync(admin, cancellationToken);
        return OperationResult.Success("Admin password rotated successfully.");
    }

    public Task<OperationResult> SaveOpenAiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        return _secretVault.SaveOpenAiApiKeyAsync(apiKey, cancellationToken);
    }

    public async Task<SecurityOverview> GetSecurityOverviewAsync(CancellationToken cancellationToken = default)
    {
        var hasKey = await _secretVault.HasOpenAiApiKeyAsync(cancellationToken);
        return new SecurityOverview
        {
            HasOpenAiApiKey = hasKey,
            KeySource = _secretVault.GetKeySource(),
            PasswordPolicySummary = _passwordPolicy.DescribePolicy(),
        };
    }

    public async Task<string> ExportDashboardAsync(ReportExportRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await GetDashboardRowsAsync(request.Query, cancellationToken);
        return await _reportExport.ExportAsync(rows, request, cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateDashboardRow>> GetDashboardRowsAsync(CandidateDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var candidates = await _users.GetCandidatesAsync(cancellationToken);
        var latestByCandidate = await _interviews.GetLatestByCandidateUserIdsAsync(
            candidates.Select(x => x.UserId).ToList(),
            cancellationToken);

        var rows = new List<CandidateDashboardRow>();

        foreach (var candidate in candidates)
        {
            latestByCandidate.TryGetValue(candidate.UserId, out var latest);

            var isExpired = candidate.ExpiresAtUtc.HasValue && candidate.ExpiresAtUtc <= DateTime.UtcNow;

            rows.Add(new CandidateDashboardRow
            {
                CandidateId = candidate.UserId,
                JobRole = candidate.JobRole,
                JobDescription = candidate.JobDescription,
                Category = candidate.InterviewCategory,
                Difficulty = candidate.InterviewDifficulty,
                PassingScore = candidate.PassingScore,
                AiProvider = candidate.AiProvider,
                InterviewerVoiceProfile = candidate.InterviewerVoiceProfile,
                InterviewStatus = latest?.Status.ToString() ?? InterviewStatus.Pending.ToString(),
                LatestInterviewScore = latest?.OverallScore ?? 0,
                IsPassed = latest?.IsPassed ?? false,
                RoleFitScore = latest?.RoleFitScore ?? 0,
                TechnicalScore = AverageScore(latest?.QuestionResults, x => x.TechnicalScore),
                CommunicationScore = AverageScore(latest?.QuestionResults, x => x.CommunicationScore),
                DepthScore = AverageScore(latest?.QuestionResults, x => x.DepthScore),
                RelevanceScore = AverageScore(latest?.QuestionResults, x => x.RelevanceScore),
                QuestionsAnswered = latest?.QuestionResults.Count ?? 0,
                CompletedAtUtc = latest?.CompletedAtUtc,
                IsActive = candidate.IsActive,
                IsExpired = isExpired,
                ExpiresAtUtc = candidate.ExpiresAtUtc,
                MustChangePassword = candidate.MustChangePassword,
                CreatedAtUtc = candidate.CreatedAtUtc,
            });
        }

        IEnumerable<CandidateDashboardRow> filtered = rows;

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var needle = query.SearchText.Trim();
            filtered = filtered.Where(x =>
                x.CandidateId.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                x.JobRole.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.RoleFilter))
        {
            filtered = filtered.Where(x => x.JobRole.Equals(query.RoleFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ActiveOnly.HasValue)
        {
            filtered = filtered.Where(x => x.IsActive == query.ActiveOnly.Value);
        }

        filtered = ApplySort(filtered, query);
        return filtered.ToList();
    }

    private static IEnumerable<CandidateDashboardRow> ApplySort(IEnumerable<CandidateDashboardRow> rows, CandidateDashboardQuery query)
    {
        Func<CandidateDashboardRow, object?> selector = query.SortBy.ToLowerInvariant() switch
        {
            "candidate" => x => x.CandidateId,
            "role" => x => x.JobRole,
            "status" => x => x.InterviewStatus,
            "score" => x => x.LatestInterviewScore,
            "fit" => x => x.RoleFitScore,
            "created" => x => x.CreatedAtUtc,
            _ => x => x.CompletedAtUtc,
        };

        return query.SortDescending
            ? rows.OrderByDescending(selector).ThenBy(x => x.CandidateId)
            : rows.OrderBy(selector).ThenBy(x => x.CandidateId);
    }

    private static double AverageScore(IReadOnlyCollection<InterviewQuestionResult>? results, Func<InterviewQuestionResult, double> selector)
    {
        if (results is null || results.Count == 0)
        {
            return 0;
        }

        var scores = results.Select(selector).Where(x => x > 0).ToList();
        return scores.Count == 0 ? 0 : Math.Round(scores.Average(), 2);
    }
}
