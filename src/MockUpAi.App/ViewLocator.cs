using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using MockUpAi.App.ViewModels;
using MockUpAi.App.ViewModels.Candidate;
using MockUpAi.App.Views.Candidate;

namespace MockUpAi.App;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        if (param is CandidateFeedbackViewModel)
        {
            return new CandidateFeedbackView();
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name) ?? param.GetType().Assembly.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#7F1D1D")),
            BorderBrush = new SolidColorBrush(Color.Parse("#B91C1C")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Foreground = Brushes.White,
                Text = "View not found: " + name,
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
