using System;
using System.Security.Authentication;

namespace NuciAPI.Controllers
{
    public class NuciApiKeyAuthorisation(string key) : NuciApiAuthorisation("ApiKey"), IEquatable<NuciApiKeyAuthorisation>
    {
        public string Key { get; } = key;

        protected override void PerformAuthorisation(string apiKey)
        {
            if (!Key.Equals(apiKey))
            {
                throw new AuthenticationException("Invalid API key.");
            }
        }

        public bool Equals(NuciApiKeyAuthorisation other)
        {
            if (!base.Equals(other))
            {
                return false;
            }

            return Key == other.Key;
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

            if (obj.GetType().Equals(typeof(NuciApiAuthorisation)))
            {
                return Equals((NuciApiAuthorisation)obj);
            }

            return Equals((NuciApiKeyAuthorisation)obj);
        }

        public override int GetHashCode() => $"{base.GetHashCode()}:{Key}".GetHashCode();

        public static bool operator ==(
            NuciApiKeyAuthorisation current,
            NuciApiKeyAuthorisation other)
            => current.Equals(other);

        public static bool operator ==(
            NuciApiKeyAuthorisation current,
            NuciApiAuthorisation other)
            => current.Equals(other);

        public static bool operator ==(
            NuciApiAuthorisation current,
            NuciApiKeyAuthorisation other)
            => other.Equals(current);

        public static bool operator !=(
            NuciApiKeyAuthorisation current,
            NuciApiKeyAuthorisation other)
            => !current.Equals(other);

        public static bool operator !=(
            NuciApiKeyAuthorisation current,
            NuciApiAuthorisation other)
            => !current.Equals(other);

        public static bool operator !=(
            NuciApiAuthorisation current,
            NuciApiKeyAuthorisation other)
            => !other.Equals(current);

        public static implicit operator string(
            NuciApiKeyAuthorisation authenticationMethod)
            => authenticationMethod.Name;
    }
}
