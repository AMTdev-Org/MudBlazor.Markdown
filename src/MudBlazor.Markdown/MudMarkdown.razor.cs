﻿using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

// ReSharper disable MemberCanBePrivate.Global

namespace MudBlazor;

public class MudMarkdown : ComponentBase, IDisposable
{
	protected IMudMarkdownThemeService? ThemeService;

	protected MarkdownPipeline? Pipeline;
	protected bool EnableLinkNavigation;

	/// <summary>
	/// Markdown text to be rendered in the component.
	/// </summary>
	[Parameter]
	public string Value { get; set; } = string.Empty;

	/// <summary>
	/// Minimum width (in pixels) for a table cell.<br/>
	/// If <see langword="null" /> or negative the minimum width is not applied.
	/// </summary>
	[Parameter]
	public int? TableCellMinWidth { get; set; }

	/// <summary>
	/// Command which is invoked when a link is clicked.<br/>
	/// If <see langword="null" /> a link is opened in the browser.
	/// </summary>
	[Parameter]
	public ICommand? LinkCommand { get; set; }

	/// <summary>
	/// Theme of the code block.<br/>
	/// Browse available themes here: https://highlightjs.org/static/demo/
	/// </summary>
	[Parameter]
	public CodeBlockTheme CodeBlockTheme { get; set; }

	/// <summary>
	/// Override the original URL address of the <see cref="LinkInline"/>.<br/>
	/// If a function is not provided <see cref="LinkInline.Url"/> is used
	/// </summary>
	[Parameter]
	public Func<LinkInline, string?>? OverrideLinkUrl { get; set; }

	/// <summary>
	/// Typography variant to use for Heading Level 1-6.<br/>
	/// If a function is not provided a default typo for each level is set (e.g. for &lt;h1&gt; it will be <see cref="Typo.h1"/>, etc.)
	/// </summary>
	[Parameter]
	public Func<Typo, Typo>? OverrideHeaderTypo { get; set; }

	/// <summary>
	/// Override default styling of the markdown component
	/// </summary>
	[Parameter]
	public MudMarkdownStyling Styling { get; set; } = new();

	[Parameter]
	public MarkdownPipeline? MarkdownPipeline { get; set; }

	[Inject]
	protected NavigationManager? NavigationManager { get; init; }

	[Inject]
	protected IJSRuntime JsRuntime { get; init; } = default!;

	[Inject]
	protected IServiceProvider? ServiceProvider { get; init; }

	public virtual void Dispose()
	{
		if (NavigationManager != null)
			NavigationManager.LocationChanged -= NavigationManagerOnLocationChanged;

		if (ThemeService != null)
			ThemeService.CodeBlockThemeChanged -= OnCodeBlockThemeChanged;

		GC.SuppressFinalize(this);
	}

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		if (string.IsNullOrEmpty(Value))
			return;

		//Markdown doesn't recognize the Razor language for fenced code blocks; replace it (and its aliases) with normal HTML
		var razorAliases = new List<string> { "```razor", "```cshtml", "```razor-cshtml" };
		razorAliases.ForEach(ra => Value = Value.Replace(ra, "```html"));

		var pipeline = GetMarkdownPipeLine();
		var parsedText = Markdown.Parse(Value, pipeline);
		if (parsedText.Count == 0)
			return;

		builder.OpenElement(0, "article");
		builder.AddAttribute(1, "class", "mud-markdown-body");
		RenderMarkdown(parsedText, builder);
		builder.CloseElement();
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		ThemeService = ServiceProvider?.GetService<IMudMarkdownThemeService>();

