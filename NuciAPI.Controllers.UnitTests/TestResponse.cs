using NuciAPI.Responses;

namespace NuciAPI.Controllers.UnitTests
{
    internal sealed class TestResponse : NuciApiResponse
    {
        public TestResponse() : base("200", "Success") { }

        public override bool IsSuccessful => true;
    }
}
