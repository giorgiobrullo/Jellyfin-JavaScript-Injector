using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.JavaScriptInjector.Configuration;

namespace Jellyfin.Plugin.JavaScriptInjector.Helpers
{
    /// <summary>
    /// The public and private scripts as served, and the version naming their content.
    /// </summary>
    public static class ScriptBundle
    {
        /// <summary>
        /// Builds the script served to signed-in users (<paramref name="requiresAuth"/>) or to everyone.
        /// </summary>
        public static string Build(PluginConfiguration config, bool requiresAuth)
        {
            var scriptBuilder = new StringBuilder();

            // Filter user scripts based on whether they are enabled and match the authentication requirement
            var userScriptsToInject = config.CustomJavaScripts
                .Where(s => s.Enabled && s.RequiresAuthentication == requiresAuth);

            foreach (var scriptEntry in userScriptsToInject)
            {
                if (!string.IsNullOrWhiteSpace(scriptEntry.Script))
                {
                    scriptBuilder.AppendLine($"/* User Script: {scriptEntry.Name} */");
                    scriptBuilder.AppendLine("(function() { try {");
                    scriptBuilder.AppendLine(scriptEntry.Script);
                    scriptBuilder.AppendLine("} catch (e) { console.error('Error in Injected JavaScript [\"" + scriptEntry.Name + "\"]:', e); } })();");
                }
            }

            // Filter plugin scripts based on whether they are enabled and match the authentication requirement
            var pluginScriptsToInject = config.PluginJavaScripts
                .Where(s => s.Enabled && s.RequiresAuthentication == requiresAuth);

            foreach (var scriptEntry in pluginScriptsToInject)
            {
                if (!string.IsNullOrWhiteSpace(scriptEntry.Script))
                {
                    scriptBuilder.AppendLine($"/* Plugin Script: {scriptEntry.Name} (from {scriptEntry.PluginName}) */");
                    scriptBuilder.AppendLine("(function() { try {");
                    scriptBuilder.AppendLine(scriptEntry.Script);
                    scriptBuilder.AppendLine("} catch (e) { console.error('Error in Plugin JavaScript [\"" + scriptEntry.Name + "\" from \"" + scriptEntry.PluginName + "\"]:', e); } })();");
                }
            }

            return scriptBuilder.ToString();
        }

        /// <summary>
        /// A hash of the script's content. As the script's URL version, it changes exactly when the
        /// script does: no cache (the browser's, a CDN's such as Cloudflare's) can serve an old
        /// one, and an unchanged one is not fetched again on every page load.
        /// </summary>
        public static string Version(string script)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(script)), 0, 8);
        }
    }
}
