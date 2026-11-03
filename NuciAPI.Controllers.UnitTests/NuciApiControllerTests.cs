using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NUnit.Framework;

using NuciAPI.Controllers;
using NuciAPI.Responses;

namespace NuciAPI.Controllers.UnitTests
{
    [TestFixture]
    public class NuciApiControllerTests
    {
        private TestController controller = null!;

        [SetUp]
        public void SetUp()
        {
            controller = new TestController();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void GivenNullRequest_WhenProcessingAction_ThenBadRequestIsReturnedAndActionIsNotExecuted()
        {
            bool actionWasExecuted = false;

            ActionResult result = controller.Process(
                null!,
                () => actionWasExecuted = true,
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result).Value, Is.TypeOf<NuciApiErrorResponse>());
            Assert.That(actionWasExecuted, Is.False);
        }

        [Test]
        public void GivenValidRequest_WhenProcessingAction_ThenOkResultIsReturnedAndActionIsExecuted()
        {
            TestRequest request = new();
            bool actionWasExecuted = false;

            ActionResult result = controller.Process(
                request,
                () => actionWasExecuted = true,
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.TypeOf<NuciApiSuccessResponse>());
            Assert.That(actionWasExecuted, Is.True);
        }

        [Test]
        public void GivenHmacHeader_WhenProcessingAction_ThenDecodedTokenIsStoredOnRequest()
        {
            TestRequest request = new();
            controller.HttpContext.Request.Headers["X-HMAC"] = "token%2Bvalue";

            controller.Process(request, () => { }, NuciApiAuthorisation.None);

            Assert.That(request.HmacToken, Is.EqualTo("token+value"));
        }

        [Test]
        public void GivenMissingHmacHeader_WhenProcessingAction_ThenRequestTokenRemainsUnchanged()
        {
            TestRequest request = new() { HmacToken = "existing" };

            controller.Process(request, () => { }, NuciApiAuthorisation.None);

            Assert.That(request.HmacToken, Is.EqualTo("existing"));
        }

        [Test]
        public void GivenNullAuthorisation_WhenProcessingValidRequest_ThenNotImplementedExceptionIsThrown()
            => Assert.Throws<NotImplementedException>(
                () => controller.Process(new TestRequest(), () => { }, null!));

        [Test]
        public void GivenNullResponseAndGetRequest_WhenProcessingResponseAction_ThenNotFoundIsReturned()
        {
            controller.HttpContext.Request.Method = "GET";

            ActionResult result = controller.Process<TestRequest, TestResponse>(
                new TestRequest(),
                () => null!,
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
            Assert.That(((NotFoundObjectResult)result).Value, Is.TypeOf<NuciApiErrorResponse>());
        }

        [Test]
        public void GivenNullResponseAndPostRequest_WhenProcessingResponseAction_ThenOkWithNullIsReturned()
        {
            controller.HttpContext.Request.Method = "POST";

            ActionResult result = controller.Process<TestRequest, TestResponse>(
                new TestRequest(),
                () => null!,
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.Null);
        }

        [Test]
        public void GivenResponseAction_WhenProcessingRequest_ThenResponseIsWrappedInOk()
        {
            TestResponse response = new();

            ActionResult result = controller.Process<TestRequest, TestResponse>(
                new TestRequest(),
                () => response,
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.SameAs(response));
        }

        [Test]
        public async Task GivenNullRequest_WhenProcessingAsyncAction_ThenBadRequestIsReturned()
        {
            bool actionWasExecuted = false;

            ActionResult result = await controller.ProcessAsync(
                null!,
                () =>
                {
                    actionWasExecuted = true;
                    return Task.CompletedTask;
                },
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(actionWasExecuted, Is.False);
        }

        [Test]
        public async Task GivenValidRequest_WhenProcessingAsyncAction_ThenActionIsExecuted()
        {
            bool actionWasExecuted = false;

            ActionResult result = await controller.ProcessAsync(
                new TestRequest(),
                () =>
                {
                    actionWasExecuted = true;
                    return Task.CompletedTask;
                },
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.TypeOf<NuciApiSuccessResponse>());
            Assert.That(actionWasExecuted, Is.True);
        }

        [Test]
        public async Task GivenAsyncResponseAction_WhenProcessingRequest_ThenResponseIsWrappedInOk()
        {
            TestResponse response = new();

            ActionResult result = await controller.ProcessAsync<TestRequest, TestResponse>(
                new TestRequest(),
                () => Task.FromResult<TestResponse>(response),
                NuciApiAuthorisation.None);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(((OkObjectResult)result).Value, Is.SameAs(response));
        }

    }
}
