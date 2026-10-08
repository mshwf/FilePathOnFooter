using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace FilePathOnFooter;

public sealed class FilePathBottomMargin : IWpfTextViewMargin
{
    public const string MarginName = "FilePathBottomMargin";

    private readonly Grid _margin;
    private readonly TextBox _textBox;
    private readonly TextBlock _feedbackTextBlock;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly ITextDocument _textDocument;
    private bool _suppressRightClickMenu;
    private bool _isDisposed;

    public FilePathBottomMargin(ITextDocument textDocument)
    {
        _textDocument = textDocument;

        _textBox = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 2, 4, 2),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        _textBox.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        _textBox.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
        ToolTipService.SetPlacement(_textBox, PlacementMode.Top);
        UpdateFilePath();

        _feedbackTextBlock = new TextBlock
        {
            Text = "Copied!",
            Margin = new Thickness(8, 2, 4, 2),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            // Reserve the space so copying does not move or resize the path.
            Visibility = Visibility.Hidden
        };
        _feedbackTextBlock.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);

        _margin = new Grid();
        _margin.SetResourceReference(Panel.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        _margin.ColumnDefinitions.Add(new ColumnDefinition());
        _margin.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _margin.Children.Add(_textBox);
        Grid.SetColumn(_feedbackTextBlock, 1);
        _margin.Children.Add(_feedbackTextBlock);

        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _feedbackTimer.Tick += OnFeedbackTimerTick;

        _textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        _textBox.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
        _textBox.PreviewMouseRightButtonUp += OnPreviewMouseRightButtonUp;
        _textDocument.FileActionOccurred += OnFileActionOccurred;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            RevealFileInExplorer();
        }
        else if (e.ClickCount == 3)
        {
            _textBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressRightClickMenu = _textBox.SelectionLength == 0;
        if (!_suppressRightClickMenu)
        {
            return;
        }

        e.Handled = true;
        try
        {
            Clipboard.SetText(_textDocument.FilePath);
            ShowFeedback("Copied!");
        }
        catch (ExternalException)
        {
            ShowFeedback("Unable to copy. Try again.");
        }
    }

    private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressRightClickMenu)
        {
            // The default context menu is triggered on mouse-up.
            e.Handled = true;
            _suppressRightClickMenu = false;
        }
    }

    private void RevealFileInExplorer()
    {
        string filePath = _textDocument.FilePath;
        if (!File.Exists(filePath))
        {
            ShowFeedback("File not found. Save it first.");
            return;
        }

        try
        {
            using (Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            }))
            {
            }
        }
        catch (Win32Exception)
        {
            ShowFeedback("Unable to open File Explorer.");
        }
    }

    private void ShowFeedback(string message)
    {
        _feedbackTimer.Stop();
        _feedbackTextBlock.Text = message;
        _feedbackTextBlock.Visibility = Visibility.Visible;
        _feedbackTimer.Start();
    }

    private void OnFeedbackTimerTick(object? sender, EventArgs e)
    {
        _feedbackTimer.Stop();
        _feedbackTextBlock.Visibility = Visibility.Hidden;
        _feedbackTextBlock.Text = "Copied!";
    }

    private void UpdateFilePath()
    {
        _textBox.Text = _textDocument.FilePath;
        _textBox.ToolTip = $"{_textDocument.FilePath}\nCtrl+Click = Reveal in File Explorer\nRight-click = Copy full path (no selection)";
    }

    private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
    {
        if ((e.FileActionType & FileActionTypes.DocumentRenamed) != 0)
        {
            UpdateFilePath();
        }
    }

    public FrameworkElement VisualElement => _margin;

    public double MarginSize => _margin.ActualHeight;

    public bool Enabled => true;

    public ITextViewMargin? GetTextViewMargin(string marginName)
    {
        return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _feedbackTimer.Stop();
            _feedbackTimer.Tick -= OnFeedbackTimerTick;
            _textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _textBox.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
            _textBox.PreviewMouseRightButtonUp -= OnPreviewMouseRightButtonUp;
            _textDocument.FileActionOccurred -= OnFileActionOccurred;
            _isDisposed = true;
        }
    }
}
