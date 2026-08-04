' Interop/LiteHtmlDocumentHandle.vb - safe, IDisposable wrapper around a native litehtml
' document handle. This is the only class outside LiteHtmlNative.vb that should ever
' P/Invoke into the shim.
Imports System
Imports System.IO
Imports System.Runtime.InteropServices

Namespace Interop

    ''' <summary>
    ''' Wraps one native litehtml document (an HTML page laid out and ready to paint) with
    ''' proper lifecycle management. Create one per page load - it is not reusable across
    ''' navigations, since litehtml documents are immutable once created
    ''' </summary>
    Public Class LiteHtmlDocumentHandle
        Implements IDisposable

        Private pHandle As IntPtr = IntPtr.Zero
        Private pDisposed As Boolean = False

        ''' <summary>
        ''' True if this handle successfully wraps a real native document (i.e.
        ''' CreateDocument didn't fail)
        ''' </summary>
        Public ReadOnly Property IsValid As Boolean
            Get
                Return pHandle <> IntPtr.Zero
            End Get
        End Property

        ''' <summary>
        ''' Creates and lays out a new document from an HTML string. Check IsValid
        ''' afterward - construction never throws for a native-side failure, only for
        ''' IsAvailable already being False when called (a programmer error - callers must
        ''' check IsAvailable first)
        ''' </summary>
        ''' <param name="vHtmlUtf8">The page's HTML source</param>
        ''' <param name="vBaseUrl">Used to resolve relative links/images/stylesheets; may be empty</param>
        Public Sub New(vHtmlUtf8 As String, vBaseUrl As String)
            Try
                If Not IsAvailable Then
                    Throw New InvalidOperationException("LiteHtmlDocumentHandle.New called while the native shim is unavailable - callers must check LiteHtmlDocumentHandle.IsAvailable first")
                End If
                pHandle = LiteHtmlNative.CreateDocument(vHtmlUtf8, If(vBaseUrl, ""))
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.New error: {ex.Message}")
                pHandle = IntPtr.Zero
            End Try
        End Sub

        ''' <summary>
        ''' Registers a pre-fetched resource (image or stylesheet) this document may
        ''' reference by URL - call for every resource discovered/downloaded before the
        ''' render call that would need it
        ''' </summary>
        ''' <param name="vUrl">The resource's resolved absolute URL</param>
        ''' <param name="vBytes">The resource's raw bytes</param>
        Public Sub AddResource(vUrl As String, vBytes As Byte())
            Try
                If Not IsValid OrElse vBytes Is Nothing Then Return
                LiteHtmlNative.AddResource(pHandle, vUrl, vBytes, vBytes.Length)
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.AddResource error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the layout viewport width in CSS pixels and re-runs layout - call before
        ''' the first Render, and again whenever the host widget is resized
        ''' </summary>
        ''' <param name="vWidthPx">Viewport width in CSS pixels</param>
        Public Sub SetViewportWidth(vWidthPx As Integer)
            Try
                If Not IsValid Then Return
                LiteHtmlNative.SetViewportWidth(pHandle, vWidthPx)
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.SetViewportWidth error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the document's laid-out content height in pixels - valid only after at
        ''' least one SetViewportWidth call
        ''' </summary>
        Public ReadOnly Property ContentHeight As Integer
            Get
                Try
                    If Not IsValid Then Return 0
                    Return LiteHtmlNative.GetContentHeight(pHandle)
                Catch ex As Exception
                    Console.WriteLine($"LiteHtmlDocumentHandle.ContentHeight error: {ex.Message}")
                    Return 0
                End Try
            End Get
        End Property

        ''' <summary>
        ''' Paints the document into an existing Cairo context
        ''' </summary>
        ''' <param name="vCairoContext">The Cairo.Context to paint into - its Handle
        ''' property is the raw cairo_t* the native side expects</param>
        ''' <param name="vClipWidth">Clip rectangle width</param>
        ''' <param name="vClipHeight">Clip rectangle height</param>
        Public Sub Render(vCairoContext As Cairo.Context, vClipWidth As Integer, vClipHeight As Integer)
            Try
                If Not IsValid OrElse vCairoContext Is Nothing Then Return
                LiteHtmlNative.Render(pHandle, vCairoContext.Handle, vClipWidth, vClipHeight)
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.Render error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Reports a click at document-space (vX,vY)
        ''' </summary>
        ''' <param name="vX">Click X in document-space pixels</param>
        ''' <param name="vY">Click Y in document-space pixels</param>
        ''' <returns>The clicked link's absolute URL, or Nothing if the click didn't land on a link</returns>
        Public Function HandleClick(vX As Integer, vY As Integer) As String
            Dim lNativeStr As IntPtr = IntPtr.Zero
            Try
                If Not IsValid Then Return Nothing
                lNativeStr = LiteHtmlNative.HandleClick(pHandle, vX, vY)
                If lNativeStr = IntPtr.Zero Then Return Nothing
                Return Marshal.PtrToStringUTF8(lNativeStr)
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.HandleClick error: {ex.Message}")
                Return Nothing
            Finally
                If lNativeStr <> IntPtr.Zero Then LiteHtmlNative.FreeString(lNativeStr)
            End Try
        End Function

        ''' <summary>
        ''' Reports pointer movement at document-space (vX,vY)
        ''' </summary>
        ''' <param name="vX">Pointer X in document-space pixels</param>
        ''' <param name="vY">Pointer Y in document-space pixels</param>
        ''' <returns>True if the hovered element changed in a way that likely needs a redraw</returns>
        Public Function HandleMouseMove(vX As Integer, vY As Integer) As Boolean
            Try
                If Not IsValid Then Return False
                Return LiteHtmlNative.HandleMouseMove(pHandle, vX, vY) <> 0
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.HandleMouseMove error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Releases the native document handle
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            If pDisposed Then Return
            Try
                If pHandle <> IntPtr.Zero Then
                    LiteHtmlNative.DestroyDocument(pHandle)
                    pHandle = IntPtr.Zero
                End If
            Catch ex As Exception
                Console.WriteLine($"LiteHtmlDocumentHandle.Dispose error: {ex.Message}")
            Finally
                pDisposed = True
                GC.SuppressFinalize(Me)
            End Try
        End Sub

        Protected Overrides Sub Finalize()
            Dispose()
        End Sub

        ' ===== Availability check (shared across all instances) =====

        Private Shared pAvailabilityChecked As Boolean = False
        Private Shared pIsAvailable As Boolean = False

        ''' <summary>
        ''' Gets whether the native litehtml shim is present in this app's own output
        ''' directory. Checked once and cached - if False, callers should fall back to
        ''' whatever non-litehtml behavior they had before (e.g. HelpBrowser's external-
        ''' browser fallback) rather than attempting any P/Invoke call at all, since a
        ''' missing native library throws DllNotFoundException on first use otherwise.
        ''' Mirrors the WebKitGTK lesson: a native dependency going missing must degrade
        ''' gracefully, never crash
        ''' </summary>
        Public Shared ReadOnly Property IsAvailable As Boolean
            Get
                If Not pAvailabilityChecked Then
                    Try
                        Dim lLibraryFileName As String = If(
                            OperatingSystem.IsWindows(), "litehtml_shim.dll", "liblitehtml_shim.so")
                        Dim lExpectedPath As String = System.IO.Path.Combine(AppContext.BaseDirectory, lLibraryFileName)
                        pIsAvailable = File.Exists(lExpectedPath)
                    Catch ex As Exception
                        Console.WriteLine($"LiteHtmlDocumentHandle.IsAvailable check error: {ex.Message}")
                        pIsAvailable = False
                    Finally
                        pAvailabilityChecked = True
                    End Try
                End If
                Return pIsAvailable
            End Get
        End Property

    End Class

End Namespace
