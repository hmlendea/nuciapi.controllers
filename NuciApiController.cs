using System;

using Microsoft.AspNetCore.Mvc;
using System.Security;
using NuciAPI.Requests;
using NuciAPI.Responses;

namespace NuciAPI.Controllers
{
    public abstract class NuciApiController : ControllerBase
    {
        protected ActionResult ProcessRequest<TRequest>(TRequest request, Action action)
            where TRequest : NuciApiRequest
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            try
            {
                action();

                return Ok(NuciApiSuccessResponse.Default);
            }
            catch (SecurityException ex)
            {
                return Unauthorized(new NuciApiErrorResponse(ex));
            }
            catch (Exception ex)
            {
                return BadRequest(new NuciApiErrorResponse(ex));
            }
        }

        protected ActionResult ProcessRequest<TRequest, TResponse>(TRequest request, Func<TResponse> action)
            where TRequest : NuciApiRequest
            where TResponse : NuciApiResponse
        {
            if (request is null)
            {
                return BadRequest(NuciApiErrorResponse.InvalidRequest);
            }

            try
            {
                TResponse response = action();

                if (response is null)
                {
                    if (Request.Method.Equals("GET"))
                    {
                        return NotFound("Resource not found.");
                    }
                }

                return Ok(action());
            }
            catch (SecurityException ex)
            {
                return Unauthorized(new NuciApiErrorResponse(ex));
            }
            catch (Exception ex)
            {
                return BadRequest(new NuciApiErrorResponse(ex));
            }
        }
    }
}
