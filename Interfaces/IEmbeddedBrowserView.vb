' Interfaces/IEmbeddedBrowserView.vb - contract for a swappable embedded-page rendering
' backend (see SimpleIDE.Widgets.vbproj's own header comment for the split rationale).
' Modeled directly on Widgets/CustomDrawHtmlView.vb's already-proven public shape, so a
' fork adding its own backend (WebView2, CEF, ...) just needs to implement this on a real
' Gtk.Widget - HelpBrowser (and anything else hosting one of these) only ever talks to
' this interface, never a concrete provider type, via EmbeddedBrowserFactory.
Imports System.Threading.Tasks
Imports SimpleIDE.Managers

Namespace Interfaces

    ''' <summary>
    ''' A widget that can render a real HTML page and report navigation outcomes -
    ''' implemented by every embedded-browser rendering backend (litehtml-backed
    ''' CustomDrawHtmlView, WebKitGTK-backed CustomDrawWebView, and any future provider a
    ''' fork adds) so callers can treat them interchangeably
    ''' </summary>
    Public Interface IEmbeddedBrowserView

        ''' <summary>Gets the URL most recently loaded, or empty if nothing has loaded yet</summary>
        ReadOnly Property CurrentUrl As String

        ''' <summary>Gets whether a navigation is currently in flight</summary>
        ReadOnly Property IsLoading As Boolean

        ''' <summary>
        ''' Wires the shared ThemeManager so the rendered page can pick up the app's colors
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Sub SetThemeManager(vThemeManager As ThemeManager)

        ''' <summary>
        ''' Renders a raw HTML string directly - no network fetch, no referenced
        ''' stylesheets/images resolved (use NavigateAsync for a real page with resources)
        ''' </summary>
        ''' <param name="vHtml">The HTML to render</param>
        ''' <param name="vBaseUrl">Used to resolve any relative links; may be empty</param>
        Sub LoadHtml(vHtml As String, Optional vBaseUrl As String = "")

        ''' <summary>
        ''' Fetches and renders vUrl. Raises LoadCompleted/LoadFailed when done
        ''' </summary>
        ''' <param name="vUrl">The page URL to load</param>
        Function NavigateAsync(vUrl As String) As Task

        ''' <summary>Raised when the user clicks a link - the provider takes no navigation
        ''' action itself, the consumer decides what to do with vUrl</summary>
        Event LinkClicked(vUrl As String)

        ''' <summary>Raised after a LoadHtml/NavigateAsync call finishes successfully</summary>
        Event LoadCompleted(vUrl As String)

        ''' <summary>Raised when NavigateAsync's fetch or render fails</summary>
        Event LoadFailed(vUrl As String, vError As String)

    End Interface

End Namespace
