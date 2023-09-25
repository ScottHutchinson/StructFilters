#include "pch.h"
#include "NG-DART.CLR.h"
#include "Internal.h"
#include "Resource.h"

namespace CLR {

    using namespace System;
    using namespace System::Configuration;
    using namespace System::Diagnostics;
    using namespace System::IO;
    using namespace System::Reflection;
    using namespace System::Runtime::InteropServices;
    using namespace System::Runtime::Versioning;

    using namespace NGDartStructFilters;

    LPCTSTR GetResourceNameForLoadedDLL() {
        return MAKEINTRESOURCE(GDD_HEADER);
    }

    string LoadStructFiltersWindow(void* structResource, const int structResourceLength, const unsigned short msgTypeID, const char* msgTypeName, const char* parentStructName, bool& isSuccessful) {
        try {
            bool isFilteringEnabled = true; // userSettings.GetIsStructFilteringEnabled();
            auto configFolderPath = CLI::SettingsInternal::GetConfigFolderPath();
            Views::App::Init();
            auto logMsgStr = StructFilters::PublicAPI::loadWindow(
                System::IntPtr(structResource),
                structResourceLength,
                msgTypeID,
                gcnew String(msgTypeName),
                gcnew String(parentStructName),
                configFolderPath, 
                isSuccessful,
                isFilteringEnabled);
            return CLI::toStd(logMsgStr);
        }
        catch (Exception^ ex) {
            return CLI::toStd(ex->ToString());
        }
        catch (const std::exception& ex) {
            return ex.what();
        }
    } // LoadStructFiltersWindow

} // namespace CLR
