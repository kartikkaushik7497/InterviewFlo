using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Models;
using MockUpAi.App.Services;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.App.ViewModels.Admin;

public partial class AdminDashboardViewModel : ViewModelBase
{
    private readonly IAdminService _adminService;
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCandidateCommand))]
    private string _candidateIdInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCandidateCommand))]
    private string _candidatePasswordInput = string.Empty;

    [ObservableProperty]
    private string _candidateJobDescription = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCandidateCommand))]
    private string _selectedJobRole = RoleCatalog.Items[0];

    [ObservableProperty]
    private string _selectedCategory = "Technical";

    [ObservableProperty]
    private string _selectedDifficulty = "Fresher";

    [ObservableProperty]
    private string _selectedAiProvider = "OpenAi";

    [ObservableProperty]
    private string _candidatePassingScoreInput = "60";

    [ObservableProperty]
    private string _candidateExpiryDateInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCandidateCommand))]
    private AdminCandidateRowViewModel? _selectedCandidate;

    [ObservableProperty]
    private string _editJobRole = string.Empty;

    [ObservableProperty]
    private string _editJobDescription = string.Empty;

    [ObservableProperty]
    private string _editCategory = "Technical";

    [ObservableProperty]
    private string _editDifficulty = "Fresher";

    [ObservableProperty]
    private string _editAiProvider = "OpenAi";

    [ObservableProperty]
    private string _editPassingScoreInput = "60";

    [ObservableProperty]
    private string _editExpiryDateInput = string.Empty;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private string _resetPasswordInput = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedRoleFilter = "All Roles";

    [ObservableProperty]
    private string _selectedActiveFilter = "All";

    [ObservableProperty]
    private string _selectedSortBy = "completedAt";

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCandidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCandidateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<AdminCandidateRowViewModel> CandidateRows { get; } = [];

    public IReadOnlyList<string> AvailableRoles => RoleCatalog.Items;
    public IReadOnlyList<string> CategoryOptions { get; } = ["Technical", "Behavioral", "Hr", "Management"];
    public IReadOnlyList<string> DifficultyOptions { get; } = ["Fresher", "Experienced", "Professional"];
    public IReadOnlyList<string> AiProviderOptions { get; } = ["OpenAi", "Gemini", "Heuristic"];

    public IReadOnlyList<string> RoleFilters { get; } = ["All Roles", .. RoleCatalog.Items];

    public IReadOnlyList<string> ActiveFilters { get; } = ["All", "Active", "Inactive"];

    public IReadOnlyList<string> SortOptions { get; } = ["completedAt", "score", "fit", "candidate", "role", "status", "created"];

    public AdminDashboardViewModel(IAdminService adminService, IAppNavigator navigator)
    {
        _adminService = adminService;
        _navigator = navigator;
    }

    public async Task LoadAsync()
    {
        await RefreshAsync();
    }

    private bool CanCreateCandidate()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(CandidateIdInput) &&
               !string.IsNullOrWhiteSpace(CandidatePasswordInput) &&
               !string.IsNullOrWhiteSpace(SelectedJobRole);
    }

    [RelayCommand]
    private void GenerateRandomCredentials()
    {
        CandidateIdInput = $"cand-{GenerateToken(8).ToLowerInvariant()}";
        CandidatePasswordInput = GenerateStrongPassword(14);
        StatusMessage = "Random candidate key and password generated.";
    }

    [RelayCommand(CanExecute = nameof(CanCreateCandidate))]
    private async Task CreateCandidateAsync()
    {
        IsBusy = true;
        try
        {
            var expiry = ParseDateInput(CandidateExpiryDateInput);
            var request = new CandidateCreateRequest
            {
                CandidateId = CandidateIdInput.Trim(),
                Password = CandidatePasswordInput,
                JobRole = SelectedJobRole,
                JobDescription = CandidateJobDescription,
                Category = ParseCategory(SelectedCategory),
                Difficulty = ParseDifficulty(SelectedDifficulty),
                AiProvider = ParseProvider(SelectedAiProvider),
                PassingScore = ParsePassingScore(CandidatePassingScoreInput),
                ExpiresAtUtc = expiry,
                MustChangePasswordOnFirstLogin = true,
            };

            var result = await _adminService.CreateCandidateAsync(request);
            StatusMessage = result.Message;
            if (!result.Succeeded)
            {
                return;
            }

            CandidateIdInput = string.Empty;
            CandidatePasswordInput = string.Empty;
            CandidateJobDescription = string.Empty;
            CandidateExpiryDateInput = string.Empty;
            CandidatePassingScoreInput = "60";
            SelectedCategory = "Technical";
            SelectedDifficulty = "Fresher";
            SelectedAiProvider = "OpenAi";

            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUpdateCandidate()
    {
        return !IsBusy && SelectedCandidate is not null;
    }

    [RelayCommand(CanExecute = nameof(CanUpdateCandidate))]
    private async Task UpdateCandidateAsync()
    {
        if (SelectedCandidate is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var update = new CandidateUpdateRequest
            {
                CandidateId = SelectedCandidate.CandidateId,
                JobRole = string.IsNullOrWhiteSpace(EditJobRole) ? SelectedCandidate.JobRole : EditJobRole,
                JobDescription = EditJobDescription,
                Category = ParseCategory(EditCategory),
                Difficulty = ParseDifficulty(EditDifficulty),
                AiProvider = ParseProvider(EditAiProvider),
                PassingScore = ParsePassingScore(EditPassingScoreInput),
                ExpiresAtUtc = ParseDateInput(EditExpiryDateInput),
            };

            var profileResult = await _adminService.UpdateCandidateAsync(update);
            if (!profileResult.Succeeded)
            {
                StatusMessage = profileResult.Message;
                return;
            }

            var lifecycle = new CandidateLifecycleUpdateRequest
            {
                CandidateId = SelectedCandidate.CandidateId,
                IsActive = EditIsActive,
                ExpiresAtUtc = ParseDateInput(EditExpiryDateInput),
            };

            var lifecycleResult = await _adminService.SetCandidateLifecycleAsync(lifecycle);
            StatusMessage = lifecycleResult.Message;
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetCandidatePasswordAsync()
    {
        if (SelectedCandidate is null)
        {
            StatusMessage = "Select a candidate first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ResetPasswordInput))
        {
            StatusMessage = "Enter a new password to reset.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _adminService.ResetCandidatePasswordAsync(new CandidatePasswordResetRequest
            {
                CandidateId = SelectedCandidate.CandidateId,
                NewPassword = ResetPasswordInput,
                MustChangeOnNextLogin = true,
            });

            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                ResetPasswordInput = string.Empty;
                await RefreshAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCandidateAsync()
    {
        if (SelectedCandidate is null)
        {
            StatusMessage = "Select a candidate first.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _adminService.DeleteCandidateAsync(SelectedCandidate.CandidateId);
            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                SelectedCandidate = null;
                await RefreshAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        await ExportAsync("csv");
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        await ExportAsync("pdf");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var query = BuildQuery();
            var rows = await _adminService.GetDashboardRowsAsync(query);
            CandidateRows.Clear();
            foreach (var row in rows)
            {
                CandidateRows.Add(ToViewModel(row));
            }

            if (CandidateRows.Count == 0)
            {
                StatusMessage = "No candidates found for current filter.";
            }
            else if (string.IsNullOrWhiteSpace(StatusMessage))
            {
                StatusMessage = $"Loaded {CandidateRows.Count} candidate records.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _navigator.LogoutAsync();
    }

    partial void OnSelectedCandidateChanged(AdminCandidateRowViewModel? value)
    {
        if (value is null)
        {
            EditJobRole = string.Empty;
            EditJobDescription = string.Empty;
            EditCategory = "Technical";
            EditDifficulty = "Fresher";
            EditAiProvider = "OpenAi";
            EditPassingScoreInput = "60";
            EditExpiryDateInput = string.Empty;
            EditIsActive = true;
            return;
        }

        EditJobRole = value.JobRole;
        EditJobDescription = string.Empty;
        EditCategory = value.Category;
        EditDifficulty = value.Difficulty;
        EditAiProvider = value.AiProvider;
        EditPassingScoreInput = value.PassingScore.ToString("0.##");
        EditExpiryDateInput = value.ExpiresAtDisplay == "-" ? string.Empty : value.ExpiresAtDisplay;
        EditIsActive = value.IsActive;
    }

    private CandidateDashboardQuery BuildQuery()
    {
        bool? activeOnly = SelectedActiveFilter switch
        {
            "Active" => true,
            "Inactive" => false,
            _ => null,
        };

        return new CandidateDashboardQuery
        {
            SearchText = SearchText,
            RoleFilter = SelectedRoleFilter == "All Roles" ? string.Empty : SelectedRoleFilter,
            ActiveOnly = activeOnly,
            SortBy = SelectedSortBy,
            SortDescending = SortDescending,
        };
    }

    private async Task ExportAsync(string format)
    {
        IsBusy = true;
        try
        {
            var outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MockUpAiReports");
            var request = new ReportExportRequest
            {
                Query = BuildQuery(),
                OutputDirectory = outputDirectory,
                Format = format,
            };

            var path = await _adminService.ExportDashboardAsync(request);
            StatusMessage = $"Report exported: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTime? ParseDateInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (DateTime.TryParse(input, out var dt))
        {
            return dt.ToUniversalTime();
        }

        return null;
    }

    private static string GenerateToken(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[length];

        for (var i = 0; i < chars.Length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(alphabet.Length);
            chars[i] = alphabet[index];
        }

        return new string(chars);
    }

    private static string GenerateStrongPassword(int length)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*_-";
        var all = upper + lower + digits + symbols;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)],
        };

        while (chars.Count < length)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string([.. chars]);
    }

    private static AdminCandidateRowViewModel ToViewModel(CandidateDashboardRow row)
    {
        var accountState = row.IsActive ? "Active" : "Inactive";
        if (row.IsExpired)
        {
            accountState = "Expired";
        }

        return new AdminCandidateRowViewModel
        {
            CandidateId = row.CandidateId,
            JobRole = row.JobRole,
            Category = row.Category.ToString(),
            Difficulty = row.Difficulty.ToString(),
            PassingScore = row.PassingScore,
            AiProvider = row.AiProvider.ToString(),
            InterviewStatus = row.InterviewStatus,
            LatestInterviewScore = row.LatestInterviewScore,
            IsPassed = row.IsPassed,
            RoleFitScore = row.RoleFitScore,
            QuestionsAnswered = row.QuestionsAnswered,
            CompletedAtDisplay = row.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Pending",
            IsActive = row.IsActive,
            IsExpired = row.IsExpired,
            ExpiresAtDisplay = row.ExpiresAtUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-",
            MustChangePassword = row.MustChangePassword,
            AccountStateDisplay = accountState,
        };
    }

    private static InterviewCategory ParseCategory(string value)
    {
        return Enum.TryParse<InterviewCategory>(value, true, out var parsed)
            ? parsed
            : InterviewCategory.Technical;
    }

    private static InterviewDifficulty ParseDifficulty(string value)
    {
        return Enum.TryParse<InterviewDifficulty>(value, true, out var parsed)
            ? parsed
            : InterviewDifficulty.Fresher;
    }

    private static InterviewAiProvider ParseProvider(string value)
    {
        return Enum.TryParse<InterviewAiProvider>(value, true, out var parsed)
            ? parsed
            : InterviewAiProvider.OpenAi;
    }

    private static double ParsePassingScore(string value)
    {
        if (double.TryParse(value, out var parsed))
        {
            return Math.Clamp(parsed, 1, 100);
        }

        return 60;
    }
}
