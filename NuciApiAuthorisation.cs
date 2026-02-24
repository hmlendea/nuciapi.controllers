using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace NuciAPI.Controllers
{
    public abstract class NuciApiAuthorisation(string name)
    {
        public string Name { get; } = name;

        public static NuciApiNoneAuthorisation None => new();

        public static NuciApiKeyAuthorisation ApiKey(string key) => new(key);

        public void Authorise(string authorisationData)
        {
            if (Name.Equals(None.Name))
            {
                return;
            }

            if (string.IsNullOrEmpty(authorisationData))
            {
                throw new AuthenticationException("Missing authorisation data.");
            }

            PerformAuthorisation(Regex.Replace(
                authorisationData.Trim(),
                @"^Bearer\s+", "",
                RegexOptions.IgnoreCase));
        }

        protected abstract void PerformAuthorisation(string authorisationData);

        public override string ToString() => $"{Name}Authorisation";
    }
}
