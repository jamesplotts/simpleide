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
Imports SimpleIDE.Interop
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' Renders a real HTML/CSS page (via the litehtml native shim) inside a self-managed
    ''' scrollable area - drop this widget into any layout and call LoadHtml/NavigateAsync
    ''' </summary>
    Public Class CustomDrawHtmlView
        Inherits Box

        ' ===== Fields =====

        Private pScrolled As ScrolledWindow
        Private pDrawingArea As DrawingArea
        Private pFetcher As New HtmlPageFetcher()
        Private pThemeManager As ThemeManager

        Private pDocHandle As LiteHtmlDocumentHandle
        Private pCurrentUrl As String = ""
        Private pIsLoading As Boolean = False
        Private pNavigationCts As CancellationTokenSource

        ' ===== Events =====

        ''' <summary>Raised when the user clicks a link - this widget takes no navigation
        ''' action itself, the consumer decides what to do with vUrl</summary>
        Public Event LinkClicked(vUrl As String)

        ''' <summary>Raised after a LoadHtml/NavigateAsync call finishes successfully</summary>
        Public Event LoadCompleted(vUrl As String)

        ''' <summary>Raised when NavigateAsync's fetch or render fails</summary>
        Public Event LoadFailed(vUrl As String, vError As String)

        ' ===== Properties =====

        ''' <summary>Gets the URL most recently loaded via NavigateAsync, or the base URL
        ''' passed to LoadHtml - empty if nothing has been loaded yet</summary>
        Public ReadOnly Property CurrentUrl As String
            Get
                Return pCurrentUrl
            End Get
        End Property

        ''' <summary>Gets whether a NavigateAsync fetch is currently in flight</summary>
        Public ReadOnly Property IsLoading As Boolean
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
        Public Sub LoadHtml(vHtml As String, Optional vBaseUrl As String = "")
            Try
                ReplaceDocument(vHtml, vBaseUrl, Nothing)
                pCurrentUrl = vBaseUrl
                RaiseEvent LoadCompleted(vBaseUrl)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.LoadHtml error: {ex.Message}")
                RaiseEvent LoadFailed(vBaseUrl, ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Fetches vUrl and every stylesheet/image it references (via HtmlPageFetcher),
        ''' then renders it. Cancels any fetch already in flight for this widget first.
        ''' Raises LoadCompleted/LoadFailed on the GTK main thread when done
        ''' </summary>
        ''' <param name="vUrl">The page URL to load</param>
        Public Async Function NavigateAsync(vUrl As String) As Task
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

                ReplaceDocument(lResult.Html, lResult.BaseUrl, lResult.Resources)
                pCurrentUrl = lResult.BaseUrl
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
        ''' rather than fighting the page's own CSS
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                ' No live re-render on theme change for v1 - the injected stylesheet only
                ' takes effect for documents loaded/reloaded after this call. Reasonable
                ' scope for a documentation viewer; revisit if it proves annoying in practice.
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ' ===== Document lifecycle =====

        Private Sub ReplaceDocument(vHtml As String, vBaseUrl As String, vResources As System.Collections.Generic.Dictionary(Of String, Byte()))
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

                Dim lWidth As Integer = Math.Max(pDrawingArea.AllocatedWidth, 1)
                pDocHandle.SetViewportWidth(lWidth)
                pDrawingArea.SetSizeRequest(lWidth, Math.Max(pDocHandle.ContentHeight, 1))
                pDrawingArea.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.ReplaceDocument error: {ex.Message}")
                Throw
            End Try
        End Sub

        ''' <summary>
        ''' Prepends a small `&lt;style&gt;` block derived from the current theme right
        ''' after vHtml's opening `&lt;head&gt;` (or at the very start if there's no
        ''' `&lt;head&gt;` tag - litehtml/gumbo parse loose HTML fine either way). Kept as
        ''' plain HTML injection rather than a native API change (a real litehtml "user
        ''' stylesheet" parameter) since normal CSS cascade/source-order already gives the
        ''' desired effect: these are just early, low-specificity defaults, so the page's
        ''' own linked stylesheets (added later, generally more specific) still win wherever
        ''' they actually style something
        ''' </summary>
        Private Function InjectThemeStylesheet(vHtml As String) As String
            Try
                If pThemeManager Is Nothing Then Return vHtml
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lTheme Is Nothing Then Return vHtml

                Dim lStyle As String = $"<style>body {{ background-color: {lTheme.BackgroundColor}; color: {lTheme.ForegroundColor}; }} a {{ color: {lTheme.AccentColor}; }}</style>"

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
            Catch ex As Exception
                Console.WriteLine($"CustomDrawHtmlView.OnDestroyed error: {ex.Message}")
            Finally
                MyBase.OnDestroyed()
            End Try
        End Sub

    End Class

End Namespace
