using System.Security.Authentication;

using NUnit.Framework;

using NuciAPI.Controllers;

namespace NuciAPI.Controllers.UnitTests
{
    [TestFixture]
    public class NuciApiAuthorisationTests
    {
        [Test]
        public void GivenNoneAuthorisation_WhenAuthorisingMissingData_ThenNoExceptionIsThrown()
            => Assert.DoesNotThrow(() => NuciApiAuthorisation.None.Authorise(null));

        [TestCase(null)]
        [TestCase("")]
        public void GivenApiKeyAuthorisation_WhenAuthorisingMissingData_ThenAuthenticationExceptionIsThrown(string authorisationData)
            => Assert.Throws<AuthenticationException>(
                () => NuciApiAuthorisation.ApiKey("expected-key").Authorise(authorisationData));

        [TestCase("expected-key")]
        [TestCase("Bearer expected-key")]
        [TestCase("bearer expected-key")]
        [TestCase("  Bearer expected-key  ")]
        public void GivenMatchingApiKey_WhenAuthorising_ThenNoExceptionIsThrown(string authorisationData)
            => Assert.DoesNotThrow(
                () => NuciApiAuthorisation.ApiKey("expected-key").Authorise(authorisationData));

        [TestCase("wrong-key")]
        [TestCase("Bearer wrong-key")]
        [TestCase("Bearer expected-key-suffix")]
        public void GivenNonMatchingApiKey_WhenAuthorising_ThenAuthenticationExceptionIsThrown(string authorisationData)
            => Assert.Throws<AuthenticationException>(
                () => NuciApiAuthorisation.ApiKey("expected-key").Authorise(authorisationData));

        [Test]
        public void GivenApiKeyAuthorisation_WhenAuthorisingBearerOnly_ThenAuthenticationExceptionIsThrown()
            => Assert.Throws<AuthenticationException>(
                () => NuciApiAuthorisation.ApiKey("expected-key").Authorise("Bearer "));

        [Test]
        public void GivenApiKeyAuthorisation_WhenAuthorisingDifferentSchemeWithMatchingText_ThenAuthenticationExceptionIsThrown()
            => Assert.Throws<AuthenticationException>(
                () => NuciApiAuthorisation.ApiKey("expected-key").Authorise("Basic expected-key"));

        [Test]
        public void GivenApiKeyAuthorisation_WhenCreated_ThenNameAndKeyAreExposed()
        {
            NuciApiKeyAuthorisation authorisation = NuciApiAuthorisation.ApiKey("expected-key");

            Assert.That(authorisation.Name, Is.EqualTo("ApiKey"));
            Assert.That(authorisation.Key, Is.EqualTo("expected-key"));
        }

        [Test]
        public void GivenNoneFactory_WhenCalledTwice_ThenDistinctAuthorisationsAreReturned()
        {
            NuciApiNoneAuthorisation first = NuciApiAuthorisation.None;
            NuciApiNoneAuthorisation second = NuciApiAuthorisation.None;

            Assert.That(first, Is.Not.SameAs(second));
        }

        [Test]
        public void GivenApiKeyAuthorisation_WhenConvertedToString_ThenNameIsReturnedWithSuffix()
            => Assert.That(
                NuciApiAuthorisation.ApiKey("expected-key").ToString(),
                Is.EqualTo("ApiKeyAuthorisation"));

        [Test]
        public void GivenNoneAuthorisation_WhenConvertedToString_ThenNameIsReturnedWithSuffix()
            => Assert.That(
                NuciApiAuthorisation.None.ToString(),
                Is.EqualTo("NoneAuthorisation"));
    }
}
