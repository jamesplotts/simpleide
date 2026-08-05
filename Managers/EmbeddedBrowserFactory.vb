' Managers/EmbeddedBrowserFactory.vb - picks which IEmbeddedBrowserView provider to use.
' Lives in the exe (not SimpleIDE.Widgets.vbproj) because "which providers exist and in
' what preference order" is an app-level policy decision, not something the reusable
' widget library should hardcode - this is the exact seam a fork replaces to add its own
' backend (e.g. WebView2/CEF for Windows): implement IEmbeddedBrowserView in a new
' SimpleIDE.<Backend>.vbproj, add one more preference check below, nothing else in the
' app needs to change.
Imports Gtk
Imports System
Imports SimpleIDE.Widgets

Namespace Managers

    ''' <summary>
    ''' Creates the best available embedded-page rendering provider - prefers the
    ''' WebKitGTK backend (CustomDrawWebView: real, JS-capable rendering) when its native
    ''' library is present, falling back to the litehtml backend (CustomDrawHtmlView:
    ''' always available, ships its own bundled native shim, but no JavaScript) otherwise
    ''' </summary>
    Public Module EmbeddedBrowserFactory

        ''' <summary>
        ''' Creates a new provider widget
        ''' </summary>
        ''' <param name="vPreferWebKit">Whether to prefer the WebKitGTK backend when it's
        ''' available - False forces the litehtml fallback (e.g. a Preferences toggle for
        ''' troubleshooting), True still falls back automatically if WebKitGTK isn't
        ''' actually available regardless</param>
        ''' <returns>A Widget that also implements IEmbeddedBrowserView</returns>
        Public Function Create(Optional vPreferWebKit As Boolean = True) As Widget
            Try
                If vPreferWebKit AndAlso CustomDrawWebView.IsAvailable Then
                    Return New CustomDrawWebView()
                End If
            Catch ex As Exception
                Console.WriteLine($"EmbeddedBrowserFactory.Create: WebKitGTK provider unavailable, falling back to litehtml: {ex.Message}")
            End Try

            Return New CustomDrawHtmlView()
        End Function

    End Module

End Namespace
