﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Textamina.Markdig.Extensions;
using Textamina.Markdig.Helpers;
using Textamina.Markdig.Syntax;
using Textamina.Markdig.Syntax.Inlines;

namespace Textamina.Markdig.Formatters.Html
{
    public class HtmlFormatterOld
    {
        private readonly HtmlTextWriter writer;

        private readonly Dictionary<Type, Action<object>> registeredWriters;
        private readonly Dictionary<Type, Action<object>> inheritedWriters;

        private bool implicitParagraph;

        protected bool EnableHtmlForInline;

        public HtmlFormatterOld(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.NewLine = "\n";
            this.writer = new HtmlTextWriter(writer);
            registeredWriters = new Dictionary<Type, Action<object>>
            {
                [typeof(ListBlock)] = o => Write((ListBlock)o), // DONE
                [typeof(FencedCodeBlock)] = o => Write((FencedCodeBlock)o), // DONE
                [typeof(CodeBlock)] = o => Write((CodeBlock)o), // DONE
                [typeof(HeadingBlock)] = o => Write((HeadingBlock)o), // DONE
                [typeof(ThematicBreakBlock)] = o => Write((ThematicBreakBlock)o), // DONE
                [typeof(QuoteBlock)] = o => Write((QuoteBlock)o), // DONE
                [typeof(ParagraphBlock)] = o => Write((ParagraphBlock)o), // DONE
                [typeof(HtmlBlock)] = o => Write((HtmlBlock)o), // DONE


                [typeof(LiteralInline)] = o => Write((LiteralInline)o),
                [typeof(CodeInline)] = o => Write((CodeInline)o),
                [typeof(LinkInline)] = o => Write((LinkInline)o),
                [typeof(AutolinkInline)] = o => Write((AutolinkInline)o),
                [typeof(HtmlInline)] = o => Write((HtmlInline)o),
                [typeof(EmphasisInline)] = o => Write((EmphasisInline)o),
                [typeof(SoftlineBreakInline)] = o => Write((SoftlineBreakInline)o),
                [typeof(HardlineBreakInline)] = o => Write((HardlineBreakInline)o),
                [typeof(ContainerInline)] = o => WriteChildren((ContainerInline)o),

                // TODO: TEMP Extensions for tables

                [typeof(TableBlock)] = o => Write((TableBlock)o),
            };

            inheritedWriters = new Dictionary<Type, Action<object>>()
            {
                [typeof (DelimiterInline)] = o => Write((DelimiterInline) o),
            };

            EnableHtmlForInline = true;
        }

        public void Write(Document document)
        {
            WriteContainer((ContainerBlock) document);
        }

        protected void Write(ListBlock listBlock)
        {
            writer.EnsureLine();
            if (listBlock.IsOrdered)
            {
                writer.WriteConstant("<ol");
                if (listBlock.OrderedStart != 1)
                {
                    writer.WriteConstant(" start=\"");
                    writer.WriteConstant(listBlock.OrderedStart.ToString(CultureInfo.InvariantCulture));
                    writer.WriteConstant("\"");
                }
                writer.WriteLineConstant(">");
            }
            else
            {
                writer.WriteLineConstant("<ul>");
            }
            foreach (var item in listBlock.Children)
            {
                var listItem = (ListItemBlock) item;
                var previousImplicit = implicitParagraph;
                implicitParagraph = !listBlock.IsLoose;
                Write(listItem, listBlock.IsLoose);
                implicitParagraph = previousImplicit;
            }
            writer.WriteLineConstant(listBlock.IsOrdered ? "</ol>": "</ul>");
        }

        protected void Write(ListItemBlock listBlockItem, bool isLoose)
        {
            writer.EnsureLine();
            writer.WriteConstant("<li>");
            WriteContainer(listBlockItem);
            writer.WriteLineConstant("</li>");
        }

        protected void Write(FencedCodeBlock fencedCodeBlock)
        {
            Write(fencedCodeBlock, fencedCodeBlock.Language, fencedCodeBlock.Arguments);
        }

        protected void Write(CodeBlock codeBlock)
        {
            Write(codeBlock, null, null);
        }

        protected void Write(CodeBlock codeBlock, string language, string arguments)
        {
            writer.EnsureLine();
            // class="language-ruby
            writer.WriteConstant("<pre><code");
            if (string.IsNullOrEmpty(language))
            {
                writer.WriteConstant(">");
            }
            else
            {
                writer.WriteConstant(" class=\"language-");
                HtmlHelper.EscapeHtml(language, writer);
                writer.WriteConstant("\">");
            }
            WriteLeaf(codeBlock, true, true);
            writer.WriteLineConstant("</code></pre>");
        }


