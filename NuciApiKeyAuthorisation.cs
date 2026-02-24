using System.Security.Authentication;

namespace NuciAPI.Controllers
{
    public class NuciApiKeyAuthorisation(string key) : NuciApiAuthorisation("ApiKey")
    {
        public string Key { get; } = key;

        protected override void PerformAuthorisation(string apiKey)
        {
            if (!Key.Equals(apiKey))
            {
                throw new AuthenticationException("Invalid API key.");
            }
        }
    }
}
