using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JavaScriptInjector.Configuration;
using Jellyfin.Plugin.JavaScriptInjector.Services;

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
                if (AppendExternalScript(scriptBuilder, scriptEntry, config.AllowExternalScriptUrls))
                {
                    continue;
                }

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
                if (AppendExternalScript(scriptBuilder, scriptEntry, config.AllowExternalScriptUrls))
                {
                    continue;
                }

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
        /// Emits loader code for entries that need their own script element (a src, or
        /// inline modules, since import is not valid inside the classic-script wrapper).
        /// </summary>
        /// <returns>True if the entry was handled here.</returns>
        private static bool AppendExternalScript(StringBuilder builder, CustomJavaScriptEntry entry, bool allowExternal)
        {
            var hasSrc = !string.IsNullOrWhiteSpace(entry.Src);
            if (!hasSrc && !entry.Module)
            {
                return false;
            }

            if (hasSrc && !JavaScriptRegistrationService.IsValidSrc(entry.Src))
            {
                builder.AppendLine($"console.error('JavaScript Injector: skipped script with invalid src', {JsonSerializer.Serialize(entry.Name)});");
                return true;
            }

            if (hasSrc && !allowExternal && JavaScriptRegistrationService.IsExternalSrc(entry.Src))
            {
                builder.AppendLine($"console.warn('JavaScript Injector: skipped external script URL (enable \"Allow external script URLs\" in the plugin settings to load it)', {JsonSerializer.Serialize(entry.Name)});");
                return true;
            }

            if (!hasSrc && string.IsNullOrWhiteSpace(entry.Script))
            {
                return true;
            }

            builder.AppendLine($"/* {(hasSrc ? "External" : "Module")} Script: {entry.Name.Replace("*/", "* /")} */");
            builder.AppendLine("(function() { try {");
            builder.AppendLine("var s = document.createElement('script');");
            if (entry.Module)
            {
                builder.AppendLine("s.type = 'module';");
            }

            builder.AppendLine(hasSrc
                ? $"s.src = {JsonSerializer.Serialize(entry.Src.Trim())};"
                : $"s.textContent = {JsonSerializer.Serialize(entry.Script)};");
            builder.AppendLine("document.head.appendChild(s);");
            builder.AppendLine("} catch (e) { console.error('Error loading script:', e); } })();");
            return true;
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
