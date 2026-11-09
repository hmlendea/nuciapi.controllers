using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuciAPI.Requests;
using NuciAPI.Responses;

namespace NuciAPI.Controllers
{
    /// <summary>
    /// Provides the base controller that standardises request validation, authorisation,
    /// HMAC extraction, and response wrapping for ASP.NET Core REST APIs.
    /// </summary>
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
        /// <param name="action">The asynchronous action to execute.</param>
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

            await action().ConfigureAwait(false);

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

            if (response is null
                && string.Equals(Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(NuciApiErrorResponse.NotFound);
            }

            return Ok(response);
        }

        /// <summary>
        /// Processes a request and returns a response asynchronously.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The asynchronous action to execute, which should return a response of type TResponse.</param>
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

            TResponse response = await action().ConfigureAwait(false);

            if (response is null
                && string.Equals(Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(NuciApiErrorResponse.NotFound);
            }

            return Ok(response);
        }

        /// <summary>
        /// Retrieves the first value of the specified request header.
        /// </summary>
        /// <param name="headerName">The name of the header to retrieve.</param>
        /// <returns>The header value, or an empty string when the header is absent.</returns>
        protected string GetHeaderValue(string headerName)
        {
            string headerValue = Request.Headers[headerName].FirstOrDefault();

            if (headerValue is null)
            {
                return string.Empty;
            }

            return headerValue;
        }

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
