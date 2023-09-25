#include "pch.h"
#include <msclr\marshal_cppstd.h>
#include "Internal.h"

namespace CLI {
    using namespace System;
    using namespace System::IO;

    // This function converts a managed System::String to an unmanaged std::string.
    std::string toStd(System::String^ systemStr) {
        return msclr::interop::marshal_as<std::string>(systemStr);
    }

    static constexpr auto s_configFolderName ="StructFilters";

    System::String^ SettingsInternal::GetConfigFolderPath() {
        const auto appDataPath = Environment::GetFolderPath(Environment::SpecialFolder::ApplicationData);
        String^ configFolderName = gcnew String(s_configFolderName);
        // TODO: Handle if not Directory::Exists(appDataPath)
        return Path::Combine(appDataPath, configFolderName);
    }

    System::Configuration::Configuration^ SettingsInternal::LoadConfig(String^ fallbackConfigFilePath, [Out] String^% errorInfo) {
        System::Configuration::Configuration^ ret;
        auto configFileMap = gcnew ExeConfigurationFileMap();
        // In case anything goes wrong, default to the specified path (where NG-DART.exe is located).
        configFileMap->ExeConfigFilename = fallbackConfigFilePath;
        try {
            // Get the user's AppData\Roaming folder path.
            auto appDataPath = Environment::GetFolderPath(Environment::SpecialFolder::ApplicationData);
            if (Directory::Exists(appDataPath)) {
                String^ configFolderName = gcnew String(s_configFolderName);
                String^ configFolderPath = Path::Combine(appDataPath, configFolderName);
                String^ configFileName = Path::GetFileName(fallbackConfigFilePath);
                String^ configFilePathInAppData = Path::Combine(configFolderPath, configFileName);
                bool fileExists = false;
                if (Directory::Exists(configFolderPath)) {
                    fileExists = File::Exists(configFilePathInAppData);
                }
                else { // no folder, so obviously no file either.
                    Directory::CreateDirectory(configFolderPath);
                }
                if (!fileExists) {
                    File::Copy(fallbackConfigFilePath, configFilePathInAppData);
                } // else use the existing file. In future, we might need to add new settings to it.
                configFileMap->ExeConfigFilename = configFilePathInAppData;
            } // else just use the fallback path, since the AppData folder is not there.
            ret = ConfigurationManager::OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel::None);
        }
        catch (Exception^ ex) { // Pass the error info back to the caller.
            errorInfo = String::Format("***** LoadConfig Error for {0}: {1}", fallbackConfigFilePath, ex->Message);
        }
        return ret;
    } // SettingsInternal::LoadConfig

} // namespace CLI
