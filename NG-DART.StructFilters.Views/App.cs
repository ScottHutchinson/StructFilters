// Note: The Nightly Code Analysis build might show these suppressed warnings, which are not shown in Visual Studio.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace NGDartStructFilters.Views {
#pragma warning restore IDE0130 // Namespace does not match folder structure
    public sealed class App {
        private App() { }
        public static void Init() {
            StructFilters.PublicAPI.init(new StructFiltersWindow(), new WaitWindow());
        }

    }
} // namespace NGDartStructFilters.Views
