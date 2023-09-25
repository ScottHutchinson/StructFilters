#pragma once

namespace CLI {

    using namespace System;
    using namespace System::Configuration;
    using namespace System::Runtime::InteropServices;

    std::string toStd(System::String ^systemStr);

    ref class SettingsInternal {
    public:
        static System::String^ GetConfigFolderPath();
        static System::Configuration::Configuration^ LoadConfig(String^ fallbackConfigFilePath, [Out] String^% errorInfo);

    };
}
