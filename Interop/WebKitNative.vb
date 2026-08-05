' Interop/WebKitNative.vb - P/Invoke declarations for the WebKitGTK rendering backend
' (native/shim/-free - libwebkit2gtk-4.1/libjavascriptcoregtk-4.1 are system libraries with
' their own already-public, already-stable C API, so there's no custom native shim to build
' here, unlike litehtml's Interop/LiteHtmlNative.vb).
'
' Hand-rolled rather than using the WebkitGtkSharp NuGet package (still referenced in
' SimpleIDE.vbproj for now, unused): confirmed this session that binding is hardcoded to
' the removed libwebkit2gtk-4.0.so.37 SONAME (throws DllNotFoundException on any system
' shipping only 4.1, which is what Debian 13 and newer actually ship), and its
' WebKit.JavascriptResult type is an empty stub with no accessible native handle via
' reflection - a dead end even where it does load.
'
' Two P/Invoke styles are mixed deliberately:
'  - Plain <DllImport> for ordinary functions (webkit_web_view_new, _load_uri, etc.) -
'    GLibrary/GtkSharp's own marshaling handles these fine.
'  - Raw g_signal_connect_data (libgobject-2.0) for the two boolean-returning WebKit
'    signals this backend needs (decide-policy, context-menu) - confirmed live this
'    session that GLib.Object.AddSignalHandler's generic marshaling does not reliably
'    handle a gboolean return value on a manually-wrapped foreign GObject type (WebKitWebView
'    isn't a GtkSharp-registered type): the handler simply never fired via that path. A
'    native C-ABI callback connected directly via g_signal_connect_data works correctly and
'    was verified against a real page load - see Widgets/CustomDrawWebView.vb.
'    Void-returning signals (load-changed) DO work fine via the simpler
'    GLib.Object.AddSignalHandler(name, delegate, GetType(GLib.SignalArgs)) pattern -
'    CustomDrawWebView uses whichever mechanism actually works for each signal.
Imports System
Imports System.Runtime.InteropServices

