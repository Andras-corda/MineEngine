using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MineEngine.Assets.Graphs;

namespace MineEngine.Editor.Views.Graphs;

/// <summary>Resizing and title editing of the comments, shared by the graph editor and Puppeteer.</summary>
internal static class CommentFrames
{
    /// <summary>
    /// Frame of a comment whose edges ("l", "r", "t", "b" or a corner like "tr") move by
    /// <paramref name="delta"/>: the right and bottom edges change the size, the left and top
    /// ones also move the corner; the size never goes under the minimum.
    /// </summary>
    public static Rect Resized(Rect origin, string edges, Vector delta)
    {
        double left = origin.Left, top = origin.Top, right = origin.Right, bottom = origin.Bottom;
        if (edges.Contains('l', StringComparison.Ordinal))
        {
            left = Math.Min(origin.Left + delta.X, right - GraphAnnotations.MinWidth);
        }

        if (edges.Contains('r', StringComparison.Ordinal))
        {
            right = Math.Max(origin.Right + delta.X, left + GraphAnnotations.MinWidth);
        }

        if (edges.Contains('t', StringComparison.Ordinal))
        {
            top = Math.Min(origin.Top + delta.Y, bottom - GraphAnnotations.MinHeight);
        }

        if (edges.Contains('b', StringComparison.Ordinal))
        {
            bottom = Math.Max(origin.Bottom + delta.Y, top + GraphAnnotations.MinHeight);
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>Text box of the title of a comment, under <paramref name="element"/>.</summary>
    public static TextBox? FindTextBox(DependencyObject element)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, i);
            if (child is TextBox { Tag: "comment-edit" } box || FindTextBox(child) is TextBox deeper && (box = deeper) is not null)
            {
                return box;
            }
        }

        return null;
    }
}
