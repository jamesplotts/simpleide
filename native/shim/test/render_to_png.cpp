/* render_to_png.cpp - Phase 3 validation harness (see plan). Renders a hardcoded HTML
 * string through the shim into a PNG file with zero .NET involvement, proving the whole
 * native pipeline (gumbo parse -> litehtml layout -> Cairo/Pango paint) works before any
 * P/Invoke interop code exists to potentially mask or introduce a separate class of bug.
 */
#include "../include/litehtml_shim.h"
#include <cairo.h>
#include <cstdio>
#include <cstdlib>

int main(int argc, char** argv)
{
	const char* lOutputPath = (argc > 1) ? argv[1] : "/tmp/litehtml_shim_test.png";

	const char* lHtml =
		"<html><head><style>"
		"body { background: #ffffff; font-family: sans-serif; margin: 20px; }"
		"h1 { color: #c0392b; }"
		"p { color: #333333; font-size: 14px; }"
		"a { color: #2980b9; }"
		"</style></head><body>"
		"<h1>Hello litehtml</h1>"
		"<p>This paragraph proves layout, styling, and Pango text rendering all work "
		"end-to-end through the shim, with zero .NET involvement.</p>"
		"<p><a href=\"https://example.com/\">A styled link</a></p>"
		"</body></html>";

	lh_document_handle lDoc = lh_create_document(lHtml, "https://example.com/");
	if (!lDoc)
	{
		std::fprintf(stderr, "lh_create_document failed\n");
		return 1;
	}

	const int lWidth = 600;
	lh_set_viewport_width(lDoc, lWidth);
	int lHeight = lh_get_content_height(lDoc);
	if (lHeight <= 0) lHeight = 200;

	cairo_surface_t* lSurface = cairo_image_surface_create(CAIRO_FORMAT_ARGB32, lWidth, lHeight);
	cairo_t* lCr = cairo_create(lSurface);

	// White background - lh_render only paints what the document's own CSS specifies,
	// and a transparent PNG background makes visual inspection harder.
	cairo_set_source_rgb(lCr, 1.0, 1.0, 1.0);
	cairo_paint(lCr);

	lh_render(lDoc, lCr, lWidth, lHeight);

	cairo_surface_flush(lSurface);
	cairo_status_t lStatus = cairo_surface_write_to_png(lSurface, lOutputPath);

	cairo_destroy(lCr);
	cairo_surface_destroy(lSurface);
	lh_destroy_document(lDoc);

	if (lStatus != CAIRO_STATUS_SUCCESS)
	{
		std::fprintf(stderr, "cairo_surface_write_to_png failed: %s\n", cairo_status_to_string(lStatus));
		return 1;
	}

	std::printf("Wrote %dx%d PNG to %s\n", lWidth, lHeight, lOutputPath);
	return 0;
}
