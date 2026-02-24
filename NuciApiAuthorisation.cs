using System;
using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace NuciAPI.Controllers
{
    public abstract class NuciApiAuthorisation(string name) : IEquatable<NuciApiAuthorisation>
    {
        public string Name { get; } = name;

        public static NuciApiNoneAuthorisation None => new();

        public static NuciApiKeyAuthorisation ApiKey(string key) => new(key);

        public void Authorise(string authorisationData)
        {
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

        public bool Equals(NuciApiAuthorisation other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((NuciApiAuthorisation)obj);
        }

        public override int GetHashCode() => $"{nameof(NuciApiAuthorisation)}:{Name}".GetHashCode();

        public override string ToString() => $"{Name}Authorisation";

        public static bool operator ==(
            NuciApiAuthorisation current,
            NuciApiAuthorisation other)
            => current.Equals(other);

        public static bool operator !=(
            NuciApiAuthorisation current,
            NuciApiAuthorisation other)
            => !current.Equals(other);

        public static implicit operator string(
            NuciApiAuthorisation authorisationMethod)
            => authorisationMethod.ToString();
    }
}
