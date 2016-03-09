﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Http.Features.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace idunno.Authentication
{
    internal class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        private const string _Scheme = "Basic";

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorizationHeader = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader))
            {
                return AuthenticateResult.Failed("No authorization header.");
            }

            if (!authorizationHeader.StartsWith(_Scheme + ' ', StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Success(ticket: null);
            }

            string encodedCredentials = encodedCredentials = authorizationHeader.Substring(_Scheme.Length).Trim();
            
            if (string.IsNullOrEmpty(encodedCredentials))
            {
                const string noCredentialsMessage = "No credentials";
                Logger.LogInformation(noCredentialsMessage);
                return AuthenticateResult.Failed(noCredentialsMessage);
            }

            try
            {
                string decodedCredentials = string.Empty;
                try
                {
                    decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                }
                catch (Exception ex)
                {
                    Logger.LogInformation($"Failed to decode credentials : {encodedCredentials}", ex);
                    throw;
                }

                var delimiterIndex = decodedCredentials.IndexOf(':');
                if (delimiterIndex == -1)
                {
                    const string missingDelimiterMessage = "Invalid credentials, missing delimiter.";
                    Logger.LogInformation(missingDelimiterMessage);
                    return AuthenticateResult.Failed(missingDelimiterMessage);
                }

                var username = decodedCredentials.Substring(0, delimiterIndex);
                var password = decodedCredentials.Substring(delimiterIndex + 1);

                var validateCredentialsContext = new ValidateCredentialsContext(Context, Options)
                {
                    Username = username,
                    Password = password
                };

                await Options.Events.ValidateCredentials(validateCredentialsContext);

                if (validateCredentialsContext.HandledResponse)
                {
                    if (validateCredentialsContext.AuthenticationTicket != null)
                    {
                        Logger.LogInformation($"Credentials validated for {username}");
                        return AuthenticateResult.Success(validateCredentialsContext.AuthenticationTicket);
                    }
                    else
                    {
                        Logger.LogInformation($"Credential validation failed for {username}");
                        return AuthenticateResult.Failed("Invalid credentials.");
                    }
                }

                const string validationNotHandled = "Credential validation not handled.";
                Logger.LogError(validationNotHandled);
                return AuthenticateResult.Failed(validationNotHandled);

            }
            catch (Exception ex)
            {
                var authenticationFailedContext = new AuthenticationFailedContext(Context, Options)
                {
                    Exception = ex
                };

                await Options.Events.AuthenticationFailed(authenticationFailedContext);
                if (authenticationFailedContext.HandledResponse)
                {
                    return AuthenticateResult.Success(authenticationFailedContext.AuthenticationTicket);
                }
                if (authenticationFailedContext.Skipped)
                {
                    return AuthenticateResult.Success(ticket: null);
                }

                throw;
            }
        }

        protected override Task<bool> HandleUnauthorizedAsync(ChallengeContext context)
        {
            Response.StatusCode = 401;

            var headerValue = _Scheme + $" realm=\"{Options.Realm}\""; ;
            Response.Headers.Add(HeaderNames.WWWAuthenticate, headerValue);

            return Task.FromResult(true);
        }

        protected override Task<bool> HandleForbiddenAsync(ChallengeContext context)
        {
            Response.StatusCode = 403;
            return Task.FromResult(true);
        }

        protected override Task HandleSignOutAsync(SignOutContext context)
        {
            throw new NotSupportedException();
        }

        protected override Task HandleSignInAsync(SignInContext context)
        {
            throw new NotSupportedException();
        }
    }
}