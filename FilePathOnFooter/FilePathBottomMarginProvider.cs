using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace FilePathOnFooter;

[Export(typeof(IWpfTextViewMarginProvider))]
[Name(FilePathBottomMargin.MarginName)]
[MarginContainer(PredefinedMarginNames.Bottom)]
[Order(After = PredefinedMarginNames.HorizontalScrollBar)]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
public sealed class FilePathBottomMarginProvider : IWpfTextViewMarginProvider
{
    [Import]
    internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

    public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
    {
        var textView = wpfTextViewHost.TextView;
        var documentBuffer = textView.TextDataModel.DocumentBuffer;

        // Projection views can display a buffer that is not associated with the file.
        if (TextDocumentFactoryService.TryGetTextDocument(documentBuffer, out ITextDocument textDocument)
            || (documentBuffer != textView.TextBuffer
                && TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out textDocument)))
        {
            return new FilePathBottomMargin(textDocument);
        }

        return null;
    }
}
