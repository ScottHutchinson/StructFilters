#pragma once
#include "pch.h"
#include <winnt.h>

#ifdef _NG_DART_CLR_DLL_
#define DLLEXP_NG_DART_CLR	__declspec(dllexport)
#else
#define DLLEXP_NG_DART_CLR	__declspec(dllimport)
#endif

using std::string;

namespace CLR {
    DLLEXP_NG_DART_CLR LPCTSTR GetResourceNameForLoadedDLL();

    DLLEXP_NG_DART_CLR string LoadStructFiltersWindow(void* structResource, const int structResourceLength, const unsigned short msgTypeID, const char* msgTypeName, const char* parentStructName, bool& isSuccessful);

} // namespace CLR
