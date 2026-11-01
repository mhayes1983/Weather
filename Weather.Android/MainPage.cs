namespace Weather.Mobile;

public sealed class MainPage : ContentPage
{
    private readonly HybridWebView _webView = new() { HybridRoot = "wwwroot", DefaultFile = "index.html" };

    public MainPage(AppBridge bridge)
    {
        _webView.SetInvokeJavaScriptTarget(bridge);
        Content = _webView;

        // Android draws apps behind the system bars, where taps would go to the status bar instead.
        SafeAreaEdges = SafeAreaEdges.All;

        // The strip behind those bars matches --bg in index.css.
        this.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#16171d"));
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () =>
        {
            // The client closes its menu or goes back a page, and says so.
            var handled = await _webView.EvaluateJavaScriptAsync("window.__appBack?.() ?? false");
            if (handled != "true")
            {
                Platform.CurrentActivity?.MoveTaskToBack(true);
            }
        });

        return true;
    }
}
