namespace MudBlazor;

/// <summary>
/// For some reason MudExpansionPanels eternally tried to dispose all panels, therefore, RenderFragment was called infinitely<br/>
/// Created this component in order to bypass that weird behaviour
/// </summary>
internal sealed class MudMarkdownDetails : ComponentBase
{
	[Parameter]
	public RenderFragment? TitleContent { get; set; }

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	private bool IsExpanded { get; set; }

	private string IconClasses => new CssBuilder("mud-expand-panel-icon")
		.AddClass("mud-transform", IsExpanded)
		.Build();

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		builder.OpenElement(0, "div");
		builder.AddAttribute(1, "class", "mud-expand-panel mud-elevation-1 mud-expand-panel-border");

		BuildTitle(builder);
		BuildContent(builder);

		builder.CloseElement();
	}

	private void BuildTitle(RenderTreeBuilder builder)
	{
		builder.OpenElement(2, "div");
		builder.AddAttribute(3, "class", "mud-expand-panel-header mud-ripple");
		builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, OnHeaderClick));

		// Text
		builder.OpenElement(5, "div");
		builder.AddAttribute(6, "class", "mud-expand-panel-text");
		builder.AddContent(7, TitleContent);
		builder.CloseElement();

		// Collapse icon
		builder.OpenComponent<MudIcon>(8);
		builder.AddAttribute(9, nameof(MudIcon.Icon), Icons.Material.Filled.ExpandMore);
		builder.AddAttribute(10, "class", IconClasses);
		builder.CloseComponent();

		builder.CloseElement();
	}

	private void BuildContent(RenderTreeBuilder builder)
	{
		builder.OpenComponent<MudCollapse>(11);
		builder.AddAttribute(12, nameof(MudCollapse.Expanded), IsExpanded);

		builder.AddAttribute(13, nameof(MudCollapse.ChildContent), (RenderFragment)(contentBuilder =>
		{
			contentBuilder.OpenElement(14, "div");
			contentBuilder.AddAttribute(15, "class", "mud-expand-panel-content");
			contentBuilder.AddContent(16, ChildContent);
			contentBuilder.CloseElement();
		}));

		builder.CloseComponent();
	}

	private void OnHeaderClick()
	{
		IsExpanded = !IsExpanded;
	}
}