        protected void Write(HtmlBlock codeBlock)
        {
            WriteLeaf(codeBlock, true, false);
        }

        protected void Write(HtmlInline htmlInline)
        {
            writer.WriteConstant(htmlInline.Tag);
        }

        protected void Write(HeadingBlock headingBlock)
        {
            var heading = headingBlock.Level.ToString(CultureInfo.InvariantCulture);
            writer.WriteConstant("<h");
            writer.WriteConstant(heading);
            writer.WriteConstant(">");
            WriteLeaf(headingBlock, false, false);
            writer.WriteConstant("</h");
            writer.WriteConstant(heading);
            writer.WriteLineConstant(">");
        }

        protected void Write(ThematicBreakBlock breakBlock)
        {
            writer.WriteLineConstant("<hr />");
        }


        protected void Write(QuoteBlock quoteBlock)
        {
            writer.EnsureLine();
            writer.WriteLineConstant("<blockquote>");
            var savedImplicitParagraph = implicitParagraph;
            implicitParagraph = false;
            WriteContainer(quoteBlock);
            implicitParagraph = savedImplicitParagraph;
            writer.WriteLineConstant("</blockquote>");
        }

        protected void Write(ParagraphBlock paragraph)
        {
            writer.EnsureLine();
            if (!implicitParagraph)
            {
                writer.WriteConstant("<p>");
            }
            WriteLeaf(paragraph, false, false);
            if (!implicitParagraph)
            {
                writer.WriteLineConstant("</p>");
            }
        }

        protected void Write(LiteralInline literal)
        {
            // TODO: Provide an optimized version
            HtmlHelper.EscapeHtml(literal.Content.ToString(), writer);
        }

        protected void Write(AutolinkInline autolink)
        {
            if (EnableHtmlForInline)
            {
                writer.WriteConstant("<a href=\"");
                if (autolink.IsEmail)
                {
                    writer.WriteConstant("mailto:");
                }
                HtmlHelper.EscapeUrl(autolink.Url, writer);
                writer.WriteConstant("\">");
            }

            HtmlHelper.EscapeHtml(autolink.Url, writer);

            if (EnableHtmlForInline)
            {
                writer.WriteConstant("</a>");
            }
        }

        protected void Write(CodeInline code)
        {
            if (EnableHtmlForInline)
            {
                writer.WriteConstant("<code>");
            }
            HtmlHelper.EscapeHtml(code.Content, writer);
            if (EnableHtmlForInline)
            {
                writer.WriteConstant("</code>");
            }
        }

        protected void Write(LinkInline link)
        {
            if (EnableHtmlForInline)
            {
                writer.WriteConstant(link.IsImage ? "<img src=\"" : "<a href=\"");
                HtmlHelper.EscapeUrl(link.Url, writer);
                writer.WriteConstant("\"");
            }
            if (link.IsImage)
            {
                if (EnableHtmlForInline)
                {
                    writer.WriteConstant(" alt=\"");
                }
                var wasEnableHtmlForInline = EnableHtmlForInline;
                EnableHtmlForInline = false;
                WriteChildren(link);
                EnableHtmlForInline = wasEnableHtmlForInline;
                if (EnableHtmlForInline)
                {
                    writer.WriteConstant("\"");
                }
            }

            if (EnableHtmlForInline && !string.IsNullOrEmpty(link.Title))
            {
                writer.WriteConstant(" title=\"");
                HtmlHelper.EscapeHtml(link.Title, writer);
                writer.WriteConstant("\"");
            }

            if (link.IsImage)
            {
                if (EnableHtmlForInline)
                {
                    writer.WriteConstant(" />");
                }
            }
            else
            {
                if (EnableHtmlForInline)
                {
                    writer.WriteConstant(">");
                }
                WriteChildren(link);
                if (EnableHtmlForInline)
                {
                    writer.WriteConstant("</a>");
                }
            }
        }

        protected void Write(EmphasisInline emphasisInline)
        {
            if (EnableHtmlForInline)
            {
                writer.WriteConstant(emphasisInline.Strong ? "<strong>" : "<em>");
            }
            WriteChildren(emphasisInline);
            if (EnableHtmlForInline)
            {
                writer.WriteConstant(emphasisInline.Strong ? "</strong>" : "</em>");
            }
        }

