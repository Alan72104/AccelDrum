using System;
using System.Text;

namespace AccelDrum.Game.Utils;

public class MatrixPrinter(int initialRows = 1, int initialColumns = 1)
{
    public string Separator { get; init; } = ", ";
    public bool Align { get; init; } = true;

    /// <summary>
    /// Backing matrix is row major, use <c>[y, x]</c> or <c>[row, column]</c> to access element
    /// </summary>
    private string?[,] matrix = new string?[initialRows, initialColumns];

    public string? this[int x, int y]
    {
        get => (x >= 0 && x < matrix.GetLength(1) && y >= 0 && y < matrix.GetLength(0))
                ? matrix[y, x]
                : null;
        set => Set(x, y, value);
    }

    public override string ToString()
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        int[]? columnWidths = null;
        if (Align)
        {
            columnWidths = new int[cols];
            for (int x = 0; x < cols; x++)
            {
                int maxWidth = 0;
                for (int y = 0; y < rows; y++)
                    maxWidth = Math.Max(matrix[y, x]?.Length ?? 0, maxWidth);
                columnWidths![x] = maxWidth;
            }
        }

        StringBuilder sb = new();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                string ele = matrix[y, x] ?? "";
                sb.Append(ele);
                if (Align)
                    sb.Append(' ', Math.Max(0, columnWidths![x] - ele.Length));

                sb.Append(Separator);
            }
            sb.Length -= Separator.Length;
            sb.AppendLine();
        }
        sb.Length -= Environment.NewLine.Length;
        return sb.ToString();
    }

    public void Set(int x, int y, object? content)
    {
        EnsureCapacity(x + 1, y + 1);
        matrix[y, x] = content?.ToString();
    }

    public void Set<T>(int x, int y, in T content) where T : unmanaged
    {
        EnsureCapacity(x + 1, y + 1);
        matrix[y, x] = content.ToString();
    }

    public void Clear()
    {
        matrix = new string?[initialRows, initialColumns];
    }

    private void EnsureCapacity(int newCols, int newRows)
    {
        int curRows = matrix.GetLength(0);
        int curCols = matrix.GetLength(1);
        if (curRows == newRows && curCols == newCols)
            return;
        newRows = Math.Max(curRows, newRows);
        newCols = Math.Max(curCols, newCols);
        string?[,] newMat = new string?[newRows, newCols];

        for (int y = 0; y < curRows; y++)
        {
            Array.ConstrainedCopy(
                matrix,
                y * curCols,
                newMat,
                y * newCols,
                curCols);
        }

        matrix = newMat;
    }
}
