using System;
using System.Net;
using System.Threading.Tasks;
using DcrdClient;
using Lykke.Service.Decred.Api.Common;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Lykke.Service.Decred.Api.Middleware
{
    public class ApiErrorHandler
    {
        private readonly RequestDelegate _next;

        public ApiErrorHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }

            catch (BusinessException ex) when (ex.Reason == ErrorReason.BadRequest)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }

            catch (BusinessException ex) when (ex.Reason == ErrorReason.InvalidAddress)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }

            catch (ArgumentException ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }

            catch (JsonReaderException ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }

            catch (Paymetheus.Decred.Wallet.AddressException ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
            }

            catch (DcrdException ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.InternalServerError);
            }

            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.InternalServerError);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context,  Exception exception, HttpStatusCode statusCode)
        {
            var result = JsonConvert.SerializeObject(new { errorMessage = exception.ToString() });
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) statusCode;
            return context.Response.WriteAsync(result);
        }
    }
}
