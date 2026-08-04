#include "shim_container.h"
#include <litehtml/url.h>
#include <gdk-pixbuf/gdk-pixbuf.h>
#include <cstdint>
#include <cstring>

shim_container::shim_container(std::string vBaseUrl)
	: m_base_url(std::move(vBaseUrl)), m_viewport_width(800), m_viewport_height(600)
{
}

void shim_container::add_resource(const std::string& vUrl, const unsigned char* vBytes, int vLength)
{
	if (vLength <= 0) return;
	m_resources[vUrl] = std::vector<unsigned char>(vBytes, vBytes + vLength);
}

const std::vector<unsigned char>* shim_container::find_resource(const std::string& vUrl) const
{
	auto lIter = m_resources.find(vUrl);
	if (lIter == m_resources.end()) return nullptr;
	return &lIter->second;
}

void shim_container::set_viewport_size(int vWidth, int vHeight)
{
	m_viewport_width = vWidth;
	m_viewport_height = vHeight;
}

int shim_container::get_document_height() const
{
	return m_viewport_height;
}

std::string shim_container::simulate_click(const litehtml::document::ptr& vDoc, int vX, int vY)
{
	m_pending_click_url.clear();
	litehtml::position::vector lRedraw;
	vDoc->on_mouse_over(vX, vY, vX, vY, lRedraw);
	vDoc->on_lbutton_down(vX, vY, vX, vY, lRedraw);
	vDoc->on_lbutton_up(vX, vY, vX, vY, lRedraw);
	return m_pending_click_url;
}

bool shim_container::simulate_mouse_move(const litehtml::document::ptr& vDoc, int vX, int vY)
{
	litehtml::position::vector lRedraw;
	bool lChanged = vDoc->on_mouse_over(vX, vY, vX, vY, lRedraw);
	return lChanged && !lRedraw.empty();
}

cairo_surface_t* shim_container::get_image(const std::string& vUrl)
{
	if (vUrl.empty()) return nullptr;

	cairo_surface_t* lSurface = m_images.get_image(vUrl);
	if (lSurface) return lSurface;

	const std::vector<unsigned char>* lBytes = find_resource(vUrl);
	if (!lBytes) return nullptr;

	// Decode via gdk-pixbuf (already a hard dependency of any GTK app - handles PNG,
	// JPEG, GIF, WebP, etc. via whatever gdk-pixbuf loaders the system has installed,
	// so this shim doesn't need to vendor its own image decoder)
	GdkPixbufLoader* lLoader = gdk_pixbuf_loader_new();
	GError* lError = nullptr;
	gdk_pixbuf_loader_write(lLoader, lBytes->data(), lBytes->size(), &lError);
	if (!lError) gdk_pixbuf_loader_close(lLoader, &lError);

	if (lError)
	{
		g_error_free(lError);
		g_object_unref(lLoader);
		return nullptr;
	}

	GdkPixbuf* lPixbuf = gdk_pixbuf_loader_get_pixbuf(lLoader);
	if (!lPixbuf)
	{
		g_object_unref(lLoader);
		return nullptr;
	}

	int lWidth = gdk_pixbuf_get_width(lPixbuf);
	int lHeight = gdk_pixbuf_get_height(lPixbuf);
	bool lHasAlpha = gdk_pixbuf_get_has_alpha(lPixbuf) != FALSE;
	cairo_format_t lFormat = lHasAlpha ? CAIRO_FORMAT_ARGB32 : CAIRO_FORMAT_RGB24;
	lSurface = cairo_image_surface_create(lFormat, lWidth, lHeight);
	if (lSurface && cairo_surface_status(lSurface) == CAIRO_STATUS_SUCCESS)
	{
		// Deliberately not gdk_cairo_set_source_pixbuf() - that lives in full GDK
		// (gdk-3.0), which pulls in the whole X11/Wayland windowing stack. This shim only
		// links gdk-pixbuf-2.0 (decode only), so pixels are copied by hand instead: read
		// each gdk-pixbuf RGB(A) pixel, premultiply alpha (Cairo's ARGB32 contract), and
		// pack into the native-endian 32-bit word Cairo expects.
		cairo_surface_flush(lSurface);

		unsigned char* lDst = cairo_image_surface_get_data(lSurface);
		int lDstStride = cairo_image_surface_get_stride(lSurface);
		const unsigned char* lSrc = gdk_pixbuf_get_pixels(lPixbuf);
		int lSrcStride = gdk_pixbuf_get_rowstride(lPixbuf);
		int lChannels = gdk_pixbuf_get_n_channels(lPixbuf);

		for (int lY = 0; lY < lHeight; ++lY)
		{
			const unsigned char* lSrcRow = lSrc + static_cast<std::size_t>(lY) * lSrcStride;
			uint32_t* lDstRow = reinterpret_cast<uint32_t*>(lDst + static_cast<std::size_t>(lY) * lDstStride);
			for (int lX = 0; lX < lWidth; ++lX)
			{
				const unsigned char* lPixel = lSrcRow + static_cast<std::size_t>(lX) * lChannels;
				unsigned int lR = lPixel[0];
				unsigned int lG = lPixel[1];
				unsigned int lB = lPixel[2];
				unsigned int lA = lHasAlpha ? lPixel[3] : 255u;

				if (lHasAlpha && lA != 255u)
				{
					lR = (lR * lA + 127u) / 255u;
					lG = (lG * lA + 127u) / 255u;
					lB = (lB * lA + 127u) / 255u;
				}

				lDstRow[lX] = (lA << 24) | (lR << 16) | (lG << 8) | lB;
			}
		}

		cairo_surface_mark_dirty(lSurface);

		m_images.add_image(vUrl, lSurface);
		// add_image doesn't take a reference for the caller - we must do it manually
		// (matches litehtml's own containers/cairo/render2png.cpp reference)
		lSurface = cairo_surface_reference(lSurface);
	}

	g_object_unref(lLoader);
	return lSurface;
}