        protected void Write(HardlineBreakInline hardlineBreak)
        {
            if (EnableHtmlForInline)
            {
                writer.WriteLineConstant("<br />");
            }
            else
            {
                writer.WriteConstant(" ");
            }
        }

        private void Write(SoftlineBreakInline softLineBreak)
        {
            writer.WriteLine();
        }

        protected void Write(TableBlock tableBlock)
        {
            writer.EnsureLine();
            writer.WriteLineConstant("<table>");

            bool hasBody = false;
            var header = (TableRowBlock)tableBlock.Children[0];
            if (!header.IsHeader)
            {
                header = null;
            }
            foreach (var rowObj in tableBlock.Children)
            {
                var row = (TableRowBlock)rowObj;
                if (row.IsHeader)
                {
                    writer.WriteLineConstant("<thead>");
                }
                else if (!hasBody)
                {
                    writer.WriteLineConstant("<tbody>");
                    hasBody = true;
                }
                writer.WriteLineConstant("<tr>");
                for (int i = 0; i < row.Children.Count; i++)
                {
                    var cellObj = row.Children[i];
                    var cell = (TableCellBlock) cellObj;

                    writer.EnsureLine();
                    if (row.IsHeader)
                    {
                        writer.WriteConstant("<th>");
                    }
                    else
                    {
                        writer.WriteConstant("<td");
                        if (header != null && i < header.ColumnAlignments.Count)
                        {
                            switch (header.ColumnAlignments[i])
                            {
                                case TableColumnAlignType.Center:
                                    writer.WriteConstant(" style=\"text-align: center;\"");
                                    break;
                                case TableColumnAlignType.Right:
                                    writer.WriteConstant(" style=\"text-align: right;\"");
                                    break;
                            }
                        }
                        writer.WriteConstant(">");
                    }

                    WriteLeaf(cell, false, false);
                    writer.WriteLineConstant(row.IsHeader ? "</th>" : "</td>");
                }
                writer.WriteLineConstant("</tr>");
                if (row.IsHeader)
                {
                    writer.WriteLineConstant("</thead>");
                }
            }

            if (hasBody)
            {
                writer.WriteLineConstant("</tbody>");
            }
        }

        protected void Write(DelimiterInline delimiterInline)
        {
            writer.WriteConstant(delimiterInline.ToLiteral());
            WriteChildren(delimiterInline);
        }

        protected void WriteChildren(ContainerInline containerInline)
        {
            var inline = containerInline.FirstChild;
            while (inline != null)
            {
                WriteDispatch(inline);
                inline = inline.NextSibling;
            }
        }

        protected void WriteDispatch(Inline inline)
        {
            Action<object> writerAction;
            if (registeredWriters.TryGetValue(inline.GetType(), out writerAction))
            {
                writerAction(inline);
            }
            else
            {
                foreach (var inheritedWriter in inheritedWriters)
                {
                    if (inheritedWriter.Key.GetTypeInfo().IsAssignableFrom(inline.GetType().GetTypeInfo()))
                    {
                        registeredWriters[inline.GetType()] = inheritedWriter.Value;
                        inheritedWriter.Value(inline);
                        break;
                    }
                }

            }
        }

        protected void WriteLeaf(LeafBlock leafBlock, bool writeEndOfLines, bool escape)
        {
            var inline = (Inline)leafBlock.Inline;
            if (inline != null)
            {
                while (inline != null)
                {
                    WriteDispatch(inline);
                    inline = inline.NextSibling;
                }
            }
            else if (leafBlock.Lines != null)
            {
                var lines = leafBlock.Lines;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!writeEndOfLines && i > 0)
                    {
                        writer.WriteLine();
                    }
                    var line = lines.Lines[i];
                    if (escape)
                    {
                        HtmlHelper.EscapeHtml(line.ToString(), writer);
                    }
                    else
                    {
                        writer.WriteConstant(line.ToString());
                    }
                    if (writeEndOfLines)
                    {
                        writer.WriteLine();
                    }
                }
            }
        }

        protected void WriteContainer(ContainerBlock container)
        {
            foreach (var child in container.Children)
            {
                Action<object> writerAction;
                if (registeredWriters.TryGetValue(child.GetType(), out writerAction))
                {
                    writerAction(child);
                }
            }
        }
    }
}