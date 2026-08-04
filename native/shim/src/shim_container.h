/* shim_container.h - the document_container implementation this shim wraps.
 *
 * Subclasses litehtml's own container_cairo_pango (containers/cairo/ in the litehtml
 * repo, vendored as a submodule) rather than reimplementing document_container's ~30
 * pure-virtual methods from scratch - container_cairo_pango already implements font/text
 * drawing (Pango) and most of the Cairo paint operations (fills, gradients, borders,
 * list markers). This subclass only overrides what's left, adapted from litehtml's own
 * containers/cairo/render2png.cpp reference container: resource loading (from an
 * in-memory pre-fetched map instead of the filesystem - see litehtml_shim.h's "pre-fetch
 * everything" model), viewport sizing, and click capture.
 */
#ifndef SIMPLEIDE_SHIM_CONTAINER_H
#define SIMPLEIDE_SHIM_CONTAINER_H

#include <litehtml.h>
#include <container_cairo_pango.h>
#include <cairo_images_cache.h>
#include <map>
#include <string>
#include <vector>

class shim_container : public container_cairo_pango
{
public:
	explicit shim_container(std::string vBaseUrl);

	/* Resource pre-registration (see lh_add_resource) - stores raw bytes keyed by the
	 * exact URL the host resolved and fetched. */
	void add_resource(const std::string& vUrl, const unsigned char* vBytes, int vLength);

	void set_viewport_size(int vWidth, int vHeight);
	int get_document_height() const;

	/* Synthesizes a full mouse_over + lbutton_down + lbutton_up sequence at the given
	 * document-space point and returns whatever URL on_anchor_click captured during it
	 * (empty if the click didn't land on a link). */
	std::string simulate_click(const litehtml::document::ptr& vDoc, int vX, int vY);

	/* Drives litehtml::document::on_mouse_over directly - see lh_handle_mouse_move. */
	bool simulate_mouse_move(const litehtml::document::ptr& vDoc, int vX, int vY);

	// ===== container_cairo's own remaining pure virtuals =====
	cairo_surface_t* get_image(const std::string& vUrl) override;
	double get_screen_dpi() const override;
	int get_screen_width() const override;
	int get_screen_height() const override;

	// ===== document_container's remaining pure virtuals =====
	void load_image(const char* vSrc, const char* vBaseUrl, bool vRedrawOnReady) override;
	void set_caption(const char* vCaption) override;
	void set_base_url(const char* vBaseUrl) override;
	void link(const std::shared_ptr<litehtml::document>& vDoc, const litehtml::element::ptr& vEl) override;
	void on_anchor_click(const char* vUrl, const litehtml::element::ptr& vEl) override;
	void on_mouse_event(const litehtml::element::ptr& vEl, litehtml::mouse_event vEvent) override;
	void set_cursor(const char* vCursor) override;
	void import_css(litehtml::string& vText, const litehtml::string& vUrl, litehtml::string& vBaseUrl) override;
	void get_viewport(litehtml::position& vViewport) const override;
	void get_media_features(litehtml::media_features& vMedia) const override;
	void get_language(litehtml::string& vLanguage, litehtml::string& vCulture) const override;
	void make_url(const char* vUrl, const char* vBasePath, litehtml::string& vOut) override;

private:
	std::string m_base_url;
	std::map<std::string, std::vector<unsigned char>> m_resources;
	cairo_images_cache m_images;
	int m_viewport_width;
	int m_viewport_height;
	std::string m_pending_click_url;

	const std::vector<unsigned char>* find_resource(const std::string& vUrl) const;
};

#endif /* SIMPLEIDE_SHIM_CONTAINER_H */
