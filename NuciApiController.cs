using System;
using System.Security;
using System.Security.Authentication;
using System.Net;

using Microsoft.AspNetCore.Mvc;

using NuciAPI.Requests;
using NuciAPI.Responses;
using NuciDAL.Repositories;
using System.Collections.Generic;

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
        /// <returns>An ActionResult containing the response.</returns>
        protected ActionResult ProcessRequest<TRequest>(TRequest request, Action action)
            where TRequest : NuciApiRequest
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            return ExecuteWithStandardHandling(() =>
            {
                action();
                return Ok(NuciApiSuccessResponse.Default);
            });
        }

        /// <summary>
        /// Processes a request and returns a response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request object containing the parameters.</param>
        /// <param name="action">The action to execute, which should return a response of type TResponse.</param>
        /// <returns>An ActionResult containing the response.</returns>
        protected ActionResult ProcessRequest<TRequest, TResponse>(TRequest request, Func<TResponse> action)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            return ExecuteWithStandardHandling(() =>
            {
                TResponse response = action();

                if (response is null && Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound("Resource not found.");
                }

                return Ok(response);
            });
        }

        private ActionResult ExecuteWithStandardHandling(Func<ActionResult> action)
        {
            try
            {
                return action();
            }
            catch (SecurityException ex)
            {
                return Unauthorized(new NuciApiErrorResponse(ex));
            }
            catch (AuthenticationException ex)
            {
                return StatusCode((int)HttpStatusCode.Forbidden, new NuciApiErrorResponse(ex));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(NuciApiErrorResponse.NotFound);
            }
            catch (DuplicateEntityException)
            {
                return Conflict(NuciApiErrorResponse.AlreadyExists);
            }
            catch (TimeoutException)
            {
                return StatusCode((int)HttpStatusCode.GatewayTimeout, new NuciApiErrorResponse("The request has timed out."));
            }
            catch (Exception ex)
            {
                return BadRequest(new NuciApiErrorResponse(ex));
            }
        }
    }
}