		if (ThemeService != null)
			ThemeService.CodeBlockThemeChanged += OnCodeBlockThemeChanged;
	}

	protected override void OnAfterRender(bool firstRender)
	{
		if (!firstRender || !EnableLinkNavigation || NavigationManager == null)
			return;

		var args = new LocationChangedEventArgs(NavigationManager.Uri, true);
		NavigationManagerOnLocationChanged(NavigationManager, args);
		NavigationManager.LocationChanged += NavigationManagerOnLocationChanged;
	}

	protected virtual void RenderMarkdown(ContainerBlock container, RenderTreeBuilder builder)
	{
		for (var i = 0; i < container.Count; i++)
		{
			switch (container[i])
			{
				case ParagraphBlock paragraph:
				{
					RenderParagraphBlock(paragraph, builder);
					break;
				}
				case HeadingBlock heading:
				{
					var typo = (Typo)heading.Level;
					typo = OverrideHeaderTypo?.Invoke(typo) ?? typo;

					EnableLinkNavigation = true;

					var id = heading.BuildIdString();
					RenderParagraphBlock(heading, builder, typo, id);

					break;
				}
				case QuoteBlock quote:
				{
					builder.OpenElement(2, "blockquote");
					RenderMarkdown(quote, builder);
					builder.CloseElement();
					break;
				}
				case Table table:
				{
					RenderTable(table, builder);
					break;
				}
				case ListBlock list:
				{
					RenderList(list, builder);
					break;
				}
				case ThematicBreakBlock:
				{
					builder.OpenComponent<MudDivider>(3);
					builder.CloseComponent();
					break;
				}
				case FencedCodeBlock code:
				{
					var text = code.CreateCodeBlockText();

					builder.OpenComponent<MudCodeHighlight>(4);
					builder.AddAttribute(5, nameof(MudCodeHighlight.Text), text);
					builder.AddAttribute(6, nameof(MudCodeHighlight.Language), code.Info ?? string.Empty);
					builder.AddAttribute(7, nameof(MudCodeHighlight.Theme), CodeBlockTheme);
					builder.CloseComponent();

					break;
				}
				case HtmlBlock html:
				{
					if (html.TryGetDetails(out var detailsData))
						RenderDetailsHtml(builder, detailsData.Header, detailsData.Content);
					else
						RenderHtml(builder, html.Lines);

					break;
				}
				default:
				{
					OnRenderMarkdownBlockDefault(container[i]);
					break;
				}
			}
		}
	}

	/// <summary>
	/// Renders a markdown block which is not covered by the switch-case block in <see cref="RenderMarkdown"/> 
	/// </summary>
	protected virtual void OnRenderMarkdownBlockDefault(Markdig.Syntax.Block block)
	{
	}

	protected virtual void RenderParagraphBlock(LeafBlock paragraph, RenderTreeBuilder builder, Typo typo = Typo.body1, string? id = null)
	{
		if (paragraph.Inline == null)
			return;

		builder.OpenComponent<MudText>(8);

		if (!string.IsNullOrEmpty(id))
			builder.AddAttribute(9, "id", id);

		builder.AddAttribute(10, nameof(MudText.Typo), typo);
		builder.AddAttribute(11, nameof(MudText.ChildContent), (RenderFragment)(contentBuilder => RenderInlines(paragraph.Inline, contentBuilder)));
		builder.CloseComponent();
	}

	protected virtual void RenderInlines(ContainerInline inlines, RenderTreeBuilder builder)
	{
		foreach (var inline in inlines)
		{
			switch (inline)
			{
				case LiteralInline x:
				{
					builder.AddContent(12, x.Content);
					break;
				}
				case HtmlInline x:
				{
					builder.AddMarkupContent(13, x.Tag);
					break;
				}
				case LineBreakInline:
				{
					builder.OpenElement(14, "br");
					builder.CloseElement();
					break;
				}
				case CodeInline x:
				{
					builder.OpenElement(15, "code");
					builder.AddContent(16, x.Content);
					builder.CloseElement();
					break;
				}
				case EmphasisInline x:
				{
					if (!x.TryGetEmphasisElement(out var elementName))
						continue;

					builder.OpenElement(17, elementName);
					RenderInlines(x, builder);
					builder.CloseElement();
					break;
				}
				case LinkInline x:
				{
					var url = OverrideLinkUrl?.Invoke(x) ?? x.Url;

					if (x.IsImage)
					{
						var alt = x
							.OfType<LiteralInline>()
							.Select(static x => x.Content);

						builder.OpenComponent<MudImage>(18);
						builder.AddAttribute(19, nameof(MudImage.Class), "rounded-lg");
						builder.AddAttribute(20, nameof(MudImage.Src), url);
						builder.AddAttribute(21, nameof(MudImage.Alt), string.Join(null, alt));
						builder.AddAttribute(22, nameof(MudImage.Elevation), 25);
						builder.CloseComponent();
					}
					else if (LinkCommand == null)
					{
						builder.OpenComponent<MudLink>(23);
						builder.AddAttribute(24, nameof(MudLink.Href), url);
						builder.AddAttribute(25, nameof(MudLink.ChildContent), (RenderFragment)(linkBuilder => RenderInlines(x, linkBuilder)));

						if (url.IsExternalUri(NavigationManager?.BaseUri))
						{
							builder.AddAttribute(26, nameof(MudLink.Target), "_blank");
							builder.AddAttribute(27, "rel", "noopener noreferrer");
						}
						// (prevent scrolling to the top of the page)
						// custom implementation only for links on the same page
						else if (url?.StartsWith('#') ?? false)
						{
							builder.AddEventPreventDefaultAttribute(28, "onclick", true);
							builder.AddAttribute(29, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () =>
							{
								if (NavigationManager == null)
									return;

								var uriBuilder = new UriBuilder(NavigationManager.Uri)
								{
									Fragment = url,
								};
								var args = new LocationChangedEventArgs(uriBuilder.Uri.AbsoluteUri, true);
								NavigationManagerOnLocationChanged(NavigationManager, args);
							}));
						}

						builder.CloseComponent();
					}
					else
					{
						builder.OpenComponent<MudLinkButton>(30);
						builder.AddAttribute(31, nameof(MudLinkButton.Command), LinkCommand);
						builder.AddAttribute(32, nameof(MudLinkButton.CommandParameter), url);
						builder.AddAttribute(33, nameof(MudLinkButton.ChildContent), (RenderFragment)(linkBuilder => RenderInlines(x, linkBuilder)));
						builder.CloseComponent();
					}

					break;
				}
				case MathInline x:
				{
					builder.OpenComponent<MudMathJax>(34);
					builder.AddAttribute(35, nameof(MudMathJax.Delimiter), x.GetDelimiter());
					builder.AddAttribute(36, nameof(MudMathJax.Value), x.Content);
					builder.CloseComponent();
					break;
				}
				default:
				{
					OnRenderInlinesDefault(inline, builder);
					break;
				}
			}
		}
	}

	/// <summary>
	/// Renders inline block which is not covered by the switch-case block in <see cref="RenderInlines"/> 
	/// </summary>
	protected virtual void OnRenderInlinesDefault(Inline inline, RenderTreeBuilder builder)
	{
	}

	protected virtual void RenderTable(Table table, RenderTreeBuilder builder)
	{
		// First child is columns
		if (table.Count < 2)
			return;

		builder.OpenComponent<MudSimpleTable>(37);
		builder.AddAttribute(38, nameof(MudSimpleTable.Style), "overflow-x: auto;");
		builder.AddAttribute(39, nameof(MudSimpleTable.Striped), Styling.Table.IsStriped);
		builder.AddAttribute(40, nameof(MudSimpleTable.Bordered), Styling.Table.IsBordered);
		builder.AddAttribute(41, nameof(MudSimpleTable.Elevation), Styling.Table.Elevation);
		builder.AddAttribute(42, nameof(MudSimpleTable.ChildContent), (RenderFragment)(contentBuilder =>
		{
			// thread
			contentBuilder.OpenElement(43, "thead");
			RenderTableRow((TableRow)table[0], "th", contentBuilder, TableCellMinWidth);
			contentBuilder.CloseElement();

			// tbody
			contentBuilder.OpenElement(44, "tbody");
			for (var j = 1; j < table.Count; j++)
			{
				RenderTableRow((TableRow)table[j], "td", contentBuilder);
			}

			contentBuilder.CloseElement();
		}));
		builder.CloseComponent();
	}

	protected virtual void RenderTableRow(TableRow row, string cellElementName, RenderTreeBuilder builder, int? minWidth = null)
	{
		builder.OpenElement(45, "tr");

		for (var j = 0; j < row.Count; j++)
		{
			var cell = (TableCell)row[j];
			builder.OpenElement(46, cellElementName);

			if (minWidth is > 0)
				builder.AddAttribute(47, "style", $"min-width:{minWidth}px");

			if (cell.Count != 0 && cell[0] is ParagraphBlock paragraphBlock)
				RenderParagraphBlock(paragraphBlock, builder);

			builder.CloseElement();
		}

		builder.CloseElement();
	}

	protected virtual void RenderList(ListBlock list, RenderTreeBuilder builder)
	{
		if (list.Count == 0)
			return;

		var elementName = list.IsOrdered ? "ol" : "ul";
		var orderStart = list.OrderedStart.ParseOrDefault();

		builder.OpenElement(48, elementName);

		if (orderStart > 1)
		{
			builder.AddAttribute(49, "start", orderStart);
		}

		for (var i = 0; i < list.Count; i++)
		{
			var block = (ListItemBlock)list[i];

			for (var j = 0; j < block.Count; j++)
			{
				switch (block[j])
				{
					case ListBlock x:
					{
						RenderList(x, builder);
						break;
					}
					case ParagraphBlock x:
					{
						builder.OpenElement(50, "li");
						RenderParagraphBlock(x, builder);
						builder.CloseElement();
						break;
					}
					case FencedCodeBlock code:
					{
						builder.OpenElement(51, "li");
						var text = code.CreateCodeBlockText();

						builder.OpenComponent<MudCodeHighlight>(52);
						builder.AddAttribute(53, nameof(MudCodeHighlight.Text), text);
						builder.AddAttribute(54, nameof(MudCodeHighlight.Language), code.Info ?? string.Empty);
						builder.AddAttribute(55, nameof(MudCodeHighlight.Theme), CodeBlockTheme);
						builder.CloseComponent();
						builder.CloseElement();
						break;
					}
					default:
					{
						OnRenderListDefault(block[j], builder);
						break;
					}
				}
			}
		}

		builder.CloseElement();
	}

	/// <summary>
	/// Renders a markdown block which is not covered by the switch-case block in <see cref="RenderList"/> 
	/// </summary>
	protected virtual void OnRenderListDefault(Markdig.Syntax.Block block, RenderTreeBuilder builder)
	{
	}

	protected virtual void RenderDetailsHtml(in RenderTreeBuilder builder, in string header, in string content)
	{
		var headerHtml = Markdown.Parse(header, Pipeline);
		var contentHtml = Markdown.Parse(content);

		builder.OpenComponent<MudMarkdownDetails>(56);
		builder.AddAttribute(57, nameof(MudMarkdownDetails.TitleContent), (RenderFragment)(titleBuilder => RenderMarkdown(headerHtml, titleBuilder)));
		builder.AddAttribute(58, nameof(MudMarkdownDetails.ChildContent), (RenderFragment)(contentBuilder => RenderMarkdown(contentHtml, contentBuilder)));
		builder.CloseComponent();
	}

	protected virtual void RenderHtml(in RenderTreeBuilder builder, in StringLineGroup lines)
	{
		var markupString = new MarkupString(lines.ToString());
		builder.AddContent(59, markupString);
	}

	private async void NavigationManagerOnLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		var idFragment = new Uri(e.Location, UriKind.Absolute).Fragment;
		if (!idFragment.StartsWith('#') || idFragment.Length < 2)
			return;

		idFragment = idFragment[1..];

		await JsRuntime.InvokeVoidAsync("scrollToElementId", idFragment)
			.ConfigureAwait(false);
	}

	private void OnCodeBlockThemeChanged(object? sender, CodeBlockTheme e) =>
		CodeBlockTheme = e;

	private MarkdownPipeline GetMarkdownPipeLine()
	{
		if (MarkdownPipeline != null)
			return MarkdownPipeline;

		return Pipeline ??= new MarkdownPipelineBuilder()
			.UseAdvancedExtensions()
			.Build();
	}
}
