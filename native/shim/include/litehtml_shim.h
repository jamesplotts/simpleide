/* litehtml_shim.h - C ABI surface SimpleIDE's VB.NET side P/Invokes into.
 *
 * Design: the "pre-fetch everything" model - the host (managed code) fetches the page's
 * HTML plus every referenced CSS/image resource via HttpClient *before* creating the
 * document, then hands each resource's raw bytes to lh_add_resource. Nothing in this shim
 * (or litehtml itself) ever performs network I/O - see SimpleIDE.CustomDrawHtmlView plan.
 *
 * All strings are UTF-8, NUL-terminated. Any string this shim returns to the caller
 * (lh_handle_click's return value) is heap-allocated with malloc and MUST be released by
 * the caller via lh_free_string - never free() it directly from managed code.
 */
#ifndef SIMPLEIDE_LITEHTML_SHIM_H
#define SIMPLEIDE_LITEHTML_SHIM_H

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32)
  #define LH_SHIM_API __declspec(dllexport)
#else
  #define LH_SHIM_API __attribute__((visibility("default")))
#endif

typedef void* lh_document_handle;

/* Creates a new document from an HTML string. vBaseUrl is used to resolve relative
 * links/images/stylesheets (e.g. "https://learn.microsoft.com/en-us/dotnet/visual-basic/")
 * and may be NULL/empty if unknown. Returns NULL on failure (caught internally, never
 * throws across the C ABI boundary). */
LH_SHIM_API lh_document_handle lh_create_document(const char* vHtmlUtf8, const char* vBaseUrl);

/* Destroys a document handle created by lh_create_document. Safe to call with NULL. */
LH_SHIM_API void lh_destroy_document(lh_document_handle vDoc);

/* Registers a pre-fetched resource (image or stylesheet) the document may reference by
 * URL, resolved the same way vBaseUrl above is - call this for every resource the host
 * discovered and downloaded BEFORE the first lh_render call that would need it. Copies
 * vBytes internally; the caller retains ownership of the buffer it passed in. */
LH_SHIM_API void lh_add_resource(lh_document_handle vDoc, const char* vUrl, const unsigned char* vBytes, int vLength);

/* Sets the layout viewport width in CSS pixels and (re)runs layout - call before the
 * first lh_render, and again whenever the host widget is resized. */
LH_SHIM_API void lh_set_viewport_width(lh_document_handle vDoc, int vWidthPx);

/* Valid only after at least one lh_set_viewport_width call. Drives the host's
 * ScrolledWindow content height. */
LH_SHIM_API int lh_get_content_height(lh_document_handle vDoc);

/* Paints the document into an existing Cairo context (vCairoCtx is the raw cairo_t*, e.g.
 * from CairoSharp's Cairo.Context.Handle) clipped to (0,0,vClipWidth,vClipHeight). */
LH_SHIM_API void lh_render(lh_document_handle vDoc, void* vCairoCtx, int vClipWidth, int vClipHeight);

/* Reports a click at document-space (vX,vY) (i.e. already offset by any scroll position
 * the host is applying). Returns a malloc'd absolute URL string if the click landed on a
 * link (caller must lh_free_string it), or NULL otherwise. */
LH_SHIM_API char* lh_handle_click(lh_document_handle vDoc, int vX, int vY);

/* Reports pointer movement at document-space (vX,vY); returns non-zero if the hovered
 * element changed in a way that likely needs a redraw (e.g. entered/left a link). */
LH_SHIM_API int lh_handle_mouse_move(lh_document_handle vDoc, int vX, int vY);

/* Frees a string previously returned by this shim (currently only lh_handle_click). */
LH_SHIM_API void lh_free_string(char* vStr);

#ifdef __cplusplus
}
#endif

#endif /* SIMPLEIDE_LITEHTML_SHIM_H */
