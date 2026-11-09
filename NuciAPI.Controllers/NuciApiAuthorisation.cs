using System;
using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace NuciAPI.Controllers
{
    /// <summary>
    /// Provides the base contract for request authorisation strategies.
    /// </summary>
    /// <param name="name">The name of the authorisation strategy.</param>
    public abstract class NuciApiAuthorisation(string name)
    {
        private static readonly Regex BearerPrefixPattern = new(
            "^Bearer\\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Gets the name of the authorisation strategy.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets an authorisation strategy that performs no validation.
        /// </summary>
        public static NuciApiNoneAuthorisation None => new();

        /// <summary>
        /// Creates an authorisation strategy that validates an API key.
        /// </summary>
        /// <param name="key">The API key that must be supplied by the caller.</param>
        /// <returns>The API key authorisation strategy.</returns>
        public static NuciApiKeyAuthorisation ApiKey(string key) => new(key);

        /// <summary>
        /// Authorises the request using the supplied authorisation data.
        /// </summary>
        /// <param name="authorisationData">The raw authorisation header value.</param>
        public void Authorise(string authorisationData)
        {
            if (string.Equals(Name, None.Name, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrEmpty(authorisationData))
            {
                throw new AuthenticationException("Missing authorisation data.");
            }

            PerformAuthorisation(
                BearerPrefixPattern.Replace(authorisationData.Trim(), string.Empty));
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Name}Authorisation";

        /// <summary>
        /// Performs the strategy-specific authorisation check.
        /// </summary>
        /// <param name="authorisationData">The authorisation data with the scheme prefix removed.</param>
        protected abstract void PerformAuthorisation(string authorisationData);
    }
}
