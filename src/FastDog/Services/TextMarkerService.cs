using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace FastDog.Services;

public sealed class TextMarker : TextSegment
{
    public System.Windows.Media.Color BackgroundColor { get; set; }
}

public sealed class TextMarkerService : IBackgroundRenderer, IVisualLineTransformer
{
    private readonly TextArea _textArea;
    private readonly TextSegmentCollection<TextMarker> _segments;

    public TextMarkerService(TextArea textArea)
    {
        _textArea = textArea;
        _segments = new TextSegmentCollection<TextMarker>(textArea.Document);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public TextMarker Create(int startOffset, int length)
    {
        var marker = new TextMarker
        {
            StartOffset = startOffset,
            Length = length,
            BackgroundColor = Colors.Yellow
        };
        _segments.Add(marker);
        _textArea.TextView.InvalidateLayer(KnownLayer.Background);
        return marker;
    }

    public void RemoveAll()
    {
        _segments.Clear();
        _textArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_segments.Count == 0 || textView.Document == null) return;

        foreach (var line in textView.VisualLines)
        {
            var lineStart = line.FirstDocumentLine.Offset;
            var lineEnd = line.LastDocumentLine.Offset + line.LastDocumentLine.Length;

            foreach (var marker in _segments.FindOverlappingSegments(lineStart, lineEnd - lineStart))
            {
                var geoBuilder = new BackgroundGeometryBuilder
                {
                    AlignToWholePixels = true,
                    CornerRadius = 2
                };
                geoBuilder.AddSegment(textView, marker);

                var geo = geoBuilder.CreateGeometry();
                if (geo is not null)
                {
                    var brush = new SolidColorBrush(marker.BackgroundColor) { Opacity = 0.4 };
                    drawingContext.DrawGeometry(brush, null, geo);
                }
            }
        }
    }

    public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
    {
    }
}
