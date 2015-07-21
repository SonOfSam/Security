// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Authentication.OAuthBearer;
using Microsoft.Framework.Internal;
using Microsoft.Framework.OptionsModel;

namespace Microsoft.AspNet.Builder
{
    /// <summary>
    /// Extension methods to add OAuth Bearer authentication capabilities to an HTTP application pipeline
    /// </summary>
    public static class OAuthBearerAppBuilderExtensions
    {
        /// <summary>
        /// Adds Bearer token processing to an HTTP application pipeline. This middleware understands appropriately
        /// formatted and secured tokens which appear in the request header. If the Options.AutomaticAuthentication is true, the
        /// claims within the bearer token are added to the current request's IPrincipal User. If the Options.AutomaticAuthentication 
        /// is false, then the current request is not modified, but IAuthenticationManager AuthenticateAsync may be used at
        /// any time to obtain the claims from the request's bearer token.
        /// See also http://tools.ietf.org/html/rfc6749
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <param name="configureOptions">Configures the options which control the processing of the bearer header.</param>
        /// <returns>The application builder</returns>
        public static IApplicationBuilder UseOAuthBearerAuthentication([NotNull] this IApplicationBuilder app, [NotNull] Action<OAuthBearerAuthenticationOptions> configureOptions)
        {
            var options = new OAuthBearerAuthenticationOptions();
            configureOptions(options);
            return app.UseOAuthBearerAuthentication(options);
        }

        /// <summary>
        /// Adds Bearer token processing to an HTTP application pipeline. This middleware understands appropriately
        /// formatted and secured tokens which appear in the request header. If the Options.AutomaticAuthentication is true, the
        /// claims within the bearer token are added to the current request's IPrincipal User. If the Options.AutomaticAuthentication 
        /// is false, then the current request is not modified, but IAuthenticationManager AuthenticateAsync may be used at
        /// any time to obtain the claims from the request's bearer token.
        /// See also http://tools.ietf.org/html/rfc6749
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <param name="options">Options which control the processing of the bearer header.</param>
        /// <returns>The application builder</returns>
        public static IApplicationBuilder UseOAuthBearerAuthentication([NotNull] this IApplicationBuilder app, [NotNull] OAuthBearerAuthenticationOptions options)
        {
            return app.UseMiddleware<OAuthBearerAuthenticationMiddleware>(options);
        }
    }
}
