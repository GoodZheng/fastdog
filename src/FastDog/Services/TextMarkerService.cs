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

    /// <summary>
    /// 批量创建高亮标记，全部添加完成后只失效渲染层一次。
    /// 逐个失效会让 AvalonEdit 每次都丢弃全部可视行并整档重新着色，
    /// 匹配数多时渲染轮数失控，曾把 UI 线程连续占用超过 5 秒触发 AppHangB1 卡死。
    /// 越界/无效区间自动跳过，返回是否实际添加了标记。
    /// </summary>
    public bool CreateBatch(IEnumerable<(int StartOffset, int Length)> ranges)
    {
        var textLength = _textArea.Document?.TextLength ?? 0;
        var added = false;
        foreach (var (start, length) in ranges)
        {
            if (start < 0 || length <= 0 || start + length > textLength) continue;
            _segments.Add(new TextMarker
            {
                StartOffset = start,
                Length = length,
                BackgroundColor = Colors.Yellow
            });
            added = true;
        }
        if (added)
            _textArea.TextView.InvalidateLayer(KnownLayer.Background);
        return added;
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
