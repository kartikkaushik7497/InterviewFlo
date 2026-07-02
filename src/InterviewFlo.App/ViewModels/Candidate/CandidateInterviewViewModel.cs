using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InterviewFlo.App.Services;
using InterviewFlo.App.Services.Media;
using InterviewFlo.App.Services.Voice;
using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using System.Collections.ObjectModel;
using System.Text;

namespace InterviewFlo.App.ViewModels.Candidate;

public partial class CandidateInterviewViewModel : ViewModelBase
{
    private readonly SessionContext _sessionContext;
    private readonly IInterviewWorkflowService _workflowService;
    private readonly IAppNavigator _navigator;
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ITranscriptionService _transcription;
    private readonly ICameraPreviewService _camera;
    private readonly IInterviewVoiceService _voice;
    private CancellationTokenSource? _liveCaptionCts;
    private DateTime? _recordingStartedAtUtc;

    [ObservableProperty]
    private string _jobRole = string.Empty;

    [ObservableProperty]
    private string _interviewCategory = string.Empty;

    [ObservableProperty]
    private string _questionPrompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
    private string _transcriptInput = string.Empty;

    [ObservableProperty]
    private string _feedback = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Preparing interview...";

    [ObservableProperty]
    private int _questionNumber;

    [ObservableProperty]
    private int _totalQuestions;

