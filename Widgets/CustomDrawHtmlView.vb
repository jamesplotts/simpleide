' Widgets/CustomDrawHtmlView.vb - a generic, self-contained, drop-in widget that renders
' real HTML/CSS pages via the litehtml native shim (Interop/LiteHtmlNative.vb) painted
' through Cairo - the same rendering stack every other CustomDraw* widget in this library
' already uses. Deliberately Help-agnostic (no PageEntry/HelpSection concepts) so it can be
' reused anywhere a widget needs to show a real web page, not just Widgets/HelpBrowser.vb.
'
' Consumers decide what a clicked link means (follow it, open externally, etc.) by
' handling LinkClicked - this widget has no navigation policy of its own beyond "load
' whatever URL/HTML you're given."
Imports Gtk
Imports Gdk
Imports System
Imports System.Threading
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Interop
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' Renders a real HTML/CSS page (via the litehtml native shim) inside a self-managed
    ''' scrollable area - drop this widget into any layout and call LoadHtml/NavigateAsync.
    ''' The litehtml-backed IEmbeddedBrowserView provider - see that interface for the
    ''' shared contract other rendering backends (e.g. CustomDrawWebView) also implement
    ''' </summary>
    Public Class CustomDrawHtmlView
        Inherits Box
        Implements IEmbeddedBrowserView

        ' ===== Fields =====

        Private pScrolled As ScrolledWindow
        Private pDrawingArea As DrawingArea
        Private pFetcher As New HtmlPageFetcher()
        Private pThemeManager As ThemeManager

        Private pDocHandle As LiteHtmlDocumentHandle
        Private pCurrentUrl As String = ""
        Private pIsLoading As Boolean = False
        Private pNavigationCts As CancellationTokenSource
        Private pLastFetchResult As HtmlPageFetchResult

        ' The raw (pre theme-injection) HTML/resources behind whatever's currently loaded,
        ' if anything - kept so OnThemeChanged can re-render with the new theme's colors
        ' without re-fetching over the network or losing already-downloaded images
        Private pCurrentHtml As String
        Private pCurrentResources As System.Collections.Generic.Dictionary(Of String, Byte())

        ' ===== Events =====

        ''' <summary>Raised when the user clicks a link - this widget takes no navigation
        ''' action itself, the consumer decides what to do with vUrl</summary>
        Public Event LinkClicked(vUrl As String) Implements IEmbeddedBrowserView.LinkClicked

        ''' <summary>Raised after a LoadHtml/NavigateAsync call finishes successfully</summary>
        Public Event LoadCompleted(vUrl As String) Implements IEmbeddedBrowserView.LoadCompleted

        ''' <summary>Raised when NavigateAsync's fetch or render fails</summary>
        Public Event LoadFailed(vUrl As String, vError As String) Implements IEmbeddedBrowserView.LoadFailed

        ' ===== Properties =====

        ''' <summary>Gets the URL most recently loaded via NavigateAsync, or the base URL
        ''' passed to LoadHtml - empty if nothing has been loaded yet</summary>
        Public ReadOnly Property CurrentUrl As String Implements IEmbeddedBrowserView.CurrentUrl
            Get
                Return pCurrentUrl
            End Get
        End Property

        ''' <summary>Gets whether a NavigateAsync fetch is currently in flight</summary>
        Public ReadOnly Property IsLoading As Boolean Implements IEmbeddedBrowserView.IsLoading
            Get
                Return pIsLoading
            End Get
        End Property

        ''' <summary>Gets the current document's laid-out content height in pixels, or 0
        ''' if nothing is loaded</summary>
        Public ReadOnly Property ContentHeight As Integer
            Get
                If pDocHandle Is Nothing OrElse Not pDocHandle.IsValid Then Return 0
                Return pDocHandle.ContentHeight
            End Get
        End Property

        ''' <summary>Gets the raw HTML/base URL/resources from the most recent successful
        ''' NavigateAsync fetch, or Nothing if none has succeeded yet - consumers that keep
        ''' their own history (e.g. HelpBrowser) can cache this to redisplay the same page
        ''' later via LoadCachedPage without hitting the network again</summary>
        Public ReadOnly Property LastFetchResult As HtmlPageFetchResult
            Get
                Return pLastFetchResult
            End Get
        End Property

        ''' <summary>Gets whether the native litehtml shim is available on this system -
        ''' consumers should check this before relying on this widget actually rendering
        ''' anything, and fall back to other behavior (e.g. opening links externally) if False</summary>
        Public Shared ReadOnly Property IsAvailable As Boolean
            Get
                Return LiteHtmlDocumentHandle.IsAvailable
            End Get
        End Property

        ' ===== Construction =====

        ''' <summary>
        ''' Creates a new, empty HTML view - call LoadHtml or NavigateAsync to show content
        ''' </summary>
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            Try
                BuildUI()
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.New error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI()
            pDrawingArea = New DrawingArea()
            pDrawingArea.CanFocus = True
            pDrawingArea.Events = pDrawingArea.Events Or
                EventMask.ButtonPressMask Or EventMask.PointerMotionMask Or EventMask.LeaveNotifyMask

            AddHandler pDrawingArea.Drawn, AddressOf OnDrawingAreaDrawn
            AddHandler pDrawingArea.SizeAllocated, AddressOf OnDrawingAreaSizeAllocated
            AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnDrawingAreaButtonPress
            AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnDrawingAreaMotionNotify

            pScrolled = New ScrolledWindow()
            pScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            pScrolled.Add(pDrawingArea) ' GTK auto-wraps a non-IScrollable child in an
                                         ' internal Viewport, which translates Drawn/
                                         ' event coordinates for us - lh_render and click/
                                         ' hover handling below never need to know about
                                         ' scroll position at all

            PackStart(pScrolled, True, True, 0)
            ShowAll()
        End Sub

        ' ===== Public API =====

        ''' <summary>
        ''' Renders a raw HTML string directly - no network fetch, no referenced
        ''' stylesheets/images resolved (use NavigateAsync for a real page with resources)
        ''' </summary>
        ''' <param name="vHtml">The HTML to render</param>
        ''' <param name="vBaseUrl">Used to resolve any relative links for LinkClicked; may be empty</param>
        Public Sub LoadHtml(vHtml As String, Optional vBaseUrl As String = "") Implements IEmbeddedBrowserView.LoadHtml
            Try
                ReplaceDocument(vHtml, vBaseUrl, Nothing, vResetScroll:=True)
                pCurrentUrl = vBaseUrl
                RaiseEvent LoadCompleted(vBaseUrl)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.LoadHtml error: {ex.Message}")
                RaiseEvent LoadFailed(vBaseUrl, ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Renders vHtml with vResources pre-registered, with no network fetch at all -
        ''' unlike LoadHtml, resources are applied so images/stylesheets from a prior
        ''' NavigateAsync fetch still work. Intended for consumers redisplaying a page they
        ''' already fetched once (e.g. HelpBrowser's Back/Forward), so revisiting a page
        ''' already shown neither re-hits the network nor can fail because of it
        ''' </summary>
        ''' <param name="vHtml">Previously-fetched HTML (e.g. from LastFetchResult)</param>
        ''' <param name="vBaseUrl">The page's base URL, for relative link resolution</param>
        ''' <param name="vResources">Previously-fetched resources (e.g. from LastFetchResult)</param>
        Public Sub LoadCachedPage(vHtml As String, vBaseUrl As String, vResources As System.Collections.Generic.Dictionary(Of String, Byte()))
            Try
                ReplaceDocument(vHtml, vBaseUrl, vResources, vResetScroll:=True)
                pCurrentUrl = vBaseUrl
                RaiseEvent LoadCompleted(vBaseUrl)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.LoadCachedPage error: {ex.Message}")
                RaiseEvent LoadFailed(vBaseUrl, ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Fetches vUrl and every stylesheet/image it references (via HtmlPageFetcher),
        ''' then renders it. Cancels any fetch already in flight for this widget first.
        ''' Raises LoadCompleted/LoadFailed on the GTK main thread when done
        ''' </summary>
        ''' <param name="vUrl">The page URL to load</param>
        Public Async Function NavigateAsync(vUrl As String) As Task Implements IEmbeddedBrowserView.NavigateAsync
            Try
                pNavigationCts?.Cancel()
                pNavigationCts = New CancellationTokenSource()
                Dim lCts As CancellationTokenSource = pNavigationCts

                pIsLoading = True
                Dim lResult As HtmlPageFetchResult = Await pFetcher.FetchPageAsync(vUrl)

                ' A newer NavigateAsync call (or Dispose) superseded this one while we were
                ' awaiting - drop the stale result rather than render over whatever's current
                If lCts.IsCancellationRequested Then Return

                pIsLoading = False

                If Not lResult.Success Then
                    RaiseEvent LoadFailed(vUrl, lResult.ErrorMessage)
                    Return
                End If

                ReplaceDocument(lResult.Html, lResult.BaseUrl, lResult.Resources, vResetScroll:=True)
                pCurrentUrl = lResult.BaseUrl
                pLastFetchResult = lResult
                RaiseEvent LoadCompleted(lResult.BaseUrl)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.NavigateAsync error: {ex.Message}")
                pIsLoading = False
                RaiseEvent LoadFailed(vUrl, ex.Message)
            End Try
        End Function

        ''' <summary>
        ''' Wires the shared ThemeManager - injected as a lightweight default stylesheet
        ''' (low CSS specificity, so any page's own styling still wins where it applies)
        ''' rather than fighting the page's own CSS. Subscribes to ThemeChanged so a
        ''' currently-loaded page picks up new theme colors immediately, re-rendered from
        ''' its already-downloaded HTML/resources with no network fetch involved
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager) Implements IEmbeddedBrowserView.SetThemeManager
            Try
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If

                pThemeManager = vThemeManager

                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If

                If pCurrentHtml IsNot Nothing Then
                    ReplaceDocument(pCurrentHtml, pCurrentUrl, pCurrentResources, vResetScroll:=False)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Re-renders whatever's currently loaded so it picks up the new theme's colors -
        ''' a no-op if nothing is loaded yet (SetThemeManager is called before any
        ''' navigation happens, so this fires harmlessly during that early call too)
        ''' </summary>
        ''' <param name="vTheme">The newly-applied theme (unused directly - InjectThemeStylesheet reads pThemeManager itself)</param>
        Private Sub OnThemeChanged(vTheme As EditorTheme)
            Try
                If pCurrentHtml Is Nothing Then Return
                ReplaceDocument(pCurrentHtml, pCurrentUrl, pCurrentResources, vResetScroll:=False)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnThemeChanged error: {ex.Message}")
            End Try
        End Sub

        ' ===== Document lifecycle =====

        ''' <summary>
        ''' Parses, lays out, and shows vHtml, replacing whatever was loaded before
        ''' </summary>
        ''' <param name="vHtml">Raw (pre theme-injection) HTML - cached to pCurrentHtml so a later theme change can re-render without a network fetch</param>
        ''' <param name="vBaseUrl">Used to resolve relative links/images/stylesheets</param>
        ''' <param name="vResources">Pre-fetched resources this document may reference by URL, or Nothing</param>
        ''' <param name="vResetScroll">True for a real navigation (start at the top); False when only re-rendering the same page for a new theme, so the reader's scroll position survives</param>
        Private Sub ReplaceDocument(vHtml As String, vBaseUrl As String, vResources As System.Collections.Generic.Dictionary(Of String, Byte()), vResetScroll As Boolean)
            Try
                pDocHandle?.Dispose()
                pDocHandle = Nothing

                If Not LiteHtmlDocumentHandle.IsAvailable Then
                    Throw New InvalidOperationException("litehtml native shim is not available on this system")
                End If

                Dim lHtmlWithTheme As String = InjectThemeStylesheet(vHtml)

                Dim lHandle As New LiteHtmlDocumentHandle(lHtmlWithTheme, vBaseUrl)
                If Not lHandle.IsValid Then
                    Throw New InvalidOperationException("failed to parse/layout document")
                End If

                If vResources IsNot Nothing Then
                    for each lEntry in vResources
                        lHandle.AddResource(lEntry.Key, lEntry.Value)
                    Next
                End If

                pDocHandle = lHandle
                pCurrentHtml = vHtml
                pCurrentResources = vResources

                Dim lWidth As Integer = Math.Max(pDrawingArea.AllocatedWidth, 1)
                pDocHandle.SetViewportWidth(lWidth)
                pDrawingArea.SetSizeRequest(lWidth, Math.Max(pDocHandle.ContentHeight, 1))

                If vResetScroll Then
                    ' Without this, a new document inherits whatever scroll position was
                    ' left over from whatever was shown here before - if that page was
                    ' scrolled down and this one is shorter (or just different), the
                    ' viewport can land past this document's real content and show nothing
                    ' at all, looking exactly like a blank/broken page even though it
                    ' rendered fine
                    pScrolled.Vadjustment.Value = pScrolled.Vadjustment.Lower
                    pScrolled.Hadjustment.Value = pScrolled.Hadjustment.Lower
                End If

                pDrawingArea.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.ReplaceDocument error: {ex.Message}")
                Throw
            End Try
        End Sub

        ''' <summary>
        ''' Prepends a small `&lt;style&gt;` block right after vHtml's opening `&lt;head&gt;`
        ''' (or at the very start if there's no `&lt;head&gt;` tag - litehtml/gumbo parse
        ''' loose HTML fine either way): always a UA-stylesheet gap-fill (see below), plus
        ''' the current theme's colors if a ThemeManager is set. Kept as plain HTML
        ''' injection rather than a native API change (a real litehtml "user stylesheet"
        ''' parameter) since normal CSS cascade/source-order already gives the desired
        ''' effect: these are just early, low-specificity defaults, so the page's own
        ''' linked stylesheets (added later, generally more specific) still win wherever
        ''' they actually style something
        ''' </summary>
        Private Function InjectThemeStylesheet(vHtml As String) As String
            Try
                ' litehtml's built-in default stylesheet has no rule for the standard HTML5
                ' `hidden` attribute (real browsers ship `[hidden] { display: none }` in
                ' their own UA stylesheet). Sites commonly render "no-JS"/"unsupported
                ' browser" fallback content with `hidden` set, then remove it via JS once
                ' feature-detection passes - since litehtml never runs that JS, the
                ' attribute alone must do the hiding, or that fallback content shows up on
                ' every single page regardless of what actually rendered fine
                Dim lStyle As String = "<style>[hidden] { display: none !important; }</style>"

                Dim lTheme As EditorTheme = If(pThemeManager IsNot Nothing, pThemeManager.GetCurrentThemeObject(), Nothing)
                If lTheme IsNot Nothing Then
                    lStyle &= $"<style>body {{ background-color: {lTheme.BackgroundColor}; color: {lTheme.ForegroundColor}; }} a {{ color: {lTheme.AccentColor}; }}</style>"
                End If

                Dim lHeadIndex As Integer = vHtml.IndexOf("<head>", StringComparison.OrdinalIgnoreCase)
                If lHeadIndex >= 0 Then
                    Dim lInsertAt As Integer = lHeadIndex + "<head>".Length
                    Return vHtml.Substring(0, lInsertAt) & lStyle & vHtml.Substring(lInsertAt)
                End If

                Return lStyle & vHtml

            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.InjectThemeStylesheet error: {ex.Message}")
                Return vHtml
            End Try
        End Function

        ' ===== Drawing / input =====

        Private Sub OnDrawingAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                If pDocHandle Is Nothing OrElse Not pDocHandle.IsValid Then Return
                pDocHandle.Render(vArgs.Cr, pDrawingArea.AllocatedWidth, pDrawingArea.AllocatedHeight)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDrawingAreaDrawn error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnDrawingAreaSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                If pDocHandle Is Nothing OrElse Not pDocHandle.IsValid Then Return
                Dim lWidth As Integer = vArgs.Allocation.Width
                If lWidth <= 0 Then Return

                pDocHandle.SetViewportWidth(lWidth)
                Dim lHeight As Integer = Math.Max(pDocHandle.ContentHeight, 1)
                ' SetSizeRequest here (not just QueueDraw) is what makes the outer
                ' ScrolledWindow's scrollbar range match the document's real height
                pDrawingArea.SetSizeRequest(lWidth, lHeight)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDrawingAreaSizeAllocated error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnDrawingAreaButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If pDocHandle Is Nothing OrElse Not pDocHandle.IsValid Then Return
                Dim lX As Integer = CInt(vArgs.Event.X)
                Dim lY As Integer = CInt(vArgs.Event.Y)
                Dim lUrl As String = pDocHandle.HandleClick(lX, lY)
                pDrawingArea.QueueDraw()
                If Not String.IsNullOrEmpty(lUrl) Then
                    RaiseEvent LinkClicked(lUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDrawingAreaButtonPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnDrawingAreaMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                If pDocHandle Is Nothing OrElse Not pDocHandle.IsValid Then Return
                Dim lX As Integer = CInt(vArgs.Event.X)
                Dim lY As Integer = CInt(vArgs.Event.Y)
                If pDocHandle.HandleMouseMove(lX, lY) Then
                    pDrawingArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDrawingAreaMotionNotify error: {ex.Message}")
            End Try
        End Sub

        ' ===== Cleanup =====

        Protected Overrides Sub OnDestroyed()
            Try
                pNavigationCts?.Cancel()
                pDocHandle?.Dispose()
                pDocHandle = Nothing
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDestroyed error: {ex.Message}")
            Finally
                MyBase.OnDestroyed()
            End Try
        End Sub

    End Class

End Namespace