Namespace Interop

    ''' <summary>
    ''' Raw P/Invoke declarations against libwebkit2gtk-4.1/libjavascriptcoregtk-4.1/
    ''' libgobject-2.0. Not meant to be called directly outside this namespace - see
    ''' Widgets/CustomDrawWebView.vb for the safe, widget-shaped surface.
    ''' </summary>
    Friend Module WebKitNative

        Friend Const cWebKitLibrary As String = "libwebkit2gtk-4.1.so.0"
        Friend Const cGObjectLibrary As String = "libgobject-2.0.so.0"

        ' ===== WebKitWebView =====

        ''' <summary>
        ''' Creates a new native WebKitWebView. Returns a GtkWidget* - wrap with
        ''' GLib.Object.GetObject to get a usable Gtk.Widget
        ''' </summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_new")>
        Friend Function CreateWebView() As IntPtr
        End Function

        ''' <summary>Loads vUri into vWebView</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_load_uri")>
        Friend Sub LoadUri(vWebView As IntPtr, <MarshalAs(UnmanagedType.LPUTF8Str)> vUri As String)
        End Sub

        ''' <summary>Gets the current page's title, or IntPtr.Zero if none</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_get_title")>
        Friend Function GetTitle(vWebView As IntPtr) As IntPtr
        End Function

        ''' <summary>Gets the current page's URI, or IntPtr.Zero if none</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_get_uri")>
        Friend Function GetUri(vWebView As IntPtr) As IntPtr
        End Function

        ''' <summary>Reloads the current page</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_reload")>
        Friend Sub Reload(vWebView As IntPtr)
        End Sub

        ''' <summary>Navigates back in history</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_go_back")>
        Friend Sub GoBack(vWebView As IntPtr)
        End Sub

        ''' <summary>Gets whether there is a page to go back to</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_can_go_back")>
        Friend Function CanGoBack(vWebView As IntPtr) As Integer
        End Function

        ''' <summary>Navigates forward in history</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_go_forward")>
        Friend Sub GoForward(vWebView As IntPtr)
        End Sub

        ''' <summary>Gets whether there is a page to go forward to</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_web_view_can_go_forward")>
        Friend Function CanGoForward(vWebView As IntPtr) As Integer
        End Function

        ' ===== Navigation policy (decide-policy signal payload) =====

        ''' <summary>Gets the WebKitNavigationAction* from a NAVIGATION_ACTION/NEW_WINDOW_ACTION policy decision</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_navigation_policy_decision_get_navigation_action")>
        Friend Function NavigationPolicyDecisionGetNavigationAction(vDecision As IntPtr) As IntPtr
        End Function

        ''' <summary>Gets the WebKitNavigationType (0=link clicked, see WebKitNavigationAction.h) of a navigation action</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_navigation_action_get_navigation_type")>
        Friend Function NavigationActionGetNavigationType(vAction As IntPtr) As Integer
        End Function

        ''' <summary>Gets the WebKitURIRequest* a navigation action is requesting</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_navigation_action_get_request")>
        Friend Function NavigationActionGetRequest(vAction As IntPtr) As IntPtr
        End Function

        ''' <summary>Gets the URI string from a WebKitURIRequest*</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_uri_request_get_uri")>
        Friend Function UriRequestGetUri(vRequest As IntPtr) As IntPtr
        End Function

        ''' <summary>Rejects a policy decision (e.g. a link-clicked navigation we're intercepting)</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_policy_decision_ignore")>
        Friend Sub PolicyDecisionIgnore(vDecision As IntPtr)
        End Sub

        ' ===== Context menu (right-click) support =====

        ''' <summary>Gets whether a hit-test result's context includes a hyperlink</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_hit_test_result_context_is_link")>
        Friend Function HitTestResultContextIsLink(vHitTestResult As IntPtr) As Integer
        End Function

        ''' <summary>Gets the hyperlink URI from a hit-test result, or IntPtr.Zero if none</summary>
        <DllImport(cWebKitLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="webkit_hit_test_result_get_link_uri")>
        Friend Function HitTestResultGetLinkUri(vHitTestResult As IntPtr) As IntPtr
        End Function

        ' ===== Raw GObject signal connection (for the two gboolean-returning signals) =====

        ''' <summary>Native C-ABI signature for decide-policy: gboolean(WebKitWebView*, WebKitPolicyDecision*, WebKitPolicyDecisionType, gpointer)</summary>
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Friend Delegate Function DecidePolicyNativeCallback(vWebView As IntPtr, vDecision As IntPtr, vDecisionType As Integer, vUserData As IntPtr) As Integer

        ''' <summary>Native C-ABI signature for context-menu: gboolean(WebKitWebView*, WebKitContextMenu*, GdkEvent*, WebKitHitTestResult*, gpointer)</summary>
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Friend Delegate Function ContextMenuNativeCallback(vWebView As IntPtr, vContextMenu As IntPtr, vEvent As IntPtr, vHitTestResult As IntPtr, vUserData As IntPtr) As Integer

        ''' <summary>
        ''' Connects a native callback to vInstance's vDetailedSignal - the low-level
        ''' GObject signal API, used instead of GLib.Object.AddSignalHandler because that
        ''' higher-level mechanism does not reliably marshal a gboolean return value for a
        ''' signal on a manually-wrapped foreign (non-GtkSharp-registered) GObject type;
        ''' confirmed live this session that a handler connected this way for decide-policy
        ''' never fired at all via AddSignalHandler, but fires correctly via this function
        ''' </summary>
        ''' <param name="vInstance">The GObject instance (e.g. a WebKitWebView*)</param>
        ''' <param name="vDetailedSignal">The signal name, e.g. "decide-policy"</param>
        ''' <param name="vHandler">A native function pointer (Marshal.GetFunctionPointerForDelegate on a delegate the caller keeps GC-rooted for the widget's lifetime)</param>
        ''' <param name="vData">User data passed to the callback; unused here (IntPtr.Zero)</param>
        ''' <param name="vDestroyData">Optional GClosureNotify to free vData; unused here (IntPtr.Zero)</param>
        ''' <param name="vConnectFlags">GConnectFlags; 0 for normal connection order</param>
        ''' <returns>The signal handler ID</returns>
        <DllImport(cGObjectLibrary, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="g_signal_connect_data")>
        Friend Function SignalConnectData(vInstance As IntPtr,
            <MarshalAs(UnmanagedType.LPUTF8Str)> vDetailedSignal As String,
            vHandler As IntPtr, vData As IntPtr, vDestroyData As IntPtr, vConnectFlags As Integer) As ULong
        End Function

    End Module

End Namespace
