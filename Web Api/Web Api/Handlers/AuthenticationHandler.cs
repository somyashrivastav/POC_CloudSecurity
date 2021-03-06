﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Web_Api.Controllers;
using Web_Api.Helper;
using Web_Api.Infrastructure;
using Web_Api.Models;
using Web_Api.Security;

namespace Web_Api.Handlers
{
    public class AuthenticationHandler : DelegatingHandler
    {

        private Dictionary<string, List<string>> _public_No_JWT_List = new Dictionary<string, List<string>>()
        {
            {
                "/api",
                new List<string>() { HttpMethod.Get.Method }
            },
            {
                 "/api/AuthToken",
                 new List<string>() { HttpMethod.Post.Method }
            },
             {
                 "/api/refresh",
                 new List<string>() { HttpMethod.Post.Method }
            },
           
            {
                 "/weatherforecast",
                 new List<string>() { HttpMethod.Get.Method }
            },
            {
                 "/api/User/AddEmployee",
                 new List<string>() { HttpMethod.Post.Method }
            },
            {
                 "/api/User/Login",
                 new List<string>() { HttpMethod.Post.Method }
            }
        };


        private readonly RequestDelegate _next;
        public AuthenticationHandler(RequestDelegate next)
        {
            this._next = next;
        }
        //private readonly AuthenticationHandler _options;
        //public AuthenticationHandler(RequestDelegate next, IOptions<AuthenticationHandler> options)
        //{
        //    this._next = next;
        //    this._options = options.Value;

        //}

        public async Task Invoke(HttpContext context)
        {
            //}
            //public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
            //{
            JWTToken oJwt = null;
            var response = new HttpResponseMessage();
            try
            {
                // skip the options requests 
                bool listContains = false;
                foreach (KeyValuePair<string, List<string>> item in _public_No_JWT_List)
                {
                    if (context.Request.Path.Value.ToLower().TrimEnd('/').Equals(item.Key.ToLower()))
                    {
                        listContains = item.Value.Contains(context.Request.Method);
                        break;
                    }
                }
                // Determine whether the client is accessing a Public controller, if not check for token
                if ((!listContains))
                {
                    var headers = context.Request.Headers;
                    string token = string.Empty;

                    if (headers.ContainsKey(JWTToken.Authorization))
                        token = headers[JWTToken.Authorization].First();
                    try
                    {
                        var hmacKey = AESServices.UserHmacKey(Constants.UserNumber, 3);
                        //var hmacKey = AESServices.UserHmacKey(TokenService.userNumber, 3);
                        //var t = token.AESStringDecryption(Constants.UserNumber);
                        oJwt = JWTToken.ParseJwtToken(token.AESStringDecryption(Constants.UserNumber), ref hmacKey);
                        //oJwt = JWTToken.ParseJwtToken(token.AESStringDecryption(TokenService.userNumber), ref hmacKey);

                        // Set Principal and store JWT
                        ClaimsIdentity id = new ClaimsIdentity(oJwt.Claims, JWTToken.JWT_ID);
                        id.BootstrapContext = oJwt;
                        ClaimsPrincipal principal = new ClaimsPrincipal(id);
                        Thread.CurrentPrincipal = new ClaimsPrincipal(id);
                        var x = id.Claims.Where(x=>x.Type== "isad").First();
                        var role= id.Claims.Where(x => x.Type == "role").First();
                        if (x.Value.Contains("False"))
                        {
                            var routeValue = context.Request.Path.Value.Split("/").Last();
                            string value = Permissions.GetPermission(routeValue);
                            var checkAccess = Bitwise.IsAttributeInCombination(long.Parse(value),long.Parse(role.Value));

                            if (!checkAccess)
                            {
                                throw new Exception("You don't have rights for this method.");
                            }
                        }
                        else
                        {
                            var routeValue = context.Request.Path.Value.Split("/").Last();
                            string value = Permissions.GetPermission(routeValue);
                            var checkAccess = Bitwise.IsAttributeInCombination(long.Parse(value), long.Parse(role.Value));

                            if (!checkAccess)
                            {
                                throw new Exception("You don't have rights for this method.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
                await _next(context);
            }
            catch (Exception ex)
            {
                HttpResponseMessage exceptionResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
                context.Response.StatusCode = Convert.ToInt32(HttpStatusCode.BadRequest);
                context.Response.ContentType = "application/json; charset=utf-8";
                var result = ex.ToString() + "An error occurred processing your authentication.";
                await context.Response.WriteAsync(result);
            }
            }
    }
}