    [ObservableProperty]
    private double _runningScore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingAndTranscribeCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingAndTranscribeCommand))]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingStatus = "Not recording";

    [ObservableProperty]
    private string _cameraStatus = "Camera will start after interview setup...";

    [ObservableProperty]
    private Bitmap? _cameraFrame;

    [ObservableProperty]
    private string _liveCaption = string.Empty;

    [ObservableProperty]
    private string _liveTranscriptLog = string.Empty;

    public ObservableCollection<InterviewChatMessage> ChatMessages { get; } = [];

    public CandidateInterviewViewModel(
        SessionContext sessionContext,
        IInterviewWorkflowService workflowService,
        IAppNavigator navigator,
        IMicrophoneRecorderService microphone,
        ITranscriptionService transcription,
        ICameraPreviewService camera,
        IInterviewVoiceService voice)
    {
        _sessionContext = sessionContext;
        _workflowService = workflowService;
        _navigator = navigator;
        _microphone = microphone;
        _transcription = transcription;
        _camera = camera;
        _voice = voice;
    }

    public async Task InitializeAsync()
    {
        var candidate = _sessionContext.CurrentUser;
        if (candidate is null)
        {
            await _navigator.NavigateToLoginAsync();
            return;
        }

        StatusMessage = "Preparing interview questions and voice...";
        Feedback = "Please wait while your interview is prepared.";
        RecordingStatus = "Setup in progress";
        QuestionPrompt = "Preparing your first question...";

        _camera.FrameReady -= OnCameraFrameReady;
        _camera.FrameReady += OnCameraFrameReady;

        _workflowService.Reset();
        var start = await _workflowService.StartInterviewAsync(candidate);

        JobRole = candidate.JobRole;
        InterviewCategory = candidate.InterviewCategory.ToString();
        _voice.SetVoiceProfile(candidate.InterviewerVoiceProfile);
        TotalQuestions = _workflowService.GetTotalQuestionCount();
        QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
        RunningScore = 0;
        Feedback = "Answer clearly and role-specifically for best scoring.";
        StatusMessage = "Interview started.";
        RecordingStatus = "Not recording";
        ChatMessages.Clear();
        LiveCaption = string.Empty;
        LiveTranscriptLog = string.Empty;

        if (!string.IsNullOrWhiteSpace(start.IntroductionMessage))
        {
            AddInterviewerMessage(start.IntroductionMessage);
            StatusMessage = "AI interviewer is introducing the session...";
            _ = _voice.SpeakAsync(start.IntroductionMessage);
        }

        UpdateCurrentQuestion(_workflowService.GetCurrentQuestion());
        await StartCameraAsync();
    }

    private bool CanSubmitAnswer()
    {
        return !IsBusy && !IsRecording && !string.IsNullOrWhiteSpace(TranscriptInput);
    }

    [RelayCommand(CanExecute = nameof(CanSubmitAnswer))]
    private async Task SubmitAnswerAsync()
    {
        IsBusy = true;
        try
        {
            var answerText = TranscriptInput.Trim();
            AddUserMessage(answerText);
            StatusMessage = "Analyzing your answer...";
            Feedback = "Scoring answer quality, role fit, clarity, and keyword coverage.";

            var submission = await _workflowService.SubmitAnswerAsync(answerText);
            Feedback = submission.Result.Feedback;
            RunningScore = submission.RunningScore;
            TranscriptInput = string.Empty;

            if (!string.IsNullOrWhiteSpace(submission.EncouragementMessage))
            {
                AddInterviewerMessage(submission.EncouragementMessage);
                StatusMessage = $"Analysis complete. Current score: {submission.RunningScore:0.##}/100.";
                await PauseForNaturalRhythmAsync(submission.EncouragementMessage, 250, 800);
                _ = _voice.SpeakAsync(submission.EncouragementMessage);
            }

            if (submission.IsInterviewCompleted)
            {
                await CompleteInterviewAsync();
                return;
            }

            await PauseForNaturalRhythmAsync(submission.NextQuestion?.Prompt ?? string.Empty, 500, 1200);
            QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
            UpdateCurrentQuestion(submission.NextQuestion);
            StatusMessage = "Answer analyzed. Continue with the interviewer prompt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not submit answer: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanStartRecording() => !IsBusy && !IsRecording;

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        IsBusy = true;
        try
        {
            var started = await _microphone.StartRecordingAsync();
            if (!started)
            {
                RecordingStatus = string.IsNullOrWhiteSpace(_microphone.LastError)
                    ? "Unable to start recording."
                    : _microphone.LastError;
                return;
            }

            IsRecording = true;
            _recordingStartedAtUtc = DateTime.UtcNow;
            RecordingStatus = "Recording in progress...";
            StatusMessage = "Speak your answer clearly.";
            StartLiveCaptionLoop();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanStopRecordingAndTranscribe() => !IsBusy && IsRecording;

    [RelayCommand(CanExecute = nameof(CanStopRecordingAndTranscribe))]
    private async Task StopRecordingAndTranscribeAsync()
    {
        IsBusy = true;
        try
        {
            StopLiveCaptionLoop();
            var wav = await _microphone.StopRecordingAsync();
            IsRecording = false;
            _recordingStartedAtUtc = null;

            if (wav.Length <= 44)
            {
                RecordingStatus = string.IsNullOrWhiteSpace(_microphone.LastError)
                    ? "No speech audio was captured. Check microphone permission/device and try again."
                    : _microphone.LastError;
                return;
            }

            RecordingStatus = $"Audio captured ({wav.Length / 1024.0:0.0} KB). Transcribing...";
            var transcript = await _transcription.TranscribeWavAsync(wav, $"answer_{DateTime.UtcNow:yyyyMMddHHmmss}.wav");
            if (string.IsNullOrWhiteSpace(transcript))
            {
                RecordingStatus = string.IsNullOrWhiteSpace(_transcription.LastError)
                    ? "Audio was recorded, but transcription failed. Check the OpenAI key/network, or type the answer manually."
                    : $"Audio was recorded, but transcription failed: {_transcription.LastError}";
                StatusMessage = "Recording worked; speech-to-text did not return text.";
                return;
            }

            TranscriptInput = transcript.Trim();
            AppendToTranscriptLog(transcript.Trim());
            RecordingStatus = "Transcription complete.";
            StatusMessage = "Review transcript and submit answer.";
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Recording/transcription failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FinishNowAsync()
    {
        try
        {
            if (!_workflowService.HasActiveInterview())
            {
                await _navigator.NavigateToCandidateFeedbackAsync();
                return;
            }

            StatusMessage = "Finishing interview and calculating final result...";
            await CompleteInterviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not complete interview cleanly: {ex.Message}";
            await _navigator.NavigateToCandidateResultAsync();
        }
    }

    [RelayCommand]
    private async Task RestartCameraAsync()
    {
        await StopCameraAsync();
        await StartCameraAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsRecording)
        {
            StopLiveCaptionLoop();
            await _microphone.StopRecordingAsync();
            IsRecording = false;
        }

        await StopCameraAsync();
        _workflowService.Reset();
        await _navigator.LogoutAsync();
    }

    private async Task CompleteInterviewAsync()
    {
        try
        {
            if (IsRecording)
            {
                StopLiveCaptionLoop();
                await _microphone.StopRecordingAsync();
                IsRecording = false;
                _recordingStartedAtUtc = null;
            }

            using var finishCts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var session = await _workflowService.FinishInterviewAsync(finishCts.Token);
            _sessionContext.LastInterview = session;

            StatusMessage = "Interview completed. Preparing feedback page...";
            await _navigator.NavigateToCandidateFeedbackAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Interview completed. Feedback view failed to load: {ex.Message}";
            try
            {
                await _navigator.NavigateToCandidateResultAsync();
            }
            catch
            {
                await _navigator.NavigateToLoginAsync();
            }
        }
        finally
        {
            _workflowService.Reset();
            await StopCameraAsync();
        }
    }

    private async Task StartCameraAsync()
    {
        var started = await _camera.StartAsync();
        CameraStatus = started ? "Camera live" : $"Camera unavailable: {_camera.LastError}";
    }

    private async Task StopCameraAsync()
    {
        await _camera.StopAsync();
        _camera.FrameReady -= OnCameraFrameReady;

        var current = CameraFrame;
        CameraFrame = null;
        current?.Dispose();
    }

    private void OnCameraFrameReady(Bitmap frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var old = CameraFrame;
            CameraFrame = frame;
            old?.Dispose();
        });
    }

    private void UpdateCurrentQuestion(InterviewQuestion? question)
    {
        QuestionPrompt = question?.Prompt ?? "All questions completed.";
        if (question is not null)
        {
            AddInterviewerMessage(question.Prompt);
            _ = _voice.SpeakAsync(question.Prompt);
        }
    }

    private void AddInterviewerMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ChatMessages.Add(new InterviewChatMessage
        {
            Sender = "Interviewer",
            Text = text.Trim(),
            IsUser = false,
            BubbleBackground = "#FFFFFF",
            BubbleBorder = "#D4DDE6",
            SenderColor = "#0F766E",
            BubbleAlignment = "Left",
        });
    }

    private void AddUserMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ChatMessages.Add(new InterviewChatMessage
        {
            Sender = "You",
            Text = text.Trim(),
            IsUser = true,
            BubbleBackground = "#DCF2E8",
            BubbleBorder = "#B3E0CF",
            SenderColor = "#1D5E4E",
            BubbleAlignment = "Right",
        });
    }

    private void StartLiveCaptionLoop()
    {
        StopLiveCaptionLoop();
        _liveCaptionCts = new CancellationTokenSource();
        var token = _liveCaptionCts.Token;

        _ = Task.Run(async () =>
        {
            string lastPublished = string.Empty;
            var silencePromptShown = false;
            while (!token.IsCancellationRequested && IsRecording)
            {
                try
                {
                    await Task.Delay(2200, token);
                    if (!silencePromptShown &&
                        _recordingStartedAtUtc.HasValue &&
                        DateTime.UtcNow - _recordingStartedAtUtc.Value > TimeSpan.FromSeconds(8) &&
                        string.IsNullOrWhiteSpace(LiveTranscriptLog))
                    {
                        silencePromptShown = true;
                        Dispatcher.UIThread.Post(() =>
                        {
                            RecordingStatus = "Listening... start whenever you are ready, or type your answer manually.";
                        });
                    }

                    var snapshot = await _microphone.GetLiveWavSnapshotAsync(token);
                    if (snapshot.Length < 12000)
                    {
                        continue;
                    }

                    var partial = await _transcription.TranscribeWavAsync(snapshot, $"live_{DateTime.UtcNow:yyyyMMddHHmmss}.wav", token);
                    if (string.IsNullOrWhiteSpace(partial))
                    {
                        continue;
                    }

                    var cleaned = partial.Trim();
                    if (cleaned.Equals(lastPublished, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    lastPublished = cleaned;
                    Dispatcher.UIThread.Post(() =>
                    {
                        LiveCaption = cleaned;
                        AppendToTranscriptLog(cleaned);
                    });
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Keep interview running even if live caption pass fails.
                }
            }
        }, token);
    }

    private void StopLiveCaptionLoop()
    {
        if (_liveCaptionCts is null)
        {
            return;
        }

        _liveCaptionCts.Cancel();
        _liveCaptionCts.Dispose();
        _liveCaptionCts = null;
        _recordingStartedAtUtc = null;
    }

    private void AppendToTranscriptLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (LiveTranscriptLog.Contains(line, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LiveTranscriptLog))
        {
            LiveTranscriptLog = line;
            return;
        }

        var sb = new StringBuilder(LiveTranscriptLog.Length + line.Length + 2);
        sb.Append(LiveTranscriptLog);
        sb.AppendLine();
        sb.Append(line);
        LiveTranscriptLog = sb.ToString();
    }

    private static async Task PauseForNaturalRhythmAsync(string text, int minMs, int maxMs)
    {
        var words = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var dynamicMs = Math.Clamp(words * 60, minMs, maxMs);
        await Task.Delay(dynamicMs);
    }
}
