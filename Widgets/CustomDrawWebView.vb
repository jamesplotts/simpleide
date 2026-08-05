' Widgets/CustomDrawWebView.vb - the WebKitGTK-backed IEmbeddedBrowserView provider. Wraps
' a native WebKitWebView (full JS-capable rendering, unlike the litehtml-backed
' CustomDrawHtmlView) as a real Gtk.Widget child - WebKit paints and scrolls itself, so
' unlike CustomDrawHtmlView there is no manual Cairo/DrawingArea code here at all, just
' construction, signal wiring, and property forwarding.
'
' Linux-only in practice (WebKitGTK has no Windows build) - EmbeddedBrowserFactory checks
' IsAvailable before ever constructing this, and falls back to CustomDrawHtmlView (litehtml)
' if the native library isn't present, so nothing here needs its own platform guards.
Imports Gtk
Imports System
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Interop
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' Renders a real, fully JavaScript-capable web page via the system's WebKitGTK
    ''' (libwebkit2gtk-4.1) - the IEmbeddedBrowserView provider used when it's available,
    ''' in preference to the litehtml-backed CustomDrawHtmlView (see EmbeddedBrowserFactory)
    ''' </summary>
    Public Class CustomDrawWebView
        Inherits Box
        Implements IEmbeddedBrowserView

        ' ===== Fields =====

        Private pNativeWebViewPtr As IntPtr = IntPtr.Zero
        Private pWebViewWidget As Widget
        Private pCurrentUrl As String = ""
        Private pIsLoading As Boolean = False
        Private pThemeManager As ThemeManager
        Private pNavigationTcs As TaskCompletionSource(Of Boolean)

        ' Marshal.GetFunctionPointerForDelegate does not itself keep a delegate alive -
        ' these must stay GC-rooted for as long as the native signal connections exist
        ' (this widget's whole lifetime), or the CLR may collect them while WebKit still
        ' holds the raw function pointer, crashing on the next signal emission
        Private pDecidePolicyCallback As WebKitNative.DecidePolicyNativeCallback
        Private pContextMenuCallback As WebKitNative.ContextMenuNativeCallback

        ' ===== Events =====

        ''' <summary>Raised when the user clicks a link - this widget takes no navigation
        ''' action itself, the consumer decides what to do with vUrl</summary>
        Public Event LinkClicked(vUrl As String) Implements IEmbeddedBrowserView.LinkClicked

        ''' <summary>Raised after a LoadHtml/NavigateAsync call finishes successfully</summary>
        Public Event LoadCompleted(vUrl As String) Implements IEmbeddedBrowserView.LoadCompleted

        ''' <summary>Raised when NavigateAsync's load fails</summary>
        Public Event LoadFailed(vUrl As String, vError As String) Implements IEmbeddedBrowserView.LoadFailed

        ' ===== Properties =====

        ''' <summary>Gets the URL most recently loaded, or empty if nothing has loaded yet</summary>
        Public ReadOnly Property CurrentUrl As String Implements IEmbeddedBrowserView.CurrentUrl
            Get
                Return pCurrentUrl
            End Get
        End Property

        ''' <summary>Gets whether a navigation is currently in flight</summary>
        Public ReadOnly Property IsLoading As Boolean Implements IEmbeddedBrowserView.IsLoading
            Get
                Return pIsLoading
            End Get
        End Property

        ''' <summary>Gets whether libwebkit2gtk-4.1 is available on this system - consumers
        ''' (EmbeddedBrowserFactory) should check this before constructing this widget and
        ''' fall back to CustomDrawHtmlView if False</summary>
        Public Shared ReadOnly Property IsAvailable As Boolean
            Get
                If Not pAvailabilityChecked Then
                    Try
                        ' Deliberately never NativeLibrary.Free() the handle on success -
                        ' confirmed live this session that dlclose-ing libwebkit2gtk-4.1
                        ' immediately after this probe crashes the process (a hard segfault
                        ' inside dlopen() itself on the very next native library load,
                        ' e.g. GTK's own Application.Init()). WebKitGTK, like most complex
                        ' libraries with their own GType registrations and static state,
                        ' isn't designed to be unloaded once loaded - leaving the mapping in
                        ' place for the process's lifetime is the same thing a real WebView
                        ' construction would do anyway, so this "leak" is intentional and safe
                        Dim lHandle As IntPtr
                        pIsAvailable = NativeLibrary.TryLoad(WebKitNative.cWebKitLibrary, lHandle)
                    Catch ex As Exception
                        Console.WriteLine($"CustomDrawWebView.IsAvailable check error: {ex.Message}")
                        pIsAvailable = False
                    Finally
                        pAvailabilityChecked = True
                    End Try
                End If
                Return pIsAvailable
            End Get
        End Property

        Private Shared pAvailabilityChecked As Boolean = False
        Private Shared pIsAvailable As Boolean = False

        ' ===== Construction =====

        ''' <summary>
        ''' Creates a new, empty WebKit view - call NavigateAsync/LoadHtml to show content.
        ''' Throws if the native library isn't available - callers must check
        ''' CustomDrawWebView.IsAvailable first
        ''' </summary>
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            Try
                If Not IsAvailable Then
                    Throw New InvalidOperationException("CustomDrawWebView.New called while the WebKitGTK native library is unavailable - callers must check CustomDrawWebView.IsAvailable first")
                End If
                BuildUI()
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.New error: {ex.Message}")
                Throw
            End Try
        End Sub

        Private Sub BuildUI()
            pNativeWebViewPtr = WebKitNative.CreateWebView()
            If pNativeWebViewPtr = IntPtr.Zero Then
                Throw New InvalidOperationException("webkit_web_view_new() returned a null pointer")
            End If

            pWebViewWidget = TryCast(GLib.Object.GetObject(pNativeWebViewPtr), Widget)
            If pWebViewWidget Is Nothing Then
                Throw New InvalidOperationException("Failed to wrap the native WebKitWebView as a Gtk.Widget")
            End If

            PackStart(pWebViewWidget, True, True, 0)

            Dim lGlibObj As GLib.Object = DirectCast(pWebViewWidget, GLib.Object)

            ' load-changed is void-returning - GLib.Object.AddSignalHandler's generic
            ' marshaling handles this fine (proven live)
            Dim lLoadChangedHandler As LoadChangedSignalHandler = AddressOf OnLoadChanged
            lGlibObj.AddSignalHandler("load-changed", lLoadChangedHandler, GetType(GLib.SignalArgs))

            Dim lLoadFailedHandler As LoadFailedSignalHandler = AddressOf OnLoadFailedNative
            Dim lLoadFailedFuncPtr As IntPtr = Marshal.GetFunctionPointerForDelegate(lLoadFailedHandler)
            pLoadFailedCallback = lLoadFailedHandler
            WebKitNative.SignalConnectData(pNativeWebViewPtr, "load-failed", lLoadFailedFuncPtr, IntPtr.Zero, IntPtr.Zero, 0)

            ' decide-policy and context-menu are gboolean-returning - confirmed live this
            ' session that GLib.Object.AddSignalHandler does not reliably fire these for a
            ' manually-wrapped foreign GObject type, so these use raw g_signal_connect_data
            ' instead (see Interop/WebKitNative.vb's header comment)
            pDecidePolicyCallback = AddressOf OnDecidePolicyNative
            Dim lDecideFuncPtr As IntPtr = Marshal.GetFunctionPointerForDelegate(pDecidePolicyCallback)
            WebKitNative.SignalConnectData(pNativeWebViewPtr, "decide-policy", lDecideFuncPtr, IntPtr.Zero, IntPtr.Zero, 0)

            pContextMenuCallback = AddressOf OnContextMenuNative
            Dim lContextFuncPtr As IntPtr = Marshal.GetFunctionPointerForDelegate(pContextMenuCallback)
            WebKitNative.SignalConnectData(pNativeWebViewPtr, "context-menu", lContextFuncPtr, IntPtr.Zero, IntPtr.Zero, 0)

            ShowAll()
        End Sub

        ' load-failed is also gboolean-returning - same reasoning as decide-policy/context-menu.
        ' UnmanagedFunctionPointer(Cdecl) is required here - without it this crashed with a
        ' segfault (confirmed live), unlike WebKitNative's two native callback delegates
        ' which already had it
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function LoadFailedSignalHandler(vWebView As IntPtr, vLoadEvent As Integer, vFailingUri As IntPtr, vError As IntPtr, vUserData As IntPtr) As Integer
        Private pLoadFailedCallback As LoadFailedSignalHandler

        Private Delegate Sub LoadChangedSignalHandler(o As Object, args As GLib.SignalArgs)

        ' ===== Public API =====

        ''' <summary>
        ''' Renders a raw HTML string directly via WebKit's own base-URI-aware loader - no
        ''' pre-fetch of resources needed (unlike CustomDrawHtmlView's LoadHtml), WebKit
        ''' fetches its own referenced stylesheets/images/scripts as it parses
        ''' </summary>
        ''' <param name="vHtml">The HTML to render</param>
        ''' <param name="vBaseUrl">Used to resolve any relative links/resources; may be empty</param>
        Public Sub LoadHtml(vHtml As String, Optional vBaseUrl As String = "") Implements IEmbeddedBrowserView.LoadHtml
            Try
                ' webkit_web_view_load_html isn't declared in WebKitNative yet - MVP scope
                ' is real-page NavigateAsync (the actual ask); route through a data: URI so
                ' this still works rather than being a hard NotImplementedException
                Dim lDataUri As String = "data:text/html;charset=utf-8," & Uri.EscapeDataString(vHtml)
                WebKitNative.LoadUri(pNativeWebViewPtr, lDataUri)
                pCurrentUrl = vBaseUrl
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.LoadHtml error: {ex.Message}")
                RaiseEvent LoadFailed(vBaseUrl, ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Loads vUrl - WebKit does its own fetching/parsing/JS execution. Raises
        ''' LoadCompleted/LoadFailed when done
        ''' </summary>
        ''' <param name="vUrl">The page URL to load</param>
        Public Function NavigateAsync(vUrl As String) As Task Implements IEmbeddedBrowserView.NavigateAsync
            Try
                pIsLoading = True
                pNavigationTcs = New TaskCompletionSource(Of Boolean)()
                WebKitNative.LoadUri(pNativeWebViewPtr, vUrl)
                Return pNavigationTcs.Task
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.NavigateAsync error: {ex.Message}")
                pIsLoading = False
                RaiseEvent LoadFailed(vUrl, ex.Message)
                Return Task.CompletedTask
            End Try
        End Function

        ''' <summary>
        ''' Stores the shared ThemeManager - not currently used to inject a stylesheet
        ''' (unlike CustomDrawHtmlView's litehtml provider, which controls the raw HTML;
        ''' theme-color injection into arbitrary WebKit-rendered pages would need
        ''' WebKitUserContentManager, deferred - see plan's Phase 5 hardening)
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager) Implements IEmbeddedBrowserView.SetThemeManager
            pThemeManager = vThemeManager
        End Sub

        ' ===== Signal handlers =====

        Private Sub OnLoadChanged(o As Object, args As GLib.SignalArgs)
            Try
                Dim lLoadEvent As Integer = CInt(args.Args(0))
                If lLoadEvent = 3 Then ' WEBKIT_LOAD_FINISHED
                    pIsLoading = False
                    Dim lUriPtr As IntPtr = WebKitNative.GetUri(pNativeWebViewPtr)
                    pCurrentUrl = If(lUriPtr <> IntPtr.Zero, Marshal.PtrToStringUTF8(lUriPtr), pCurrentUrl)
                    RaiseEvent LoadCompleted(pCurrentUrl)
                    pNavigationTcs?.TrySetResult(True)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.OnLoadChanged error: {ex.Message}")
            End Try
        End Sub

        Private Function OnLoadFailedNative(vWebView As IntPtr, vLoadEvent As Integer, vFailingUri As IntPtr, vError As IntPtr, vUserData As IntPtr) As Integer
            Try
                pIsLoading = False
                Dim lFailingUri As String = If(vFailingUri <> IntPtr.Zero, Marshal.PtrToStringUTF8(vFailingUri), pCurrentUrl)
                RaiseEvent LoadFailed(lFailingUri, $"Failed to load {lFailingUri}")
                pNavigationTcs?.TrySetResult(False)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.OnLoadFailedNative error: {ex.Message}")
            End Try
            Return 0 ' FALSE - propagate to WebKit's own default failure handling too
        End Function

        ''' <summary>
        ''' Intercepts link clicks (WEBKIT_NAVIGATION_TYPE_LINK_CLICKED = 0) and raises
        ''' LinkClicked instead of letting WebKit navigate itself, so this widget stays a
        ''' pure IEmbeddedBrowserView provider with no navigation policy of its own - matches
        ''' CustomDrawHtmlView's existing design (see that class's own header comment) and
        ''' is what lets HelpBrowser's back/forward stack work identically across both
        ''' providers. Every other navigation type (the initial NavigateAsync call itself
        ''' comes through as WEBKIT_NAVIGATION_TYPE_OTHER, confirmed live) is left alone by
        ''' returning FALSE, which lets WebKit's own default handling (proceed) run
        ''' </summary>
        Private Function OnDecidePolicyNative(vWebView As IntPtr, vDecision As IntPtr, vDecisionType As Integer, vUserData As IntPtr) As Integer
            Try
                If vDecisionType <> 0 Then Return 0 ' only WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION

                Dim lAction As IntPtr = WebKitNative.NavigationPolicyDecisionGetNavigationAction(vDecision)
                If lAction = IntPtr.Zero Then Return 0

                Dim lNavType As Integer = WebKitNative.NavigationActionGetNavigationType(lAction)
                If lNavType <> 0 Then Return 0 ' 0 = WEBKIT_NAVIGATION_TYPE_LINK_CLICKED

                Dim lRequest As IntPtr = WebKitNative.NavigationActionGetRequest(lAction)
                If lRequest = IntPtr.Zero Then Return 0

                Dim lUriPtr As IntPtr = WebKitNative.UriRequestGetUri(lRequest)
                If lUriPtr = IntPtr.Zero Then Return 0

                Dim lUri As String = Marshal.PtrToStringUTF8(lUriPtr)
                WebKitNative.PolicyDecisionIgnore(vDecision)
                RaiseEvent LinkClicked(lUri)
                Return 1 ' TRUE - we handled it
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.OnDecidePolicyNative error: {ex.Message}")
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Right-click support (explicit ask): if the click landed on a hyperlink, shows a
        ''' single-item "Open Link in Browser" menu instead of WebKit's own default context
        ''' menu, using the same Process.Start pattern HelpBrowser.OpenExternalUrl already
        ''' uses. Right-clicking non-link content falls through to WebKit's default menu -
        ''' only the explicit hyperlink case was asked for
        ''' </summary>
        Private Function OnContextMenuNative(vWebView As IntPtr, vContextMenu As IntPtr, vEvent As IntPtr, vHitTestResult As IntPtr, vUserData As IntPtr) As Integer
            Try
                If WebKitNative.HitTestResultContextIsLink(vHitTestResult) = 0 Then Return 0

                Dim lUriPtr As IntPtr = WebKitNative.HitTestResultGetLinkUri(vHitTestResult)
                If lUriPtr = IntPtr.Zero Then Return 0
                Dim lUri As String = Marshal.PtrToStringUTF8(lUriPtr)

                Dim lMenu As New Menu()
                Dim lItem As New MenuItem("Open Link in Browser")
                AddHandler lItem.Activated, Sub(vSender As Object, vArgs As EventArgs)
                    Try
                        Process.Start(New ProcessStartInfo With {
                            .FileName = lUri,
                            .UseShellExecute = True
                        })
                    Catch lEx As Exception
                        Console.WriteLine($"CustomDrawWebView: failed to open {lUri}: {lEx.Message}")
                    End Try
                End Sub
                lMenu.Append(lItem)
                lMenu.ShowAll()
                lMenu.Popup()

                Return 1 ' TRUE - suppress WebKit's own default context menu
            Catch ex As Exception
                Console.WriteLine($"CustomDrawWebView.OnContextMenuNative error: {ex.Message}")
                Return 0
            End Try
        End Function

    End Class

End Namespace
