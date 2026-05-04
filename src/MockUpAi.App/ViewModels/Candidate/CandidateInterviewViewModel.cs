using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.App.Services.Media;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateInterviewViewModel : ViewModelBase
{
    private readonly SessionContext _sessionContext;
    private readonly IInterviewWorkflowService _workflowService;
    private readonly IAppNavigator _navigator;
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ITranscriptionService _transcription;
    private readonly ICameraPreviewService _camera;

    [ObservableProperty]
    private string _jobRole = string.Empty;

    [ObservableProperty]
    private string _questionPrompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
    private string _transcriptInput = string.Empty;

    [ObservableProperty]
    private string _feedback = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

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
    private string _cameraStatus = "Camera initializing...";

    [ObservableProperty]
    private Bitmap? _cameraFrame;

    public CandidateInterviewViewModel(
        SessionContext sessionContext,
        IInterviewWorkflowService workflowService,
        IAppNavigator navigator,
        IMicrophoneRecorderService microphone,
        ITranscriptionService transcription,
        ICameraPreviewService camera)
    {
        _sessionContext = sessionContext;
        _workflowService = workflowService;
        _navigator = navigator;
        _microphone = microphone;
        _transcription = transcription;
        _camera = camera;
    }

    public async Task InitializeAsync()
    {
        var candidate = _sessionContext.CurrentUser;
        if (candidate is null)
        {
            await _navigator.NavigateToLoginAsync();
            return;
        }

        _camera.FrameReady -= OnCameraFrameReady;
        _camera.FrameReady += OnCameraFrameReady;

        _workflowService.Reset();
        var start = await _workflowService.StartInterviewAsync(candidate);

        JobRole = candidate.JobRole;
        TotalQuestions = start.Questions.Count;
        QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
        RunningScore = 0;
        Feedback = "Answer clearly and role-specifically for best scoring.";
        StatusMessage = "Interview started.";
        RecordingStatus = "Not recording";

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
            var submission = await _workflowService.SubmitAnswerAsync(TranscriptInput);
            Feedback = submission.Result.Feedback;
            RunningScore = submission.RunningScore;
            TranscriptInput = string.Empty;

            if (submission.IsInterviewCompleted)
            {
                await CompleteInterviewAsync();
                return;
            }

            QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
            UpdateCurrentQuestion(submission.NextQuestion);
            StatusMessage = "Answer recorded. Next question loaded.";
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
            RecordingStatus = "Recording in progress...";
            StatusMessage = "Speak your answer clearly.";
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
            var wav = await _microphone.StopRecordingAsync();
            IsRecording = false;

            if (wav.Length == 0)
            {
                RecordingStatus = "No audio captured.";
                return;
            }

            RecordingStatus = "Transcribing audio...";
            var transcript = await _transcription.TranscribeWavAsync(wav, $"answer_{DateTime.UtcNow:yyyyMMddHHmmss}.wav");
            if (string.IsNullOrWhiteSpace(transcript))
            {
                RecordingStatus = "Transcription unavailable. You can type the answer manually.";
                return;
            }

            TranscriptInput = transcript.Trim();
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
        if (!_workflowService.HasActiveInterview())
        {
            return;
        }

        await CompleteInterviewAsync();
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
            await _microphone.StopRecordingAsync();
            IsRecording = false;
        }

        await StopCameraAsync();
        _workflowService.Reset();
        await _navigator.LogoutAsync();
    }

    private async Task CompleteInterviewAsync()
    {
        if (IsRecording)
        {
            await _microphone.StopRecordingAsync();
            IsRecording = false;
        }

        var session = await _workflowService.FinishInterviewAsync();
        _workflowService.Reset();
        await StopCameraAsync();

        _sessionContext.LastInterview = session;
        StatusMessage = "Interview completed. Preparing result card...";
        await _navigator.NavigateToCandidateResultAsync();
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
    }
}
