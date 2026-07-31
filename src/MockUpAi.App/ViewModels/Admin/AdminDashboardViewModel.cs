using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Avalonia.Threading;
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
    private readonly IStorageStatusService _storageStatus;
    private readonly DispatcherTimer _liveRefreshTimer;
    private bool _isRefreshing;
    private bool _isRestoringSelection;

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
    private string _selectedAiProvider = "Heuristic";
    [ObservableProperty]
    private string _selectedInterviewerVoice = "Windows:Natural";

    [ObservableProperty]
    private string _candidatePassingScoreInput = "60";

    [ObservableProperty]
    private string _candidateExpiryDateInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCandidateCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCandidate))]
    private AdminCandidateRowViewModel? _selectedCandidate;

    public bool HasSelectedCandidate => SelectedCandidate is not null;

    [ObservableProperty]
    private string _editJobRole = string.Empty;

    [ObservableProperty]
    private string _editJobDescription = string.Empty;

    [ObservableProperty]
    private string _editCategory = "Technical";

    [ObservableProperty]
    private string _editDifficulty = "Fresher";

    [ObservableProperty]
    private string _editAiProvider = "Heuristic";
    [ObservableProperty]
    private string _editInterviewerVoice = "Windows:Natural";

    [ObservableProperty]
    private string _editPassingScoreInput = "60";

    [ObservableProperty]
    private string _editExpiryDateInput = string.Empty;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private string _resetPasswordInput = string.Empty;

    [ObservableProperty]
    private string _deleteConfirmationInput = string.Empty;

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

    [ObservableProperty]
    private bool _isDeleteConfirmationVisible;

    [ObservableProperty]
    private string _deleteConfirmationMessage = string.Empty;

    [ObservableProperty]
    private string _storageStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isTemporaryStorage;

    [ObservableProperty]
    private bool _liveUpdatesEnabled = true;

    [ObservableProperty]
    private string _lastUpdatedDisplay = "Not refreshed yet";

    [ObservableProperty]
    private string _totalCandidatesDisplay = "0";

    [ObservableProperty]
    private string _completedInterviewsDisplay = "0";

    [ObservableProperty]
    private string _pendingInterviewsDisplay = "0";

    [ObservableProperty]
    private string _averageScoreDisplay = "0.0";

    [ObservableProperty]
    private string _passRateDisplay = "0%";

    [ObservableProperty]
    private string _activeCandidatesDisplay = "0 active";

    public ObservableCollection<AdminCandidateRowViewModel> CandidateRows { get; } = [];

    public IReadOnlyList<string> AvailableRoles => RoleCatalog.Items;
    public IReadOnlyList<string> CategoryOptions { get; } = ["Technical", "Behavioral", "Hr", "Management"];
    public IReadOnlyList<string> DifficultyOptions { get; } = ["Fresher", "Experienced", "Professional"];
    public IReadOnlyList<string> AiProviderOptions { get; } = ["Heuristic", "OpenAi", "Gemini"];
    public IReadOnlyList<string> InterviewerVoiceOptions { get; } =
        ["Windows:Natural", "Windows:Zira", "Windows:David", "Windows:Mark", "Windows:Heera", "Windows:Ravi", "OpenAI:Nova", "OpenAI:Alloy", "OpenAI:Shimmer", "OpenAI:Echo", "OpenAI:Fable", "OpenAI:Onyx", "ElevenLabs:Patrick", "ElevenLabs:Neal"];

    public IReadOnlyList<string> RoleFilters { get; } = ["All Roles", .. RoleCatalog.Items];

    public IReadOnlyList<string> ActiveFilters { get; } = ["All", "Active", "Inactive"];

    public IReadOnlyList<string> SortOptions { get; } = ["completedAt", "score", "fit", "candidate", "role", "status", "created"];

    public AdminDashboardViewModel(
        IAdminService adminService,
        IAppNavigator navigator,
        IStorageStatusService storageStatus)
    {
        _adminService = adminService;
        _navigator = navigator;
        _storageStatus = storageStatus;
        StorageStatusMessage = storageStatus.Message;
        IsTemporaryStorage = !storageStatus.IsPersistent;
        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10),
        };
        _liveRefreshTimer.Tick += async (_, _) =>
        {
            if (LiveUpdatesEnabled)
            {
                await RefreshAsync(showBusy: false);
            }
        };
    }

    public async Task LoadAsync()
    {
        await RefreshAsync();
        if (!_liveRefreshTimer.IsEnabled)
        {
            _liveRefreshTimer.Start();
        }
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
                InterviewerVoiceProfile = SelectedInterviewerVoice,
                PassingScore = ParsePassingScore(CandidatePassingScoreInput),
                ExpiresAtUtc = expiry,
                MustChangePasswordOnFirstLogin = false,
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
            SelectedJobRole = RoleCatalog.Items[0];
            CandidateExpiryDateInput = string.Empty;
            CandidatePassingScoreInput = "60";
            SelectedCategory = "Technical";
            SelectedDifficulty = "Fresher";
            SelectedAiProvider = "Heuristic";
            SelectedInterviewerVoice = "Windows:Natural";

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
                InterviewerVoiceProfile = EditInterviewerVoice,
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
                MustChangeOnNextLogin = false,
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

        if (!DeleteConfirmationInput.Trim().Equals(SelectedCandidate.CandidateId, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Type {SelectedCandidate.CandidateId} in the confirmation box before deleting.";
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
                DeleteConfirmationInput = string.Empty;
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
        await RefreshAsync(showBusy: true);
    }

    private async Task RefreshAsync(bool showBusy)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        if (showBusy)
        {
            IsBusy = true;
        }

        try
        {
            var query = BuildQuery();
            var rows = await _adminService.GetDashboardRowsAsync(query);
            var selectedCandidateId = SelectedCandidate?.CandidateId;
            var markedCandidateIds = CandidateRows
                .Where(x => x.IsMarkedForDelete)
                .Select(x => x.CandidateId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            CandidateRows.Clear();
            foreach (var row in rows)
            {
                var rowViewModel = ToViewModel(row);
                rowViewModel.IsMarkedForDelete = markedCandidateIds.Contains(rowViewModel.CandidateId);
                CandidateRows.Add(rowViewModel);
            }

            if (!string.IsNullOrWhiteSpace(selectedCandidateId))
            {
                var restoredSelection = CandidateRows.FirstOrDefault(x =>
                    x.CandidateId.Equals(selectedCandidateId, StringComparison.OrdinalIgnoreCase));
                if (restoredSelection is not null)
                {
                    _isRestoringSelection = true;
                    try
                    {
                        SelectedCandidate = restoredSelection;
                    }
                    finally
                    {
                        _isRestoringSelection = false;
                    }
                }
            }

            if (CandidateRows.Count == 0)
            {
                StatusMessage = "No candidates found for current filter.";
            }
            else if (string.IsNullOrWhiteSpace(StatusMessage))
            {
                StatusMessage = $"Loaded {CandidateRows.Count} candidate records.";
            }

            UpdateDashboardSummary();
            LastUpdatedDisplay = $"Last updated {DateTime.Now:HH:mm:ss}";
        }
        finally
        {
            if (showBusy)
            {
                IsBusy = false;
            }

            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCandidateByIdAsync(string? candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            StatusMessage = "Select a candidate to delete.";
            return;
        }

        await DeleteCandidateInternalAsync(candidateId.Trim(), clearSelection: true);
    }

    [RelayCommand]
    private void DeleteMarkedCandidates()
    {
        var markedCandidates = CandidateRows
            .Where(x => x.IsMarkedForDelete)
            .Select(x => x.CandidateId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markedCandidates.Count == 0)
        {
            StatusMessage = "Select one or more candidates with the checkboxes before deleting.";
            return;
        }

        DeleteConfirmationMessage =
            $"Do you really want to permanently delete {markedCandidates.Count} candidate{(markedCandidates.Count == 1 ? string.Empty : "s")}?";
        IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteMarkedCandidatesAsync()
    {
        var markedCandidates = CandidateRows
            .Where(x => x.IsMarkedForDelete)
            .Select(x => x.CandidateId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markedCandidates.Count == 0)
        {
            IsDeleteConfirmationVisible = false;
            StatusMessage = "No candidates are selected for deletion.";
            return;
        }

        IsBusy = true;
        try
        {
            IsDeleteConfirmationVisible = false;
            var deleted = 0;
            var failures = new List<string>();
            foreach (var candidateId in markedCandidates)
            {
                var result = await _adminService.DeleteCandidateAsync(candidateId);
                if (result.Succeeded)
                {
                    deleted++;
                    continue;
                }

                failures.Add($"{candidateId}: {result.Message}");
            }

            if (SelectedCandidate is not null &&
                markedCandidates.Contains(SelectedCandidate.CandidateId, StringComparer.OrdinalIgnoreCase))
            {
                SelectedCandidate = null;
                DeleteConfirmationInput = string.Empty;
            }

            StatusMessage = failures.Count == 0
                ? $"Deleted {deleted} candidate{(deleted == 1 ? string.Empty : "s")}."
                : $"Deleted {deleted}. Failed: {string.Join("; ", failures)}";
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelDeleteMarkedCandidates()
    {
        IsDeleteConfirmationVisible = false;
        DeleteConfirmationMessage = string.Empty;
    }

    private async Task DeleteCandidateInternalAsync(string candidateId, bool clearSelection)
    {
        IsBusy = true;
        try
        {
            var result = await _adminService.DeleteCandidateAsync(candidateId);
            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                if (clearSelection && SelectedCandidate?.CandidateId.Equals(candidateId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    SelectedCandidate = null;
                    DeleteConfirmationInput = string.Empty;
                }

                await RefreshAsync();
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
        _liveRefreshTimer.Stop();
        await _navigator.LogoutAsync();
    }

    partial void OnSelectedCandidateChanged(AdminCandidateRowViewModel? value)
    {
        if (_isRestoringSelection)
        {
            return;
        }

        if (value is null)
        {
            EditJobRole = string.Empty;
            EditJobDescription = string.Empty;
            EditCategory = "Technical";
            EditDifficulty = "Fresher";
            EditAiProvider = "Heuristic";
            EditInterviewerVoice = "Windows:Natural";
            EditPassingScoreInput = "60";
            EditExpiryDateInput = string.Empty;
            EditIsActive = true;
            DeleteConfirmationInput = string.Empty;
            return;
        }

        EditJobRole = value.JobRole;
        EditJobDescription = value.JobDescription;
        EditCategory = value.Category;
        EditDifficulty = value.Difficulty;
        EditAiProvider = value.AiProvider;
        EditInterviewerVoice = value.InterviewerVoiceProfile;
        EditPassingScoreInput = value.PassingScore.ToString("0.##");
        EditExpiryDateInput = value.ExpiresAtDisplay == "-" ? string.Empty : value.ExpiresAtDisplay;
        EditIsActive = value.IsActive;
        DeleteConfirmationInput = string.Empty;
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
            var outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InterviewFloReports");
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
            JobDescription = row.JobDescription,
            Category = row.Category.ToString(),
            Difficulty = row.Difficulty.ToString(),
            PassingScore = row.PassingScore,
            AiProvider = row.AiProvider.ToString(),
            InterviewerVoiceProfile = row.InterviewerVoiceProfile,
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

    private void UpdateDashboardSummary()
    {
        var rows = CandidateRows.ToList();
        var total = rows.Count;
        var completed = rows.Count(x => x.InterviewStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase));
        var pending = rows.Count(x => x.InterviewStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase));
        var active = rows.Count(x => x.IsActive && !x.IsExpired);
        var scoredRows = rows
            .Where(x => x.LatestInterviewScore > 0 || x.QuestionsAnswered > 0)
            .ToList();
        var averageScore = scoredRows.Count == 0 ? 0 : scoredRows.Average(x => x.LatestInterviewScore);
        var passRate = completed == 0
            ? 0
            : rows.Count(x => x.InterviewStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase) && x.IsPassed) * 100.0 / completed;

        TotalCandidatesDisplay = total.ToString();
        CompletedInterviewsDisplay = completed.ToString();
        PendingInterviewsDisplay = pending.ToString();
        ActiveCandidatesDisplay = $"{active} active";
        AverageScoreDisplay = averageScore.ToString("0.0");
        PassRateDisplay = $"{passRate:0}%";
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
            : InterviewAiProvider.Heuristic;
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
