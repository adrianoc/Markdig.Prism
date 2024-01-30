using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Markdig.Prism
{
    public class PrismCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private readonly CodeBlockRenderer codeBlockRenderer;

        public PrismCodeBlockRenderer(CodeBlockRenderer codeBlockRenderer)
        {
            this.codeBlockRenderer = codeBlockRenderer ?? new CodeBlockRenderer();
        }

        protected override void Write(HtmlRenderer renderer, CodeBlock node)
        {
            var fencedCodeBlock = node as FencedCodeBlock;
            var parser = node.Parser as FencedCodeBlockParser;
            if (fencedCodeBlock == null || parser == null)
            {
                codeBlockRenderer.Write(renderer, node);
                return;
            }

            var languageCode = fencedCodeBlock.Info.Replace(parser.InfoPrefix, string.Empty);
            if (string.IsNullOrWhiteSpace(languageCode) || !PrismSupportedLanguages.IsSupportedLanguage(languageCode))
            {
                codeBlockRenderer.Write(renderer, node);
                return;
            }

            var codeAttributes = new HtmlAttributes();
            codeAttributes.AddClass($"language-{languageCode}");
            var lineParameters = ParseLineParameters(fencedCodeBlock);
            if (lineParameters.AddLineNumbers)
            {
                codeAttributes.AddClass($"line-numbers");
                codeAttributes.AddClass($"linkable-line-numbers");
            }
            
            var preAttributes = new HtmlAttributes();
            if (lineParameters.HighlightLines != null)
            {
                preAttributes.AddProperty("data-line", lineParameters.HighlightLines);
                ValidateLineRange(lineParameters.HighlightLines.AsMemory(), fencedCodeBlock.Lines.Count);
                //ValidateLineRange(lineParameters.HighlightLines.AsSpan(), fencedCodeBlock.Lines.Count);
            }

            var code = ExtractSourceCode(node);
            var escapedCode = HttpUtility.HtmlEncode(code);

            renderer
                .Write("<pre")
                .WriteAttributes(preAttributes)
                .Write(">")
                .Write("<code")
                .WriteAttributes(codeAttributes)
                .Write(">")
                .Write(escapedCode)
                .Write("</code>")
                .Write("</pre>");
        }

        // Parses arguments passed through the fenced code block as "```lang [#[:firstline[-lastline]]]
        private LineParameters ParseLineParameters(FencedCodeBlock fencedCodeBlock)
        {
            var argsSpan = fencedCodeBlock.Arguments.AsSpan();
            var lineNumberMarkerIndex = argsSpan.IndexOf('#');
            var highlightLines = string.Empty;
            if (lineNumberMarkerIndex != -1 && (lineNumberMarkerIndex + 1) < argsSpan.Length && argsSpan[lineNumberMarkerIndex + 1] == ':')
            {
                highlightLines = argsSpan.Slice(lineNumberMarkerIndex + 2).ToString();
            }

            return new LineParameters(lineNumberMarkerIndex != -1, highlightLines);
        }

        private void ValidateLineRange(ReadOnlyMemory<char> lines, int numberOfLines)
        {
            if (lines.Length == 0)
                return;
            
            int startLine = 0;
            int endLine = -1;
            
            foreach (var range in RangesFrom(lines))
            {
                var rangeSeparatorIndex = range.Span.Slice(1).IndexOf('-');  // start at 1 to avoid handling - (minus)
                                                                                // from negative startLine as the separator 
                if (rangeSeparatorIndex != -1)
                {
                    ValidateRange(range.Span, rangeSeparatorIndex);
                }
                else
                {
                    if (!Int32.TryParse(range.Span.ToString(), out startLine))
                    {
                        throw new ArgumentOutOfRangeException($"Invalid line ({lines}). This value must be in the range [1-{numberOfLines}].");
                    }
                    
                    if (startLine <= 0 || startLine > numberOfLines)
                    {
                        throw new ArgumentOutOfRangeException($"Invalid line ({startLine}). This value must be in the range [1-{numberOfLines}].");
                    }
                }
            }

            IEnumerable<ReadOnlyMemory<char>> RangesFrom(ReadOnlyMemory<char> line)
            {
                var commaIndex = line.Span.IndexOf(',');
                var startIndex = 0;
                while (commaIndex != -1)
                {
                    yield return line.Slice(startIndex, commaIndex);
                    startIndex += commaIndex + 1;
                    commaIndex = line.Slice(startIndex).Span.IndexOf(',');
                }
                
                yield return line.Slice(startIndex);
            }
            
            void ValidateRange(ReadOnlySpan<char> range, int rangeSeparatorIndex)
            {
                startLine = Int32.Parse(range.Slice(0, rangeSeparatorIndex + 1).ToString());
                endLine = Int32.Parse(range.Slice(rangeSeparatorIndex + 2).ToString());
                
                if (startLine <= 0 || startLine > numberOfLines)
                {
                    throw new ArgumentOutOfRangeException($"Invalid start line ({startLine}). This value must be in the range [1-{numberOfLines}].");
                }
                
                if (endLine < startLine || endLine > numberOfLines)
                {
                    throw new ArgumentOutOfRangeException($"Invalid end line ({endLine}). This value must be in the range [{startLine}-{numberOfLines}].");
                }
            }            
        }

        protected string ExtractSourceCode(LeafBlock node)
        {
            var code = new StringBuilder();
            var lines = node.Lines.Lines;
            int totalLines = lines.Length;
            for (int i = 0; i < totalLines; i++)
            {
                var line = lines[i];
                var slice = line.Slice;
                if (slice.Text == null)
                {
                    continue;
                }

                var lineText = slice.Text.Substring(slice.Start, slice.Length);
                if (i > 0)
                {
                    code.AppendLine();
                }

                code.Append(lineText);
            }

            return code.ToString();
        }
    }

    public struct LineParameters
    {
        public LineParameters(bool addLineNumbers, string highlightLines)
        {
            AddLineNumbers = addLineNumbers;
            HighlightLines = highlightLines;
        }

        public bool AddLineNumbers { get; }
        public string HighlightLines { get;}
    }
}
