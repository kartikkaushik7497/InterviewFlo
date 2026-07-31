using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.App.Services.Media;
using MockUpAi.App.Services.Voice;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;
using System.Collections.ObjectModel;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateInterviewViewModel : ViewModelBase
{
    private readonly SessionContext _sessionContext;
    private readonly IInterviewWorkflowService _workflowService;
    private readonly IAppNavigator _navigator;
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ITranscriptionService _transcription;
    private readonly ICameraPreviewService _camera;
    private readonly IInterviewVoiceService _voice;
    private readonly IRuntimeDiagnosticsService _diagnostics;
    private CancellationTokenSource? _liveCaptionCts;
    private DateTime? _recordingStartedAtUtc;
    private byte[]? _lastRecordedWav;
    private InterviewTimelineItem? _activeTimelineItem;
    private string? _reviewQuestionId;

    [ObservableProperty]
    private string _jobRole = string.Empty;

    [ObservableProperty]
    private string _interviewCategory = string.Empty;

    [ObservableProperty]
    private string _questionPrompt = string.Empty;

    [ObservableProperty]
    private string _questionMeta = "Preparing interview";

    [ObservableProperty]
    private string _questionTopic = "Setup";

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
    private double _interviewProgressPercent;

    [ObservableProperty]
    private string _progressSummary = "0 of 0 answered";

    [ObservableProperty]
    private string _workflowProgressText = "Device Check > Rules > Interview > Submitted";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingAndTranscribeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryTranscriptionCommand))]
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

    [ObservableProperty]
    private double _micInputLevel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryTranscriptionCommand))]
    private bool _hasRecordedAudio;

    [ObservableProperty]
    private string _interviewStage = "Setup";

    [ObservableProperty]
    private string _interviewerStatus = "Preparing interviewer";

    [ObservableProperty]
    private string _primaryActionHint = "Wait while the interview room is prepared.";

    [ObservableProperty]
    private bool _isInterviewerSpeaking;

    [ObservableProperty]
    private bool _isAnalyzingAnswer;

    [ObservableProperty]
    private bool _isSubmitConfirmationVisible;

    [ObservableProperty]
    private string _submitConfirmationMessage = string.Empty;

    [ObservableProperty]
    private bool _isTimelineOpen;

    [ObservableProperty]
    private bool _isReviewingPreviousAnswer;

    public ObservableCollection<InterviewChatMessage> ChatMessages { get; } = [];

    public ObservableCollection<InterviewTimelineItem> TimelineItems { get; } = [];

    public CandidateInterviewViewModel(
        SessionContext sessionContext,
        IInterviewWorkflowService workflowService,
        IAppNavigator navigator,
        IMicrophoneRecorderService microphone,
        ITranscriptionService transcription,
        ICameraPreviewService camera,
        IInterviewVoiceService voice,
        IRuntimeDiagnosticsService diagnostics)
    {
        _sessionContext = sessionContext;
        _workflowService = workflowService;
        _navigator = navigator;
        _microphone = microphone;
        _transcription = transcription;
        _camera = camera;
        _voice = voice;
        _diagnostics = diagnostics;
    }

    public async Task InitializeAsync()
    {
        _voice.Stop();
        var candidate = _sessionContext.CurrentUser;
        if (candidate is null)
        {
            await _navigator.NavigateToLoginAsync();
            return;
        }

        IsBusy = true;
        try
        {
            InterviewStage = "Setup";
            QuestionMeta = "Preparing interview";
            QuestionTopic = "Setup";
            InterviewerStatus = "Preparing interviewer";
            PrimaryActionHint = "Loading the role brief, camera, microphone, and local transcription.";
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
            RefreshProgress();
            Feedback = "Use concrete examples: context, your action, technical tradeoff, and result.";
            StatusMessage = _diagnostics.GetSummary();
            RecordingStatus = "Not recording";
            ChatMessages.Clear();
            TimelineItems.Clear();
            _activeTimelineItem = null;
            IsTimelineOpen = false;
            LiveCaption = "Live transcription is optimized for final accuracy. Record your full answer, then press Stop Recording.";
            LiveTranscriptLog = string.Empty;
            WarmUpTranscription();

            await StartCameraAsync();

            if (!string.IsNullOrWhiteSpace(start.IntroductionMessage))
            {
                AddInterviewerMessage(start.IntroductionMessage);
                await SpeakInterviewerAsync(start.IntroductionMessage, "Welcoming you and explaining the interview flow");
            }

            await ShowQuestionAsync(_workflowService.GetCurrentQuestion());
        }
        finally
        {
            IsAnalyzingAnswer = false;
            IsBusy = false;
        }
    }

    private bool CanSubmitAnswer()
    {
        return !IsBusy && !IsRecording && !string.IsNullOrWhiteSpace(TranscriptInput);
    }

    [RelayCommand(CanExecute = nameof(CanSubmitAnswer))]
    private async Task SubmitAnswerAsync()
    {
        if (IsReviewingPreviousAnswer && !string.IsNullOrWhiteSpace(_reviewQuestionId))
        {
            await ResubmitReviewedAnswerAsync(_reviewQuestionId);
            return;
        }

        IsBusy = true;
        try
        {
            var answerText = TranscriptInput.Trim();
            AddUserMessage(answerText);
            InterviewStage = "Analyzing";
            InterviewerStatus = "Thinking through your answer";
            PrimaryActionHint = "Hold on while the interviewer reviews the answer and prepares the next move.";
            StatusMessage = "Analyzing your answer...";
            Feedback = "Reviewing answer quality, role fit, clarity, and keyword coverage.";
            IsAnalyzingAnswer = true;
            MarkActiveTimelineReviewing();

            var submission = await _workflowService.SubmitAnswerAsync(answerText);
            MarkActiveTimelineAnswered(submission.IsAdaptiveFollowUp, submission.Result.Feedback);
            Feedback = BuildCandidateSafeFeedback(submission.Result.Feedback);
            RunningScore = submission.RunningScore;
            RefreshProgress();
            TranscriptInput = string.Empty;

            if (!string.IsNullOrWhiteSpace(submission.EncouragementMessage))
            {
                AddInterviewerMessage(submission.EncouragementMessage);
                StatusMessage = "Answer saved. Preparing the next prompt.";
                await PauseForNaturalRhythmAsync(submission.EncouragementMessage, 250, 800);
                await SpeakInterviewerAsync(submission.EncouragementMessage, "Giving feedback and deciding the next prompt");
            }

            if (submission.IsInterviewCompleted)
            {
                await CompleteInterviewAsync();
                return;
            }

            if (submission.IsAdaptiveFollowUp)
            {
                Feedback = "The interviewer is asking a targeted follow-up so your answer can be clearer and more complete.";
                StatusMessage = "Adaptive follow-up prepared from your last answer.";
            }

            await PauseForNaturalRhythmAsync(submission.NextQuestion?.Prompt ?? string.Empty, 500, 1200);
            QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
            RefreshProgress();
            await ShowQuestionAsync(submission.NextQuestion);
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
            InterviewStage = "Recording";
            InterviewerStatus = "Listening";
            PrimaryActionHint = "Speak naturally. When your answer is complete, press Stop Recording.";
            LiveCaption = "Recording... speak in full sentences. Final transcript will appear after Stop Recording.";
            LiveTranscriptLog = string.Empty;
            MicInputLevel = 0;
            RecordingStatus = "Recording in progress. Keep the microphone close and reduce background noise.";
            StatusMessage = "Speak your answer clearly, then press Stop Recording.";
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
            InterviewStage = "Transcribing";
            InterviewerStatus = "Processing your audio";
            PrimaryActionHint = "Keep the window open while the transcript is prepared.";
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

            _lastRecordedWav = wav;
            HasRecordedAudio = true;
            await TranscribeAudioAsync(wav, isRetry: false);
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Recording/transcription failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzingAnswer = false;
            IsBusy = false;
        }
    }

    private async Task ResubmitReviewedAnswerAsync(string questionId)
    {
        IsBusy = true;
        try
        {
            var answerText = TranscriptInput.Trim();
            InterviewStage = "Re-scoring";
            InterviewerStatus = "Updating your previous answer";
            PrimaryActionHint = "Saving the revised response without changing the current interview question.";
            StatusMessage = "Updating your previous answer...";
            Feedback = "Rechecking the revised answer against the same question rubric.";
            IsAnalyzingAnswer = true;
            MarkActiveTimelineReviewing();

            var submission = await _workflowService.ResubmitAnswerAsync(questionId, answerText);
            MarkActiveTimelineAnswered(false, submission.Result.Feedback);
            if (_activeTimelineItem is not null)
            {
                _activeTimelineItem.CanReanswer = true;
                _activeTimelineItem.IsReviewTarget = false;
            }

            AddUserMessage($"Updated answer for question {QuestionNumber}: {answerText}");
            Feedback = BuildCandidateSafeFeedback(submission.Result.Feedback);
            RunningScore = submission.RunningScore;
            RefreshProgress();

            IsReviewingPreviousAnswer = false;
            _reviewQuestionId = null;
            TranscriptInput = string.Empty;
            LiveCaption = "Previous answer updated. Continue with the current question when ready.";
            LiveTranscriptLog = string.Empty;
            RecordingStatus = "Answer update saved.";
            StatusMessage = "Previous answer updated. Returning to the current question.";

            var currentQuestion = _workflowService.GetCurrentQuestion();
            QuestionNumber = _workflowService.GetCurrentQuestionIndex() + 1;
            UpdateCurrentQuestion(currentQuestion, updateTimeline: true, addChatMessage: false);
            InterviewStage = currentQuestion is null ? "Complete" : "Answer";
            InterviewerStatus = currentQuestion is null ? "All questions completed" : "Ready for your answer";
            PrimaryActionHint = currentQuestion is null
                ? "Submit the interview to save your final summary."
                : "Continue with the current question, or use Track Progress to revise another answered question.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update previous answer: {ex.Message}";
        }
        finally
        {
            IsAnalyzingAnswer = false;
            IsBusy = false;
        }
    }

    private bool CanRetryTranscription() => !IsBusy && !IsRecording && HasRecordedAudio;

    [RelayCommand(CanExecute = nameof(CanRetryTranscription))]
    private async Task RetryTranscriptionAsync()
    {
        if (_lastRecordedWav is null || _lastRecordedWav.Length <= 44)
        {
            RecordingStatus = "No previous audio is available to retry.";
            return;
        }

        IsBusy = true;
        try
        {
            await TranscribeAudioAsync(_lastRecordedWav, isRetry: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TranscribeAudioAsync(byte[] wav, bool isRetry)
    {
        RecordingStatus = $"{(isRetry ? "Retrying" : "Audio captured")} ({wav.Length / 1024.0:0.0} KB). Transcribing with local Whisper...";
        LiveCaption = "Processing final transcript. Keep this window open while local transcription finishes.";
        InterviewStage = isRetry ? "Retrying transcript" : "Transcribing";
        InterviewerStatus = "Converting speech to text";
        PrimaryActionHint = "Review the transcript before submitting so the saved answer matches what you meant.";

        using var timeoutCts = new CancellationTokenSource(GetTranscriptionTimeout());
        try
        {
            var transcript = await _transcription.TranscribeWavAsync(
                wav,
                $"answer_{DateTime.UtcNow:yyyyMMddHHmmss}.wav",
                timeoutCts.Token);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                RecordingStatus = string.IsNullOrWhiteSpace(_transcription.LastError)
                    ? "Audio was recorded, but speech recognition could not produce a confident transcript. Type or correct the answer manually."
                    : $"Audio was recorded, but transcription failed: {_transcription.LastError}";
                LiveCaption = "No confident transcript produced. You can type the answer manually in the answer box.";
                LiveTranscriptLog = string.Empty;
                InterviewStage = "Manual answer";
                InterviewerStatus = "Waiting for typed answer";
                PrimaryActionHint = "Type your answer manually, then submit it.";
                StatusMessage = "Recording worked; speech-to-text did not return reliable text.";
                return;
            }

            var cleanedTranscript = CleanTranscript(transcript);
            TranscriptInput = cleanedTranscript;
            LiveCaption = cleanedTranscript;
            LiveTranscriptLog = cleanedTranscript;
            RecordingStatus = "Transcription complete. Ready for review.";
            InterviewStage = "Review";
            InterviewerStatus = "Waiting for your reviewed answer";
            PrimaryActionHint = "Correct any transcript mistakes, then press Submit Answer.";
            StatusMessage = "Review and correct the transcript if needed. Scoring starts only after Submit Answer.";
        }
        catch (OperationCanceledException)
        {
            RecordingStatus = "Transcription timed out. You can retry or type your answer manually.";
            LiveCaption = "Local transcription took too long. Retry may work if the model was still warming up.";
            InterviewStage = "Retry available";
            InterviewerStatus = "Transcript timed out";
            PrimaryActionHint = "Press Transcribe Again, or type your answer manually.";
            StatusMessage = "Speech-to-text timed out; recording is saved for retry.";
        }
    }

    [RelayCommand]
    private void ToggleTimeline()
    {
        IsTimelineOpen = !IsTimelineOpen;
    }

    [RelayCommand]
    private void CloseTimeline()
    {
        IsTimelineOpen = false;
    }

    [RelayCommand]
    private void ReviewTimelineQuestion(InterviewTimelineItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (IsBusy || IsRecording)
        {
            StatusMessage = "Finish the current recording or wait for processing before reviewing an earlier question.";
            return;
        }

        if (!item.CanReanswer)
        {
            StatusMessage = "Only answered questions can be opened for re-answering.";
            return;
        }

        var question = _workflowService.GetQuestionById(item.QuestionId);
        var result = _workflowService.GetQuestionResult(item.QuestionId);
        if (question is null || result is null)
        {
            StatusMessage = "That question is not available for review yet.";
            return;
        }

        foreach (var timelineItem in TimelineItems)
        {
            timelineItem.IsReviewTarget = false;
        }

        item.IsReviewTarget = true;
        _activeTimelineItem = item;
        _reviewQuestionId = item.QuestionId;
        IsReviewingPreviousAnswer = true;
        IsTimelineOpen = false;

        QuestionNumber = item.Number;
        UpdateCurrentQuestion(question, updateTimeline: false, addChatMessage: false);
        TranscriptInput = result.CandidateTranscript;
        LiveCaption = result.CandidateTranscript;
        LiveTranscriptLog = result.CandidateTranscript;
        InterviewStage = "Re-answer";
        InterviewerStatus = $"Reviewing question {item.Number}";
        PrimaryActionHint = "Edit the previous answer, then press Submit Answer to replace it.";
        RecordingStatus = "Editing previous answer. You can type changes or record a new answer.";
        StatusMessage = "Previous question loaded. Submit Answer will update this answer and return you to the current question.";
        Feedback = BuildCandidateSafeFeedback(result.Feedback);
    }

    [RelayCommand]
    private async Task FinishNowAsync()
    {
        try
        {
            if (!_workflowService.HasActiveInterview())
            {
                await _navigator.NavigateToCandidateResultAsync();
                return;
            }

            var answered = _workflowService.GetCurrentQuestionIndex();
            var total = Math.Max(1, TotalQuestions);
            if (answered < total)
            {
                SubmitConfirmationMessage =
                    $"You have answered {answered} of {total} questions. Submit the interview now, or continue answering the remaining questions?";
                IsSubmitConfirmationVisible = true;
                return;
            }

            StatusMessage = "Submitting interview and saving your answers...";
            InterviewStage = "Finalizing";
            InterviewerStatus = "Submitting interview";
            PrimaryActionHint = "Please wait while your interview is saved.";
            await CompleteInterviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not complete interview cleanly: {ex.Message}";
            await _navigator.NavigateToCandidateResultAsync();
        }
    }

    [RelayCommand]
    private void CancelSubmitInterview()
    {
        IsSubmitConfirmationVisible = false;
        SubmitConfirmationMessage = string.Empty;
        StatusMessage = "Interview submission cancelled. Continue when you are ready.";
        InterviewerStatus = "Ready for your answer";
        PrimaryActionHint = "Continue the interview, or submit again when you are finished.";
    }

    [RelayCommand]
    private async Task ConfirmSubmitInterviewAsync()
    {
        IsSubmitConfirmationVisible = false;
        SubmitConfirmationMessage = string.Empty;
        StatusMessage = "Submitting interview and saving your answers...";
        InterviewStage = "Finalizing";
        InterviewerStatus = "Submitting interview";
        PrimaryActionHint = "Please wait while your interview is saved.";
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
        _voice.Stop();

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
            _voice.Stop();
            InterviewStage = "Finalizing";
            InterviewerStatus = "Submitting interview";
            PrimaryActionHint = "Saving the interview and preparing your submission summary.";
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

            StatusMessage = "Interview submitted. Preparing summary page...";
            await _navigator.NavigateToCandidateResultAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Interview completed. Summary view failed to load: {ex.Message}";
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
        _camera.FrameReady -= OnCameraFrameReady;
        _camera.FrameReady += OnCameraFrameReady;
        CameraStatus = "Starting camera...";
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

    private void UpdateCurrentQuestion(InterviewQuestion? question, bool updateTimeline = true, bool addChatMessage = true)
    {
        QuestionPrompt = question?.Prompt ?? "All questions completed.";
        QuestionTopic = ResolveQuestionTopic(question);
        QuestionMeta = question is null
            ? "Interview complete"
            : IsFollowUpQuestion(question)
                ? $"Adaptive follow-up {Math.Max(1, QuestionNumber)} of {Math.Max(QuestionNumber, TotalQuestions)}"
                : $"Question {Math.Max(1, QuestionNumber)} of {Math.Max(QuestionNumber, TotalQuestions)}";

        if (question is not null)
        {
            if (updateTimeline)
            {
                SyncTimelineWithCurrentQuestion(question);
            }

            if (addChatMessage)
            {
                AddInterviewerMessage(question.Prompt);
            }
        }
    }

    private static string ResolveQuestionTopic(InterviewQuestion? question)
    {
        if (question is null)
        {
            return "Complete";
        }

        var section = ExtractHintValue(question.IdealAnswerHint, "Section");
        if (!string.IsNullOrWhiteSpace(section))
        {
            return ToQuestionTopicLabel(section);
        }

        return InferQuestionTopic(question.Prompt);
    }

    private static string ExtractHintValue(string? hint, string key)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return string.Empty;
        }

        foreach (var part in hint.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = part[..separatorIndex].Trim();
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separatorIndex + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private static string ToQuestionTopicLabel(string section)
    {
        return section.Trim().ToLowerInvariant() switch
        {
            "api" => "API",
            "hr" => "HR",
            "oop" => "OOP",
            "career" => "Career",
            "project" => "Project",
            "coding" => "Coding",
            "database" => "Database",
            "security" => "Security",
            "testing" => "Testing",
            "frontend" => "Frontend",
            "followup" => "Follow-up",
            "debugging" => "Debugging",
            "reliability" => "Reliability",
            "behavioral" => "Behavioral",
            "management" => "Management",
            "technical" => "Technical",
            _ => "Interview",
        };
    }

    private static string InferQuestionTopic(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "Interview";
        }

        var text = prompt.ToLowerInvariant();
        if (text.StartsWith("follow-up:") || text.Contains("follow-up"))
        {
            return "Follow-up";
        }

        if (text.Contains("project"))
        {
            return "Project";
        }

        if (text.Contains("coding") || text.Contains("array") || text.Contains("string"))
        {
            return "Coding";
        }

        if (text.Contains("oop") || text.Contains("interface") || text.Contains("class"))
        {
            return "OOP";
        }

        if (text.Contains("api") || text.Contains("endpoint"))
        {
            return "API";
        }

        if (text.Contains("database") || text.Contains("query") || text.Contains("index"))
        {
            return "Database";
        }

        if (text.Contains("security") || text.Contains("login") || text.Contains("authorization"))
        {
            return "Security";
        }

        return "Interview";
    }

    private async Task ShowQuestionAsync(InterviewQuestion? question)
    {
        UpdateCurrentQuestion(question);
        if (question is null)
        {
            InterviewStage = "Complete";
            InterviewerStatus = "All questions completed";
            PrimaryActionHint = "Submit the interview to save your final summary.";
            return;
        }

        var isFollowUp = IsFollowUpQuestion(question);
        InterviewStage = isFollowUp ? "Follow-up" : "Listen";
        PrimaryActionHint = isFollowUp
            ? "This follow-up was generated from your previous answer. Add the missing detail clearly."
            : "Listen to the interviewer prompt before recording your answer.";
        await SpeakInterviewerAsync(question.Prompt, $"Asking question {QuestionNumber} of {TotalQuestions}");

        InterviewStage = isFollowUp ? "Follow-up" : "Answer";
        InterviewerStatus = isFollowUp ? "Ready for follow-up answer" : "Ready for your answer";
        PrimaryActionHint = isFollowUp
            ? "Answer the follow-up with the missing example, tradeoff, or technical detail."
            : "Press Start Recording, answer with a concrete example, then review the transcript.";
        StatusMessage = "Your turn. Record or type your answer when ready.";
    }

    private async Task SpeakInterviewerAsync(string text, string status)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        IsInterviewerSpeaking = true;
        InterviewerStatus = status;
        StatusMessage = status;
        try
        {
            await _voice.SpeakAsync(text);
        }
        finally
        {
            IsInterviewerSpeaking = false;
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

    private void WarmUpTranscription()
    {
        if (!OperatingSystem.IsWindows() || _transcription is not WhisperLocalTranscriptionService whisper)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RecordingStatus = "Preparing local Whisper transcription...";
                });

#pragma warning disable CA1416
                await whisper.WarmUpAsync();
#pragma warning restore CA1416

                Dispatcher.UIThread.Post(() =>
                {
                    if (!IsRecording && !IsBusy)
                    {
                        RecordingStatus = "Local Whisper transcription ready.";
                    }
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!IsRecording && !IsBusy)
                    {
                        RecordingStatus = "Local Whisper warm-up skipped; fallback transcription is available.";
                    }
                });
            }
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
            var silencePromptShown = false;
            while (!token.IsCancellationRequested && IsRecording)
            {
                try
                {
                    await Task.Delay(1000, token);
                    if (!silencePromptShown &&
                        _recordingStartedAtUtc.HasValue &&
                        DateTime.UtcNow - _recordingStartedAtUtc.Value > TimeSpan.FromSeconds(8))
                    {
                        silencePromptShown = true;
                        Dispatcher.UIThread.Post(() =>
                        {
                            RecordingStatus = "Still recording... keep speaking naturally, then press Stop Recording.";
                        });
                    }

                    if (!_recordingStartedAtUtc.HasValue)
                    {
                        continue;
                    }

                    var elapsed = DateTime.UtcNow - _recordingStartedAtUtc.Value;
                    var level = _microphone.CurrentInputLevel * 100;
                    Dispatcher.UIThread.Post(() =>
                    {
                        MicInputLevel = level;
                        LiveCaption = $"Recording... {elapsed:mm\\:ss}. Final transcript will appear after Stop Recording.";
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

    private static string CleanTranscript(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return string.Join(' ', line
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private TimeSpan GetTranscriptionTimeout()
    {
        if (_transcription is WhisperLocalTranscriptionService whisper)
        {
#pragma warning disable CA1416
            return TimeSpan.FromSeconds(whisper.TimeoutSeconds);
#pragma warning restore CA1416
        }

        return TimeSpan.FromSeconds(75);
    }

    private void RefreshProgress()
    {
        var total = Math.Max(1, TotalQuestions);
        var answered = Math.Clamp(_workflowService.GetCurrentQuestionIndex(), 0, total);
        ProgressSummary = $"{answered} of {total} answered";
        InterviewProgressPercent = Math.Round(answered * 100.0 / total, 2);
    }

    private void SyncTimelineWithCurrentQuestion(InterviewQuestion question)
    {
        var existing = TimelineItems.FirstOrDefault(item => item.QuestionId == question.Id);
        if (existing is null)
        {
            existing = new InterviewTimelineItem
            {
                QuestionId = question.Id,
                Number = TimelineItems.Count + 1,
                Topic = ResolveQuestionTopic(question),
                Title = BuildTimelineTitle(question),
                IsFollowUp = IsFollowUpQuestion(question),
            };
            TimelineItems.Add(existing);
        }

        if (_activeTimelineItem is not null &&
            _activeTimelineItem.QuestionId != existing.QuestionId &&
            _activeTimelineItem.Status.Equals("Current", StringComparison.OrdinalIgnoreCase))
        {
            ApplyTimelineStatus(_activeTimelineItem, "Pending", "Waiting for answer", "#CBD5E1", "#F8FAFC", "#64748B");
        }

        _activeTimelineItem = existing;
        ApplyTimelineStatus(
            existing,
            existing.IsFollowUp ? "Follow-up" : "Current",
            existing.IsFollowUp ? "Adaptive prompt from previous answer" : "Ready for answer",
            existing.IsFollowUp ? "#F59E0B" : "#2563EB",
            existing.IsFollowUp ? "#FEF3C7" : "#DBEAFE",
            existing.IsFollowUp ? "#92400E" : "#1D4ED8");
    }

    private void MarkActiveTimelineReviewing()
    {
        if (_activeTimelineItem is null)
        {
            return;
        }

        ApplyTimelineStatus(_activeTimelineItem, "Reviewing", "Answer is being analyzed", "#D97706", "#FEF3C7", "#92400E");
    }

    private void MarkActiveTimelineAnswered(bool followUpRequested, string feedback)
    {
        if (_activeTimelineItem is null)
        {
            return;
        }

        var skipped = feedback.Contains("skipped", StringComparison.OrdinalIgnoreCase);
        if (skipped)
        {
            ApplyTimelineStatus(_activeTimelineItem, "Skipped", "Skipped by candidate", "#94A3B8", "#F1F5F9", "#475569");
            _activeTimelineItem.CanReanswer = true;
            return;
        }

        if (followUpRequested)
        {
            ApplyTimelineStatus(_activeTimelineItem, "Needs detail", "Follow-up generated", "#F59E0B", "#FEF3C7", "#92400E");
            _activeTimelineItem.CanReanswer = true;
            return;
        }

        ApplyTimelineStatus(_activeTimelineItem, "Answered", "Answer saved", "#10B981", "#D1FAE5", "#047857");
        _activeTimelineItem.CanReanswer = true;
    }

    private static void ApplyTimelineStatus(
        InterviewTimelineItem item,
        string status,
        string subtitle,
        string accentBrush,
        string badgeBackground,
        string badgeForeground)
    {
        item.Status = status;
        item.Subtitle = subtitle;
        item.AccentBrush = accentBrush;
        item.BadgeBackground = badgeBackground;
        item.BadgeForeground = badgeForeground;
    }

    private static string BuildTimelineTitle(InterviewQuestion question)
    {
        var prompt = question.Prompt.Trim();
        if (prompt.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase))
        {
            prompt = prompt["Follow-up:".Length..].Trim();
        }

        const int maxLength = 78;
        if (prompt.Length <= maxLength)
        {
            return prompt;
        }

        var cut = prompt.LastIndexOf(' ', maxLength);
        return $"{prompt[..(cut > 0 ? cut : maxLength)].TrimEnd('.', ',', ';')}...";
    }

    private static bool IsFollowUpQuestion(InterviewQuestion? question)
    {
        if (question is null)
        {
            return false;
        }

        return ExtractHintValue(question.IdealAnswerHint, "Section").Equals("followup", StringComparison.OrdinalIgnoreCase) ||
               question.Prompt.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCandidateSafeFeedback(string feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
        {
            return "Answer saved. Continue with the next prompt when ready.";
        }

        var parts = feedback
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !part.Contains("score", StringComparison.OrdinalIgnoreCase) &&
                           !part.Contains("/100", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        return parts.Length == 0
            ? "Answer saved. Continue with the next prompt when ready."
            : $"{string.Join(". ", parts)}.";
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
