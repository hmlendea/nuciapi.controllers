using System.Security.Authentication;

using NUnit.Framework;

using NuciAPI.Controllers;

namespace NuciAPI.Controllers.UnitTests
{
    [TestFixture]
    public class NuciApiKeyAuthorisationTests
    {
        [TestCase("key")]
        [TestCase("key with spaces")]
        public void GivenConfiguredKey_WhenAuthorisedWithSameValue_ThenNoExceptionIsThrown(string key)
            => Assert.DoesNotThrow(() => NuciApiAuthorisation.ApiKey(key).Authorise(key));

        [TestCase("configured", "different")]
        [TestCase("configured", "CONFIGURED")]
        public void GivenConfiguredKey_WhenAuthorisedWithDifferentValue_ThenAuthenticationExceptionIsThrown(
            string configuredKey,
            string suppliedKey)
            => Assert.Throws<AuthenticationException>(
                () => NuciApiAuthorisation.ApiKey(configuredKey).Authorise(suppliedKey));
    }
}
