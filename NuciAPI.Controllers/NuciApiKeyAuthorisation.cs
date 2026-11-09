using System;
using System.Security.Authentication;

namespace NuciAPI.Controllers
{
    /// <summary>
    /// Validates requests against a configured API key.
    /// </summary>
    /// <param name="key">The API key that must be supplied by the caller.</param>
    public sealed class NuciApiKeyAuthorisation(string key) : NuciApiAuthorisation("ApiKey")
    {
        /// <summary>
        /// Gets the configured API key.
        /// </summary>
        public string Key { get; } = key;

        /// <inheritdoc/>
        protected override void PerformAuthorisation(string apiKey)
        {
            if (!string.Equals(Key, apiKey, StringComparison.Ordinal))
            {
                throw new AuthenticationException("Invalid API key.");
            }
        }
    }
}
