using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AzureSearchCrawler
{
    /// <summary>
    /// Extracts text content from a web page. The default implementation is very simple: it removes all script, style,
    /// svg, and path tags, and then returns the InnerText of the page body, with cleaned up whitespace.
    /// <para/>You can implement your own custom text extraction by overriding the ExtractText method. The protected
    /// helper methods in this class might be useful. GetCleanedUpTextForXpath is the easiest way to get started.
    /// </summary>
    public partial class TextExtractor
    {
        private readonly Regex newlines = MyRegex();
        private readonly Regex spaces = MyRegex1();

        public List<Dictionary<string, object>> ExtractText(HtmlDocument doc, string xpath)
        {
            var pages = new List<Dictionary<string, object>>();
            
            // Extract the source web page's Open Graph preview image source from the document's metadata to use if a selected divNode has no image
            var sourceUrlPreviewImageSrc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image' or @name='twitter:image']")?.GetAttributeValue("content", string.Empty);

            // Separate page by div nodes for major section components
            var divNodes = doc.DocumentNode.SelectNodes("//div[@ocr-component-name='section-master' or @ocr-component-name='interactive-demo']");

            if (divNodes == null || divNodes.Count == 0)
            {
                // Fall back to the page body
                divNodes = doc.DocumentNode.SelectNodes(xpath);
            }
            {
                foreach (var divNode in divNodes)
                {
                    var page = new Dictionary<string, object>
                    {
                        { "content", divNode.InnerText },
                        // Use the first h1 or h2 tag within the divNode as the title, whichever is present in that priority order
                        { "title", divNode.SelectSingleNode(".//h1")?.InnerText ?? divNode.SelectSingleNode(".//h2")?.InnerText }
                    };

                    // Find a priority bookmark anchor to serve as destinationURL, according to HTML Standard https://developer.mozilla.org/en-US/docs/Web/HTML/Element/a#linking_to_an_element_on_the_same_page
                    var firstAnchor = divNode.SelectSingleNode(".//a[starts-with(@href, '#')]");
                    if (firstAnchor != null)
                    {
                        page["destinationURL"] = firstAnchor.GetAttributeValue("href", string.Empty);
                    }

                    // Find a priority image within the divNode to serve as a preview image; include alt text if available
                    var firstImage = divNode.SelectSingleNode(".//img");
                    if (firstImage != null)
                    {
                        page["imagePreviewUrl"] = firstImage.GetAttributeValue("src", string.Empty);
                        page["AltText"] = firstImage.GetAttributeValue("alt", string.Empty);
                    }
                    else if (!string.IsNullOrEmpty(sourceUrlPreviewImageSrc))
                    {
                        // When no priority image exists, use the Open Graph preview image of the overall page as the default image
                        page["imagePreviewUrl"] = sourceUrlPreviewImageSrc;
                    }

                    pages.Add(page);
                }
            }

            return pages;
        }

        public string GetCleanedUpTextForXpath(HtmlDocument doc, string xpath)
        {
            if (doc == null || doc.DocumentNode == null)
            {
                return null;
            }

            RemoveNodesOfType(doc, "script", "style", "svg", "path");

            string content = ExtractTextFromFirstMatchingElement(doc, xpath);
            return NormalizeWhitespace(content);
        }

        protected string NormalizeWhitespace(string content)
        {
            if (content == null)
            {
                return null;
            }

            content = newlines.Replace(content, "\n");
            return spaces.Replace(content, " ");
        }

        protected void RemoveNodesOfType(HtmlDocument doc, params string[] types)
        {
            string xpath = String.Join(" | ", types.Select(t => "//" + t));
            RemoveNodes(doc, xpath);
        }

        protected void RemoveNodes(HtmlDocument doc, string xpath)
        {
            var nodes = SafeSelectNodes(doc, xpath).ToList();
            // Console.WriteLine("Removing {0} nodes matching {1}.", nodes.Count, xpath);
            foreach (var node in nodes)
            {
                node.Remove();
            }
        }

        /// <summary>
        /// Returns InnerText of the first element matching the xpath expression, or null if no elements match.
        /// </summary>
        protected string ExtractTextFromFirstMatchingElement(HtmlDocument doc, string xpath)
        {
            return SafeSelectNodes(doc, xpath).FirstOrDefault()?.InnerText;
        }

        /// <summary>
        /// Null-safe DocumentNode.SelectNodes
        /// </summary>
        protected IEnumerable<HtmlNode> SafeSelectNodes(HtmlDocument doc, string xpath)
        {
            return doc.DocumentNode.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>();
        }

        [GeneratedRegex(@"(\r\n|\n)+")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"[ \t]+")]
        private static partial Regex MyRegex1();
    }
}
