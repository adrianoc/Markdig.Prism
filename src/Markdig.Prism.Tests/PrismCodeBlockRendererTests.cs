using System;
using System.Linq;
using HtmlAgilityPack;
using NUnit.Framework;

namespace Markdig.Prism.Tests
{
    [TestFixture]
    public class PrismCodeBlockRendererTests
    {
        private static readonly String TestMarkdownWithLanguage = @"
# Sample Dockerfile

```docker
FROM nginx
ENV AUTHOR=Docker

WORKDIR /usr/share/nginx/html
COPY Hello_docker.html /usr/share/nginx/html

CMD cd /usr/share/nginx/html && sed -e s/Docker/""$AUTHOR""/ Hello_docker.html > index.html ; nginx -g 'daemon off;'
```

Use **docker** command to build the imaage from this Dockerfile.
";

        private static readonly string TestMarkdownWithoutLanguage = "Use ```docker run .``` command to build the image";

        private static readonly string TestMarkdownWithUnsupportedLanguage = "Simple graph ```mermaid graph TD; A --> B``` as an example";

        private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UsePrism()
                .Build();

        [Test]
        public void RenderValidPrismCodeBlock()
        {
            var html = Markdown.ToHtml(TestMarkdownWithLanguage, pipeline);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var pre = doc.DocumentNode.SelectSingleNode("//pre");
            Assert.That(pre, Is.Not.Null);
            var code = pre.ChildNodes.First();
            Assert.That(code.Name, Is.EqualTo("code"));
            var className = code.Attributes.First(a => a.Name == "class");
            Assert.That(className.Value, Is.EqualTo("language-docker"));
            Assert.That(code.InnerHtml.IndexOf("FROM nginx"), Is.GreaterThan(-1));
        }

        [Test]
        public void UseDefaultCodeBlockRendererIfNoLanguageSpecified()
        {
            var html = Markdown.ToHtml(TestMarkdownWithoutLanguage, pipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var pre = doc.DocumentNode.SelectSingleNode("//pre");
            Assert.That(pre, Is.Null);
            var code = doc.DocumentNode.SelectSingleNode("//code");
            Assert.That(code, Is.Not.Null);
            Assert.That(code.InnerHtml, Is.EqualTo("docker run ."));
        }

        [Test]
        public void UseDefaultCodeBlockRendererIfLanguageIsNotSupported()
        {
            var html = Markdown.ToHtml(TestMarkdownWithUnsupportedLanguage, pipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var pre = doc.DocumentNode.SelectSingleNode("//pre");
            Assert.That(pre, Is.Null);
            var code = doc.DocumentNode.SelectSingleNode("//code");
            Assert.That(code, Is.Not.Null);
            Assert.That(code.InnerHtml, Is.EqualTo("mermaid graph TD; A --&gt; B"));
        }        
        
        [Test]
        public void FencedCodeBlockWithValidArguments([Values("1-3", "1", "3", "1,3,5", "3-5,2")] string range)
        {
            var html = Markdown.ToHtml($$"""
                                       ```CSharp #:{{range}}
                                       class C
                                       {
                                          // Line 3
                                          // Line 4
                                       }
                                       ```
                                       """, pipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var pre = doc.DocumentNode.SelectSingleNode("//pre");
            Assert.That(pre, Is.Not.Null);
            Assert.That(pre.Attributes["data-line"].Value, Is.EqualTo(range));

            var code = doc.DocumentNode.SelectSingleNode("//code");
            Assert.That(code, Is.Not.Null);
            Assert.That(code.Attributes["class"].Value, Is.EqualTo("language-CSharp line-numbers linkable-line-numbers"));
        }
        
        [Test]
        public void InvalidStartLineInMultiLineSelectionThrowArgumentOutOfRangeException([Values(-1, 0, 4)] int startLine)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Markdown.ToHtml($$"""
                                                                               ```CSharp #:{{startLine}}-3
                                                                               class C
                                                                               {
                                                                               }
                                                                               ```
                                                                               """, pipeline));

        }
        
        [Test]
        public void InvalidStartLineInSingleLineSelectionThrowArgumentOutOfRangeException([Values(-1, 0, 4)] int startLine)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Markdown.ToHtml($$"""
                                                                               ```CSharp #:{{startLine}}
                                                                               class C
                                                                               {
                                                                               }
                                                                               ```
                                                                               """, pipeline));
        }

        [Test]
        public void ShowLineNumberWithNoHighlightedLine()
        {
            var html = Markdown.ToHtml($$"""
                                         ```CSharp #:
                                         class C
                                         {
                                            // Line 3
                                            // Line 4
                                         }
                                         ```
                                         """, pipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var pre = doc.DocumentNode.SelectSingleNode("//pre");
            Assert.That(pre, Is.Not.Null);
            Assert.That(pre.Attributes, Does.Not.Contain("data-line"));

            var code = doc.DocumentNode.SelectSingleNode("//code");
            Assert.That(code, Is.Not.Null);
            Assert.That(code.Attributes["class"].Value, Is.EqualTo("language-CSharp line-numbers linkable-line-numbers"));
        }
    }
}
