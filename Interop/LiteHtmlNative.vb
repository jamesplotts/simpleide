' Interop/LiteHtmlNative.vb - P/Invoke declarations for the litehtml native shim
' (native/shim/, built separately via native/build-native.sh - see SimpleIDE.vbproj's
' Exists()-conditioned <None> item that copies liblitehtml_shim.so into the build output).
'
' This is the first substantial P/Invoke surface in SimpleIDE - the only prior precedent
' is a single DllImport("libc") call in Program.vb for setenv. All strings crossing this
' boundary are explicitly UTF-8 (LPUTF8Str) rather than relying on platform-dependent
' "Ansi" marshaling, since the native side always expects/produces UTF-8 regardless of OS.
Imports System
Imports System.Runtime.InteropServices

Namespace Interop

    ''' <summary>
    ''' Raw P/Invoke declarations for native/shim/include/litehtml_shim.h. Not meant to be
    ''' called directly outside this namespace - see LiteHtmlDocumentHandle for the
    ''' IDisposable-wrapped, safe-to-use surface.
    ''' </summary>
    Friend Module LiteHtmlNative

        Private Const cLibraryName As String = "litehtml_shim"

        ''' <summary>
        ''' Creates a new litehtml document from an HTML string. Returns IntPtr.Zero on
        ''' failure (caught internally on the native side, never throws across the P/Invoke
        ''' boundary)
        ''' </summary>
        ''' <param name="vHtmlUtf8">The page's HTML source</param>
        ''' <param name="vBaseUrl">Used to resolve relative links/images/stylesheets; may be empty</param>
        ''' <returns>An opaque native document handle, or IntPtr.Zero on failure</returns>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_create_document")>
        Friend Function CreateDocument(
            <MarshalAs(UnmanagedType.LPUTF8Str)> vHtmlUtf8 As String,
            <MarshalAs(UnmanagedType.LPUTF8Str)> vBaseUrl As String) As IntPtr
        End Function

        ''' <summary>
        ''' Destroys a document handle created by CreateDocument. Safe to call with IntPtr.Zero
        ''' </summary>
        ''' <param name="vDoc">The document handle to destroy</param>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_destroy_document")>
        Friend Sub DestroyDocument(vDoc As IntPtr)
        End Sub

        ''' <summary>
        ''' Registers a pre-fetched resource (image or stylesheet) the document may
        ''' reference by URL - call for every resource discovered/downloaded BEFORE the
        ''' render call that would need it
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <param name="vUrl">The resource's resolved absolute URL, exactly as litehtml will look it up</param>
        ''' <param name="vBytes">The resource's raw bytes</param>
        ''' <param name="vLength">Number of valid bytes in vBytes</param>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_add_resource")>
        Friend Sub AddResource(
            vDoc As IntPtr,
            <MarshalAs(UnmanagedType.LPUTF8Str)> vUrl As String,
            vBytes As Byte(),
            vLength As Integer)
        End Sub

        ''' <summary>
        ''' Sets the layout viewport width in CSS pixels and re-runs layout - call before
        ''' the first Render, and again whenever the host widget is resized
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <param name="vWidthPx">Viewport width in CSS pixels</param>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_set_viewport_width")>
        Friend Sub SetViewportWidth(vDoc As IntPtr, vWidthPx As Integer)
        End Sub

        ''' <summary>
        ''' Gets the document's laid-out content height in pixels - valid only after at
        ''' least one SetViewportWidth call
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <returns>Content height in pixels</returns>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_get_content_height")>
        Friend Function GetContentHeight(vDoc As IntPtr) As Integer
        End Function

        ''' <summary>
        ''' Paints the document into an existing Cairo context
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <param name="vCairoCtx">The raw cairo_t* - e.g. a Cairo.Context's Handle property</param>
        ''' <param name="vClipWidth">Clip rectangle width</param>
        ''' <param name="vClipHeight">Clip rectangle height</param>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_render")>
        Friend Sub Render(vDoc As IntPtr, vCairoCtx As IntPtr, vClipWidth As Integer, vClipHeight As Integer)
        End Sub

        ''' <summary>
        ''' Reports a click at document-space (vX,vY). Returns a native pointer to a
        ''' malloc'd UTF-8 string (the clicked link's absolute URL) or IntPtr.Zero if the
        ''' click didn't land on a link - the caller MUST pass a non-zero result to
        ''' FreeString after reading it, never Marshal.FreeHGlobal/managed free
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <param name="vX">Click X in document-space pixels</param>
        ''' <param name="vY">Click Y in document-space pixels</param>
        ''' <returns>Native pointer to a malloc'd UTF-8 string, or IntPtr.Zero</returns>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_handle_click")>
        Friend Function HandleClick(vDoc As IntPtr, vX As Integer, vY As Integer) As IntPtr
        End Function

        ''' <summary>
        ''' Reports pointer movement at document-space (vX,vY)
        ''' </summary>
        ''' <param name="vDoc">The document handle</param>
        ''' <param name="vX">Pointer X in document-space pixels</param>
        ''' <param name="vY">Pointer Y in document-space pixels</param>
        ''' <returns>True if the hovered element changed in a way that likely needs a redraw</returns>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_handle_mouse_move")>
        Friend Function HandleMouseMove(vDoc As IntPtr, vX As Integer, vY As Integer) As Integer
        End Function

        ''' <summary>
        ''' Frees a native string previously returned by HandleClick
        ''' </summary>
        ''' <param name="vStr">The native pointer to free</param>
        <DllImport(cLibraryName, CallingConvention:=CallingConvention.Cdecl, EntryPoint:="lh_free_string")>
        Friend Sub FreeString(vStr As IntPtr)
        End Sub

    End Module

End Namespace
