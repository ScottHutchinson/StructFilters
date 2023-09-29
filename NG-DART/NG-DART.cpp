// NG-DART.cpp : This file contains the 'main' function. Program execution begins and ends there.
//
#include "NG-DART.h"
#include "NG-DART.CLR/NG-DART.CLR.h"
#include <iostream>
#include <string>

using std::string;

HGLOBAL LoadRcDataResourceFromDll(HMODULE hModule, LPCSTR name, DWORD& resourceSize) {

    HRSRC hResource = FindResource(hModule, name, RT_RCDATA);
    if (hResource == nullptr) {
        CString errString;
        errString.Format("Failed to find resource %s. Error = %d", name, GetLastError());
        //this->m_pDoc->AddErrorLogInfo(errString);
        return nullptr;
    }

    HGLOBAL hResourceData = LoadResource(hModule, hResource);
    if (hResourceData == nullptr) {
        CString errString;
        errString.Format("Failed to load resource resource %s. Error = %d", name, GetLastError());
        //this->m_pDoc->AddErrorLogInfo(errString);
        return nullptr;
    }

    resourceSize = SizeofResource(hModule, hResource);
    if (resourceSize == 0) {
        CString errString;
        errString.Format("Failed to get the size of the resource %s. Error = %d", name, GetLastError());
        //this->m_pDoc->AddErrorLogInfo(errString);
        return nullptr;
    }

    return hResourceData;
} //CBaseDXFile::LoadRcDataResourceFromDll

HGLOBAL GetStructHdrResourceFromLoadedDLL(DWORD& resourceSize) {
    auto hModule = ::GetModuleHandle("NG-DART.CLR.dll");
    return LoadRcDataResourceFromDll(hModule, CLR::GetResourceNameForLoadedDLL(), resourceSize);
} //CBaseDXFile::GetStructHdrFromLoadedDll

int main() {
    try {
        if (HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED); !SUCCEEDED(hr)) {
            std::cout << "\n  could not initialize COM";
            return 1;
        }
        //stMsgTypeDefRecType mt{ .m_wMsgTypeID=0x0205, .m_szMsgTypeName="Dabblers_MT", .m_szMsgTypeFormat="dabblers" };
        stMsgTypeDefRecType mt{ .m_wMsgTypeID = 0x0414, .m_szMsgTypeName = "Inbent_MT", .m_szMsgTypeFormat = "inbent" };
        bool isSuccessful{};
        DWORD structHdrResSize{};
        auto structHdrResHandle = GetStructHdrResourceFromLoadedDLL(structHdrResSize);
        const string logMsg = CLR::LoadStructFiltersWindow(
            structHdrResHandle,
            structHdrResSize,
            mt.m_wMsgTypeID, mt.m_szMsgTypeName, mt.m_szMsgTypeFormat, isSuccessful);
        if (isSuccessful) {
            std::cout << "successful";
        }
        else {
            std::cout << logMsg;
        }
        CoUninitialize();
    }
    catch (const std::exception& ex) {
        std::cout << "\n" << ex.what();
    }
} // main
