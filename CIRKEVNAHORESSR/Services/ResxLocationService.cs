using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;

namespace CIRKEVNAHORESSR.Services
{
    public class ResxLocationService
    {
        private readonly ResourceManager _resourceManager;

        public ResxLocationService()
        {
            // Dynamicky sestavíme základní název (např. "LADA.Resources.Location")
            var assembly = Assembly.GetExecutingAssembly();
            var baseName = $"{assembly.GetName().Name}.Resources.Location";
            _resourceManager = new ResourceManager(baseName, assembly);
        }

        public string this[string key] =>
            _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

        public Task SetCurrentCultureAsync(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return Task.CompletedTask;
        }
    }
}