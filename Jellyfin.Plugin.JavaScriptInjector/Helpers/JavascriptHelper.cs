namespace Jellyfin.Plugin.JavaScriptInjector.Helpers
{
    public static class JavascriptHelper
    {
        public static readonly string StartComment = "<!-- BEGIN JavaScript Injector Plugin -->";
        public static readonly string EndComment = "<!-- END JavaScript Injector Plugin -->";
        public static string BuildInjectionBlock()
        {
            var startComment = "<!-- BEGIN JavaScript Injector Plugin -->";
            var endComment = "<!-- END JavaScript Injector Plugin -->";

            // Each script's URL carries a hash of its content (ScriptBundle.Version): it changes when
            // the script does, which busts every cache in between (Cloudflare's, #8), and stays the
            // same otherwise, so browsers keep the script and index.html can answer 304. It was the
            // time, which made every page load fetch both scripts and a new index.html.
            var config = Plugin.Instance?.Configuration;
            var publicScript = config == null ? null : ScriptBundle.Build(config, false);
            var privateScript = config == null ? null : ScriptBundle.Build(config, true);
            var fallback = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var publicVersion = publicScript == null ? fallback : ScriptBundle.Version(publicScript);
            var timestamp = privateScript == null ? fallback : ScriptBundle.Version(privateScript);

            // Public scripts are loaded immediately for all users (including on the login page).
            var publicScriptTag = $"<script defer src=\"../JavaScriptInjector/public.js?v={publicVersion}\"></script>";
            // This inline script waits for the user to be authenticated and then fetches the private scripts.
            // It uses the official ApiClient.fetch method, which automatically includes authentication headers.
            var privateScriptLoader = @"
        <script>
            (function() {
                'use strict';
                const fetchPrivateScripts = () => {
                    // Check if the API client is fully initialized and a user is logged in.
                    if (window.ApiClient && typeof window.ApiClient.getCurrentUserId === 'function' && window.ApiClient.getCurrentUserId() && window.ApiClient.serverInfo) {
                        // Once authenticated, stop checking.
                        clearInterval(authInterval);
                        // Use the built-in ApiClient.fetch to make an authenticated request for the private scripts.
                        ApiClient.fetch({
                            url: ApiClient.getUrl('JavaScriptInjector/private.js?v=" + timestamp + @"'),
                            type: 'GET',
                            dataType: 'text'
                        }).then(scriptText => {
                            if (scriptText && scriptText.trim().length > 0) {
                                const scriptElement = document.createElement('script');
                                scriptElement.textContent = scriptText;
                                document.head.appendChild(scriptElement);
                                console.log('JavaScript Injector: Private scripts loaded successfully.');
                            }
                        }).catch(err => {
                            console.error('JavaScript Injector: Failed to load private scripts.', err);
                        });
                    }
                };
                // Check for authentication status every 50 milliseconds (a cheap check: the
                // private scripts no longer wait up to 300 ms after sign-in).
                const authInterval = setInterval(fetchPrivateScripts, 50);
            })();
        </script>";
            // The full block to be injected, wrapped in comments.
            var injectionBlock = $@"{startComment}
        {publicScriptTag}
        {privateScriptLoader}
        {endComment}";

            return injectionBlock;
        }
    }
}