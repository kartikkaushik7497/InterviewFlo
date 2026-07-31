using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using MockUpAi.App.ViewModels;
using MockUpAi.App.ViewModels.Admin;
using MockUpAi.App.ViewModels.Auth;
using MockUpAi.App.ViewModels.Candidate;
using MockUpAi.App.Views.Admin;
using MockUpAi.App.Views.Auth;
using MockUpAi.App.Views.Candidate;

namespace MockUpAi.App;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        Control? view = param switch
        {
            LoginViewModel => new LoginView(),
            PasswordResetViewModel => new PasswordResetView(),
            AdminDashboardViewModel => new AdminDashboardView(),
            CandidatePermissionViewModel => new CandidatePermissionView(),
            CandidateRulesViewModel => new CandidateRulesView(),
            CandidateLobbyViewModel => new CandidateLobbyView(),
            CandidateInterviewViewModel => new CandidateInterviewView(),
            CandidateFeedbackViewModel => new CandidateFeedbackView(),
            CandidateResultViewModel => new CandidateResultView(),
            _ => null,
        };

        if (view is not null)
        {
            return view;
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FEF2F2")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FCA5A5")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#991B1B")),
                Text = "View not found for " + param.GetType().Name,
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
