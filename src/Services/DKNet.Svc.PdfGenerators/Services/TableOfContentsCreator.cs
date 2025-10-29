using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DKNet.Svc.PdfGenerators.Options;
using Markdig.Helpers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Tokens;

namespace DKNet.Svc.PdfGenerators.Services;

internal class TableOfContentsCreator
{
    #region Fields

    private readonly string _closeListElement;

    private readonly EmbeddedResourceService _embeddedResourceService;
    private LinkWithPageNumber[]? _linkPages;

    private Link[]? _links;
    private readonly int _maxDepthLevel;

    // Substract 1 to adjust to 0 based values
    private readonly int _minDepthLevel;
    private readonly string _openListElement;

    private readonly TableOfContentsOptions _options;
    private static readonly Regex EmojiReg = new(":(\\w+):", RegexOptions.Compiled);

    private static readonly Regex HeaderReg = new(
        "^(?<hashes>#{1,6}) +(?<title>[^\r\n]*)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

    private static readonly Regex HtmlElementReg = new("<[^>]*>[^>]*</[^>]*>|<[^>]*/>", RegexOptions.Compiled);

    private static readonly Regex InsertionRegex = new(
        """^(\[TOC]|\[\[_TOC_]]|<!-- toc -->)\r?$""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    //private static readonly Regex LineBreakRegex = new("\r\n?|\n", RegexOptions.Compiled);
    private static readonly string Nl = Environment.NewLine;

    #endregion

    #region Constructors

    public TableOfContentsCreator(
        TableOfContentsOptions options,
        IConversionEvents conversionEvents,
        EmbeddedResourceService embeddedResourceService)
    {
        this._options = options;
        var isOrdered = options.ListStyle == ListStyle.OrderedDefault
                        || options.ListStyle == ListStyle.Decimals;
        this._minDepthLevel = options.MinDepthLevel - 1;
        this._maxDepthLevel = options.MaxDepthLevel - 1;
        this._embeddedResourceService = embeddedResourceService;
        this._openListElement = isOrdered ? "<ol>" : "<ul>";
        this._closeListElement = isOrdered ? "</ol>" : "</ul>";

        conversionEvents.HtmlConverting += this.InternalAddToMarkdown;
        conversionEvents.TemplateModelCreating += this.InternalAddStylesToTemplateAsync;

        if (options.PageNumberOptions != null)
        {
            conversionEvents.TempPdfCreated += this.InternalReadPageNumbers;
        }
    }

    #endregion

    #region Methods

    internal async Task InternalAddStylesToTemplateAsync(object? _, TemplateModelEventArgs e)
    {
        var tableOfContentsDecimalStyle = this._options.ListStyle switch
        {
            ListStyle.None => ListStyleNone,
            ListStyle.Decimals => await this._embeddedResourceService.GetResourceContentAsync(DecimalStyleFileName),
            _ => string.Empty
        };

        if (!this._options.HasColoredLinks)
        {
            tableOfContentsDecimalStyle += Environment.NewLine + ".table-of-contents a { all: unset; }";
        }

        if (this._options.PageNumberOptions != null)
        {
            tableOfContentsDecimalStyle += Environment.NewLine +
                                           await this._embeddedResourceService.GetResourceContentAsync(
                                               PageNumberStyleFileName);
        }

        e.TemplateModel.Add(TocStyleKey, tableOfContentsDecimalStyle);
    }

    private void InternalAddToMarkdown(object? sender, MarkdownEventArgs e)
    {
        var tocHtml = this.InternalToHtml(e.MarkdownContent);
        e.MarkdownContent = InternalInsertInto(e.MarkdownContent, tocHtml);
    }

    private string InternalCreatedDenestingTags(int depth, int lastDepth)
    {
        var difference = lastDepth - depth;
        var html = string.Empty;

        for (var i = 0; i < difference; ++i)
        {
            html += Nl + "</li>" + Nl + this._closeListElement;
        }

        return html + Nl + "<li>";
    }

    private IEnumerable<Link> InternalCreateLinks(string markdownContent)
    {
        var matches = HeaderReg.Matches(markdownContent);
        var links = new List<Link>(matches.Count);
        var linkAddresses = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            var depth = match.Groups["hashes"].Value.Length - 1;
            var title = match.Groups["title"].Value;

            if (depth < this._minDepthLevel
                || depth > this._maxDepthLevel
                || title.ToLower(CultureInfo.CurrentCulture)
                    .EndsWith(OmitInTocIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // build link
            title = HtmlElementReg.Replace(title, string.Empty);
            title = EmojiReg.Replace(title, string.Empty).Trim();

            var linkAddress = LinkHelper.Urilize(title, false);
            linkAddress = "#" + linkAddress.ToLowerInvariant();

            // ensure every linkAddress is unique
            var counterVal = 2;
            var linkAddressUnique = linkAddress;
            while (linkAddresses.Contains(linkAddressUnique))
            {
                // add an increasing number at the end
                linkAddressUnique = linkAddress + "-" + counterVal;
                counterVal += 1;
                if (counterVal > 99)
                {
                    break; // limit to 99 in case of error
                }
            }

            linkAddresses.Add(linkAddressUnique);

            links.Add(new Link(title, linkAddressUnique, depth));
        }

        return links;
    }

    private string InternalCreateLinkText(Link link)
    {
        if (this._options.PageNumberOptions == null)
        {
            return link.ToHtml();
        }

        if (this._linkPages == null)
        {
            return link.ToHtml(-1); // Placeholder
        }

        var pageNumber = this._linkPages
            .First(l => string.Equals(l.LinkAddress, link.LinkAddress, StringComparison.OrdinalIgnoreCase)).PageNumber;
        return link.ToHtml(pageNumber);
    }

    private string InternalCreateNestingTags(int depth, int lastDepth)
    {
        var difference = depth - lastDepth;
        var html = string.Empty;

        // open nesting
        for (var i = 0; i < difference; ++i)
        {
            // only provide ListStyle for elements that actually have text
            var extraStyle = difference > 1 && i != difference - 1
                ? " style='list-style:none'"
                : string.Empty;

            html += Nl + this._openListElement + Nl + $"<li{extraStyle}>";
        }

        return html;
    }

    private static string InternalCreateSameDepthTags() => "</li>" + Nl + "<li>";

    private static string InternalGetDescription(Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString())!;
        var attribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();

        return attribute != null ? attribute.Description : value.ToString();
    }

    private static string InternalInsertInto(string content, string tocHtml)
        => InsertionRegex.Replace(content, tocHtml);

    private static IEnumerable<LinkWithPageNumber> InternalParsePageNumbersFromPdf(PdfDocument pdf, Link[] links)
    {
        var linkPages = new LinkWithPageNumber[links.Length];
        var linksToFind = links.ToList();

        foreach (var page in pdf.GetPages())
        {
            //var text = ContentOrderTextExtractor.GetText(page);
            //var lines = LineBreakRegex.Split(text);
            var annotations = page.GetAnnotations();

            // the invisible link rectangles in the TOC contains the link addresses and the destination page
            // it is possible to extract both information and link them to the TOC elements
            foreach (var annotation in annotations)
            {
                // check if it is a GoTo annotation and contains a link destination
                if (annotation.Action!.Type != ActionType.GoTo
                    || !annotation.AnnotationDictionary.ContainsKey(NameToken.Dest))
                {
                    continue;
                }

                // extract the destination form dictionary (instead of the # there is a leading /)
                annotation.AnnotationDictionary.TryGet(NameToken.Dest, out var linkToken);
                var linkAddress = linkToken!.ToString()!.Replace('/', '#');

                // try to find the link address in all links
                foreach (var link in linksToFind)
                {
                    if (!string.Equals(link.LinkAddress, linkAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // get the page number from action destination
                    var pageNumber = ((GoToAction)annotation.Action).Destination.PageNumber;

                    // save in link pages and remove links from the links to find
                    linkPages[Array.IndexOf(links, link)] = new LinkWithPageNumber(link, pageNumber);
                    linksToFind.Remove(link);
                    if (linksToFind.Count == 0)
                    {
                        return linkPages; // All links found
                    }

                    break; // Found link, continue with next line
                }
            }
        }

        return linkPages;
    }

    private void InternalReadPageNumbers(object? _, PdfEventArgs e)
    {
        // TODO: what if link not found
        if (this._links == null)
        {
            throw new InvalidOperationException("Links have not been created yet.");
        }

        using var pdf = PdfDocument.Open(e.PdfPath);
        this._linkPages = [.. InternalParsePageNumbersFromPdf(pdf, this._links)];

        // Fill in values that could not be found
        var length = this._links.Length;
        for (var i = 0; i < length; ++i)
        {
            this._linkPages[i] = i == 0
                ? new LinkWithPageNumber(this._links[i], 1) // Assume first page
                : new LinkWithPageNumber(this._links[i], this._linkPages[i - 1].PageNumber); // Assume same as previous
        }
    }

    private string InternalToHtml(string markdownContent)
    {
        var links = this._links = [.. this.InternalCreateLinks(markdownContent)];
        if (links.Length == 0)
        {
            return string.Empty;
        }

        var minLinkDepth = links.Min(l => l.Depth);
        var minDepth = Math.Max(this._minDepthLevel, minLinkDepth); // ensure that there's no unneeded nesting

        var lastDepth = -1; // start at -1 to open the list on first element
        var tocBuilder = new StringBuilder();

        var htmlClasses = HtmlClassName;
        if (this._options.PageNumberOptions != null)
        {
            var leader = this._options.PageNumberOptions.TabLeader;
            var leaderClass = InternalGetDescription(leader);
            htmlClasses += $" {leaderClass}";
        }

        tocBuilder.Append(CultureInfo.InvariantCulture, $"<nav class=\"{htmlClasses}\">");

        foreach (var link in links)
        {
            var fixedDepth = link.Depth - minDepth; // Start counting from minDepth
            if (fixedDepth < 0)
            {
                continue;
            }

            var htmlListTags = fixedDepth switch
            {
                _ when fixedDepth > lastDepth => this.InternalCreateNestingTags(fixedDepth, lastDepth),
                _ when fixedDepth == lastDepth => InternalCreateSameDepthTags(),
                _ => this.InternalCreatedDenestingTags(fixedDepth, lastDepth)
            };

            tocBuilder.Append(htmlListTags);
            lastDepth = fixedDepth;

            tocBuilder.Append(this.InternalCreateLinkText(link));
        }

        // close open tags
        for (var i = 0; i <= lastDepth; ++i)
        {
            tocBuilder.Append(Nl + "</li>" + Nl + this._closeListElement);
        }

        tocBuilder.Append(Nl + "</nav>");

        return tocBuilder.ToString();
    }

    #endregion

    private const string DecimalStyleFileName = "TableOfContentsDecimalStyle.css";
    private const string HtmlClassName = "table-of-contents";

    private class Link(string title, string linkAddress, int depth)
    {
        #region Properties

        /// <summary>
        ///     Gets or sets Depth.
        /// </summary>
        public int Depth { get; } = depth;

        /// <summary>
        ///     Gets or sets LinkAddress.
        /// </summary>
        public string LinkAddress { get; } = linkAddress;

        /// <summary>
        ///     Gets or sets Title.
        /// </summary>
        public string Title { get; } = title;

        #endregion

        #region Methods

        public string ToHtml() => $"<a href=\"{this.LinkAddress}\">{this.Title}</a>";

        public string ToHtml(int pageNumber) => $"" +
                                                $"<a href=\"{this.LinkAddress}\">" +
                                                $"<span class=\"title\">{this.Title}</span>" +
                                                $"<span class=\"page-number\">{pageNumber}</span>" +
                                                $"</a>";

        #endregion
    }

    private class LinkWithPageNumber(Link link, int pageNumber)
        : Link(link.Title, link.LinkAddress, link.Depth)
    {
        #region Properties

        /// <summary>
        ///     Gets or sets PageNumber.
        /// </summary>
        public int PageNumber { get; } = pageNumber;

        #endregion
    }

    private const string ListStyleNone = ".table-of-contents ul { list-style: none; }";

    private const string OmitInTocIdentifier = "<!-- omit from toc -->";
    private const string PageNumberStyleFileName = "TableOfContentsPageNumberStyle.css";
    private const string TocStyleKey = "tocStyle";
}