double shim_container::get_screen_dpi() const { return 96.0; }
int shim_container::get_screen_width() const { return m_viewport_width; }
int shim_container::get_screen_height() const { return m_viewport_height; }

void shim_container::load_image(const char* /*vSrc*/, const char* /*vBaseUrl*/, bool /*vRedrawOnReady*/)
{
	// No-op: the "pre-fetch everything" model means every resource litehtml will ever
	// ask for via get_image()/import_css() is already in m_resources by the time layout
	// runs, so there's nothing to kick off here (see litehtml_shim.h's design note).
}

void shim_container::set_caption(const char* /*vCaption*/) {}

void shim_container::set_base_url(const char* vBaseUrl)
{
	if (vBaseUrl && *vBaseUrl) m_base_url = vBaseUrl;
}

void shim_container::link(const std::shared_ptr<litehtml::document>& /*vDoc*/, const litehtml::element::ptr& /*vEl*/) {}

void shim_container::on_anchor_click(const char* vUrl, const litehtml::element::ptr& /*vEl*/)
{
	if (!vUrl) return;
	litehtml::string lResolved;
	make_url(vUrl, nullptr, lResolved);
	m_pending_click_url = lResolved;
}

void shim_container::on_mouse_event(const litehtml::element::ptr& /*vEl*/, litehtml::mouse_event /*vEvent*/) {}

void shim_container::set_cursor(const char* /*vCursor*/) {}

void shim_container::import_css(litehtml::string& vText, const litehtml::string& vUrl, litehtml::string& vBaseUrl)
{
	litehtml::string lResolved;
	make_url(vUrl.c_str(), vBaseUrl.empty() ? nullptr : vBaseUrl.c_str(), lResolved);

	const std::vector<unsigned char>* lBytes = find_resource(lResolved);
	if (lBytes)
	{
		vText.assign(reinterpret_cast<const char*>(lBytes->data()), lBytes->size());
	}
	// If the stylesheet wasn't pre-fetched, litehtml just proceeds without it - a missing
	// stylesheet degrading layout rather than failing the whole page is the right
	// trade-off for a documentation viewer.
}

void shim_container::get_viewport(litehtml::position& vViewport) const
{
	vViewport.x = 0;
	vViewport.y = 0;
	vViewport.width = m_viewport_width;
	vViewport.height = m_viewport_height;
}

void shim_container::get_media_features(litehtml::media_features& vMedia) const
{
	vMedia.type = litehtml::media_type_screen;
	vMedia.width = m_viewport_width;
	vMedia.height = m_viewport_height;
	vMedia.device_width = m_viewport_width;
	vMedia.device_height = m_viewport_height;
	vMedia.color = 8;
	vMedia.monochrome = 0;
	vMedia.color_index = 256;
	vMedia.resolution = static_cast<int>(get_screen_dpi());
}

void shim_container::get_language(litehtml::string& vLanguage, litehtml::string& vCulture) const
{
	vLanguage = "en";
	vCulture = "";
}

void shim_container::make_url(const char* vUrl, const char* vBasePath, litehtml::string& vOut)
{
	if (!vUrl) { vOut.clear(); return; }

	std::string lBase = (vBasePath && *vBasePath) ? vBasePath : m_base_url;
	if (lBase.empty())
	{
		vOut = vUrl;
		return;
	}

	// litehtml::resolve is a real RFC 3986 resolver (litehtml/url.h) - handles relative
	// paths, protocol-relative "//host/path", and absolute URLs correctly, unlike
	// container_cairo's own default make_url (which just returns vUrl verbatim).
	litehtml::url lResolved = litehtml::resolve(litehtml::url(lBase), litehtml::url(vUrl));
	vOut = lResolved.str();
}
