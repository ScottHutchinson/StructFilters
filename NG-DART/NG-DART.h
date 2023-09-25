#pragma once

#ifndef VC_EXTRALEAN
#define VC_EXTRALEAN            // Exclude rarely-used stuff from Windows headers
#endif

#include "targetver.h"

#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS      // some CString constructors will be explicit

#ifndef _AFX_NO_AFXCMN_SUPPORT
#include <afxcmn.h>                     // MFC support for Windows Common Controls
#endif // _AFX_NO_AFXCMN_SUPPORT

#define _USE_MATH_DEFINES
//#include "minwindef.h"

struct stMsgTypeDefRecType {
	WORD	m_wMsgTypeID{};							// Message type ID
	char	m_szMsgTypeName[80]{};	// Message type name
	char	m_szMsgTypeFormat[80]{}; // Message type format - structure name
};

[[nodiscard]] HGLOBAL GetStructHdrResourceFromLoadedDLL(DWORD& resourceSize); //Loads the struct header resource in the loaded DLL as a resource handle
