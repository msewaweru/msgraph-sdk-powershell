﻿// ------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------

namespace Microsoft.Graph.Authentication.Core
{
    using Microsoft.Graph.Auth;
    using Microsoft.Graph.PowerShell.Authentication;
    using Microsoft.Graph.PowerShell.Authentication.Core;
    using Microsoft.Graph.PowerShell.Authentication.Helpers;
    using Microsoft.Identity.Client;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Authenticator class for handling sign-ins and sign-outs.
    /// </summary>
    public static class Authenticator
    {
        /// <summary>
        /// Authenticates the client using the provided <see cref="IAuthContext"/>.
        /// </summary>
        /// <param name="authContext">The <see cref="IAuthContext"/> to authenticate.</param>
        /// <param name="forceRefresh">Whether or not to force refresh a token if one exists.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<(IAuthContext context, AuthError authError)> AuthenticateAsync(IAuthContext authContext, bool forceRefresh, CancellationToken cancellationToken)
        {
            try
            {
                // Gets a static instance of IAuthenticationProvider when the client app hasn't changed.
                IAuthenticationProvider authProvider = AuthenticationHelpers.GetAuthProvider(authContext);
                IClientApplicationBase clientApplication = null;

                if (authContext.AuthType == AuthenticationType.Delegated && authContext.UseDeviceAuth)
                {
                    clientApplication = (authProvider as DeviceCodeProvider).ClientApplication;
                }
                if (authContext.AuthType == AuthenticationType.Delegated && !authContext.UseDeviceAuth)
                {
                    clientApplication = (authProvider as InteractiveAuthenticationProvider).ClientApplication;
                }
                if (authContext.AuthType == AuthenticationType.AppOnly)
                {
                    clientApplication = (authProvider as ClientCredentialProvider).ClientApplication;
                }

                // Incremental scope consent without re-instantiating the auth provider. We will use a static instance.
                GraphRequestContext graphRequestContext = new GraphRequestContext();
                graphRequestContext.CancellationToken = cancellationToken;
                graphRequestContext.MiddlewareOptions = new Dictionary<string, IMiddlewareOption>
                {
                    {
                        typeof(AuthenticationHandlerOption).ToString(),
                        new AuthenticationHandlerOption
                        {
                            AuthenticationProviderOption = new AuthenticationProviderOption
                            {
                                Scopes = authContext.Scopes,
                                ForceRefresh = forceRefresh
                            }
                        }
                    }
                };

                // Trigger consent.
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
                httpRequestMessage.Properties.Add(typeof(GraphRequestContext).ToString(), graphRequestContext);
                await authProvider.AuthenticateRequestAsync(httpRequestMessage);

                IAccount account = null;
                if (clientApplication != null)
                {
                    // Only get accounts when we are using MSAL to get an access token.
                    IEnumerable<IAccount> accounts = clientApplication.GetAccountsAsync().GetAwaiter().GetResult();
                    account = accounts.FirstOrDefault();
                }

                JwtHelpers.DecodeJWT(httpRequestMessage.Headers.Authorization?.Parameter, account, ref authContext);
                return (authContext, new AuthError(AuthErrorType.None, null));
            }
            catch (AuthenticationException authEx)
            {
                //Interactive Authentication Failure: Could Not Open Browser, fallback to DeviceAuth
                if (IsUnableToOpenWebPageError(authEx))
                {
                    authContext.UseDeviceAuth = true;
                    //ReAuthenticate using DeviceCode as fallback.
                    var (retryAuthContext, retryAuthError) = await AuthenticateAsync(authContext, forceRefresh, cancellationToken);
                    //Indicate that this was a Fallback
                    retryAuthError = new AuthError(AuthErrorType.FallBack, retryAuthError.Exception);
                    return (retryAuthContext, retryAuthError);
                }
                // DeviceCode Authentication Failure: Timeout
                if (authEx.InnerException is TaskCanceledException && cancellationToken.IsCancellationRequested)
                {
                    // DeviceCodeTimeout
                    var deviceCode = new Exception(string.Format(
                            CultureInfo.CurrentCulture,
                            ErrorConstants.Message.DeviceCodeTimeout,
                            Constants.MaxDeviceCodeTimeOut));
                    return (authContext, new AuthError(AuthErrorType.DeviceCodeFailure, deviceCode.InnerException ?? deviceCode));
                }
                //Something Unknown Went Wrong
                return (authContext, new AuthError(AuthErrorType.Unknown, authEx.InnerException ?? authEx));
            }
            catch (Exception ex)
            {
                return (authContext, new AuthError(AuthErrorType.Unknown, ex.InnerException ?? ex));
            }
        }
        /// <summary>
        /// Signs out of the provided <see cref="IAuthContext"/>.
        /// </summary>
        /// <param name="authContext">The <see cref="IAuthContext"/> to sign-out from.</param>
        public static void LogOut(IAuthContext authContext)
        {
            AuthenticationHelpers.Logout(authContext);
        }

        private static bool IsUnableToOpenWebPageError(Exception exception)
        {
            return exception.InnerException is MsalClientException clientException &&
                   clientException?.ErrorCode == MsalError.LinuxXdgOpen ||
                   (exception.Message?.ToLower()?.Contains("unable to open a web page") ?? false);
        }
    }
}