using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuciAPI.Controllers;
using NuciAPI.Requests;
using NuciAPI.Responses;

namespace NuciAPI.Controllers.UnitTests
{
    internal sealed class TestController : NuciApiController
    {
        public ActionResult Process(
            TestRequest request,
            Action action,
            NuciApiAuthorisation authorisation)
            => ProcessRequest(request, action, authorisation);

        public Task<ActionResult> ProcessAsync(
            TestRequest request,
            Func<Task> action,
            NuciApiAuthorisation authorisation)
            => ProcessRequestAsync(request, action, authorisation);

        public ActionResult Process<TRequest, TResponse>(
            TRequest request,
            Func<TResponse> action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
            => ProcessRequest(request, action, authorisation);

        public Task<ActionResult> ProcessAsync<TRequest, TResponse>(
            TRequest request,
            Func<Task<TResponse>> action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
            => ProcessRequest(request, action, authorisation);
    }
}
