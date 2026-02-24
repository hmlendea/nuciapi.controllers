using System;

namespace NuciAPI.Controllers
{
    public class NuciApiNoneAuthorisation : NuciApiAuthorisation, IEquatable<NuciApiNoneAuthorisation>
    {
        internal NuciApiNoneAuthorisation() : base("None") { }

        public override void Authorise(string authenticationData)
        {
            // No authentication
        }

        public bool Equals(NuciApiNoneAuthorisation other)
            => base.Equals(other);

        public override bool Equals(object obj)
            => base.Equals(obj);

        public override int GetHashCode()
            => base.GetHashCode();

        public static bool operator ==(
            NuciApiNoneAuthorisation current,
            NuciApiNoneAuthorisation other)
            => current.Equals(other);

        public static bool operator ==(
            NuciApiNoneAuthorisation current,
            NuciApiAuthorisation other)
            => current.Equals(other);

        public static bool operator ==(
            NuciApiAuthorisation current,
            NuciApiNoneAuthorisation other)
            => other.Equals(current);

        public static bool operator !=(
            NuciApiNoneAuthorisation current,
            NuciApiNoneAuthorisation other)
            => !current.Equals(other);

        public static bool operator !=(
            NuciApiNoneAuthorisation current,
            NuciApiAuthorisation other)
            => !current.Equals(other);

        public static bool operator !=(
            NuciApiAuthorisation current,
            NuciApiNoneAuthorisation other)
            => !other.Equals(current);

        public static implicit operator string(
            NuciApiNoneAuthorisation authenticationMethod)
            => authenticationMethod.Name;
    }
}
