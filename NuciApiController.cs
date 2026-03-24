using System;
using System.Net;

using Microsoft.AspNetCore.Mvc;

using NuciAPI.Requests;
using NuciAPI.Responses;
using System.Linq;
using System.Threading.Tasks;

namespace NuciAPI.Controllers
{
    public abstract class NuciApiController : ControllerBase
    {
        /// <summary>
        /// Processes a request and returns a response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="authorisation">The authorisation method to use for the request.</param>
        /// <returns>An ActionResult containing the response.</returns>
        protected ActionResult ProcessRequest<TRequest>(
            TRequest request,
            Action action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            AuthoriseRequest(authorisation);
            RetrieveHmacTokenFromHeaders(request);

            action();
            return Ok(NuciApiSuccessResponse.Default);
        }

        /// <summary>
        /// Processes a request and returns a response asynchronously.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The action to execute.</param>
        /// <param name="authorisation">The authorisation method to use for the request.</param>
        /// <returns>An ActionResult containing the response.</returns>
        protected async Task<ActionResult> ProcessRequestAsync<TRequest>(
            TRequest request,
            Func<Task> action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            AuthoriseRequest(authorisation);
            RetrieveHmacTokenFromHeaders(request);

            await action();
            return Ok(NuciApiSuccessResponse.Default);
        }

        /// <summary>
        /// Processes a request and returns a response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The action to execute, which should return a response of type TResponse.</param>
        /// <param name="authorisation">The authorisation method to use for the request.</param>
        /// <returns>An ActionResult containing the response.</returns>
        protected ActionResult ProcessRequest<TRequest, TResponse>(
            TRequest request,
            Func<TResponse> action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            AuthoriseRequest(authorisation);
            RetrieveHmacTokenFromHeaders(request);

            TResponse response = action();

            if (response is null && Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound("Resource not found.");
            }

            return Ok(response);
        }

        /// <summary>
        /// Processes a request and returns a response asynchronously.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The action to execute, which should return a response of type TResponse.</param>
        /// <param name="authorisation">The authorisation method to use for the request.</param>
        /// <returns>An ActionResult containing the response.</returns>
        protected async Task<ActionResult> ProcessRequest<TRequest, TResponse>(
            TRequest request,
            Func<Task<TResponse>> action,
            NuciApiAuthorisation authorisation)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            AuthoriseRequest(authorisation);
            RetrieveHmacTokenFromHeaders(request);

            TResponse response = await action();

            if (response is null && Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound("Resource not found.");
            }

            return Ok(response);
        }

        protected string GetHeaderValue(string headerName)
            => Request.Headers[headerName].FirstOrDefault();

        private void AuthoriseRequest(NuciApiAuthorisation authorisation)
        {
            if (authorisation is null)
            {
                throw new NotImplementedException("This endpoint has no authorisation specified.");
            }

            authorisation.Authorise(GetHeaderValue("Authorization"));
        }

        private void RetrieveHmacTokenFromHeaders<TRequest>(TRequest request)
            where TRequest : NuciApiRequest
        {
            string hmacToken = GetHeaderValue("X-HMAC");

            if (!string.IsNullOrEmpty(hmacToken))
            {
                request.HmacToken = WebUtility.UrlDecode(hmacToken);
            }
        }
    }
}
