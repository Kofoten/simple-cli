using System;
using System.Text;

namespace Kofoten.SimpleCli.Generator;

internal class CodeBuilder
{
    private readonly StringBuilder sb = new();
    private int indent = 0;

    public void AppendLine(string line = "", bool applyIndent = true)
    {
        if (string.IsNullOrEmpty(line))
        {
            sb.AppendLine();
            return;
        }

        if (applyIndent)
        {
            sb.Append(' ', indent * 4);
        }

        sb.AppendLine(line);
    }

    public void Append(string text, bool applyIndent = false)
    {
        if (applyIndent)
        {
            sb.Append(' ', indent * 4);
        }

        sb.Append(text);
    }

    public IDisposable StartBlock()
    {
        AppendLine("{");
        indent++;
        return new BlockScope(this);
    }

    private void EndBlock()
    {
        indent--;
        AppendLine("}");
    }

    /// <summary>
    /// Indents the code inside the using statement. For use in switch/case expressions
    /// or when you just want to indent an extra time.
    /// </summary>
    /// <returns>A scope that resets the indentation of teh code builder when disposed.</returns>
    public IDisposable Indent()
    {
        indent++;
        return new IndentScope(this);
    }

    public override string ToString() => sb.ToString();

    private readonly struct BlockScope(CodeBuilder builder) : IDisposable
    {
        private readonly CodeBuilder builder = builder;

        public void Dispose() => builder.EndBlock();
    }

    private readonly struct IndentScope(CodeBuilder builder) : IDisposable
    {
        private readonly CodeBuilder builder = builder;

        public void Dispose() => builder.indent--;
    }
}
