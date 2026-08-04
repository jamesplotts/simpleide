#include "litehtml_shim.h"
#include "shim_container.h"
#include <litehtml.h>
#include <cstdlib>
#include <cstring>
#include <iostream>

/* Opaque handle contents: the container (resource cache, viewport state, click capture)
 * and the litehtml document it produced both need to outlive rendering, so both live
 * together behind the single lh_document_handle the managed side holds. */
struct lh_document
{
	shim_container container;
	litehtml::document::ptr doc;

	explicit lh_document(const std::string& vBaseUrl) : container(vBaseUrl) {}
};

extern "C" {

LH_SHIM_API lh_document_handle lh_create_document(const char* vHtmlUtf8, const char* vBaseUrl)
{
	try
	{
		if (!vHtmlUtf8) return nullptr;
		auto* lHandle = new lh_document(vBaseUrl ? vBaseUrl : "");
		lHandle->doc = litehtml::document::createFromString(std::string(vHtmlUtf8), &lHandle->container);
		if (!lHandle->doc)
		{
			delete lHandle;
			return nullptr;
		}
		return lHandle;
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_create_document error: " << vEx.what() << std::endl;
		return nullptr;
	}
	catch (...)
	{
		std::cerr << "lh_create_document error: unknown exception" << std::endl;
		return nullptr;
	}
}

LH_SHIM_API void lh_destroy_document(lh_document_handle vDoc)
{
	if (!vDoc) return;
	delete static_cast<lh_document*>(vDoc);
}

LH_SHIM_API void lh_add_resource(lh_document_handle vDoc, const char* vUrl, const unsigned char* vBytes, int vLength)
{
	if (!vDoc || !vUrl || !vBytes) return;
	try
	{
		static_cast<lh_document*>(vDoc)->container.add_resource(vUrl, vBytes, vLength);
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_add_resource error: " << vEx.what() << std::endl;
	}
}

LH_SHIM_API void lh_set_viewport_width(lh_document_handle vDoc, int vWidthPx)
{
	if (!vDoc || vWidthPx <= 0) return;
	try
	{
		auto* lHandle = static_cast<lh_document*>(vDoc);
		// Height is provisional here - litehtml lays out width-driven, then
		// document::height() reports the actual content height afterward (see
		// lh_get_content_height), matching how litehtml's own render2png.cpp does it.
		lHandle->container.set_viewport_size(vWidthPx, lHandle->container.get_document_height());
		lHandle->doc->render(vWidthPx);
		lHandle->container.set_viewport_size(vWidthPx, static_cast<int>(lHandle->doc->height()));
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_set_viewport_width error: " << vEx.what() << std::endl;
	}
}

LH_SHIM_API int lh_get_content_height(lh_document_handle vDoc)
{
	if (!vDoc) return 0;
	return static_cast<lh_document*>(vDoc)->container.get_document_height();
}

LH_SHIM_API void lh_render(lh_document_handle vDoc, void* vCairoCtx, int vClipWidth, int vClipHeight)
{
	if (!vDoc || !vCairoCtx) return;
	try
	{
		auto* lHandle = static_cast<lh_document*>(vDoc);
		litehtml::position lClip(0, 0, vClipWidth, vClipHeight);
		lHandle->doc->draw(reinterpret_cast<litehtml::uint_ptr>(vCairoCtx), 0, 0, &lClip);
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_render error: " << vEx.what() << std::endl;
	}
}

LH_SHIM_API char* lh_handle_click(lh_document_handle vDoc, int vX, int vY)
{
	if (!vDoc) return nullptr;
	try
	{
		auto* lHandle = static_cast<lh_document*>(vDoc);
		std::string lUrl = lHandle->container.simulate_click(lHandle->doc, vX, vY);
		if (lUrl.empty()) return nullptr;

		char* lResult = static_cast<char*>(std::malloc(lUrl.size() + 1));
		if (!lResult) return nullptr;
		std::memcpy(lResult, lUrl.c_str(), lUrl.size() + 1);
		return lResult;
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_handle_click error: " << vEx.what() << std::endl;
		return nullptr;
	}
}

LH_SHIM_API int lh_handle_mouse_move(lh_document_handle vDoc, int vX, int vY)
{
	if (!vDoc) return 0;
	try
	{
		auto* lHandle = static_cast<lh_document*>(vDoc);
		return lHandle->container.simulate_mouse_move(lHandle->doc, vX, vY) ? 1 : 0;
	}
	catch (const std::exception& vEx)
	{
		std::cerr << "lh_handle_mouse_move error: " << vEx.what() << std::endl;
		return 0;
	}
}

LH_SHIM_API void lh_free_string(char* vStr)
{
	std::free(vStr);
}

} // extern "C"
