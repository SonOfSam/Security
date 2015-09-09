// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Http;
using Microsoft.Framework.Internal;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Microsoft.AspNet.Authentication.OpenIdConnect
{
    /// <summary>
    /// When a user configures the <see cref="OpenIdConnectAuthenticationMiddleware"/> to be notified prior to redirecting to an IdentityProvider
    /// an instance of <see cref="RedirectForSignOutContext"/> is passed to the 'RedirectForSignOut" event.
    /// </summary>
    public class RedirectForSignOutContext : BaseControlContext<OpenIdConnectAuthenticationOptions>
    {
        public RedirectForSignOutContext([NotNull] HttpContext context, [NotNull] OpenIdConnectAuthenticationOptions options)
            : base(context, options)
        {
        }

        /// <summary>
        /// Gets or sets the <see cref="OpenIdConnectMessage"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">if 'value' is null.</exception>
        public OpenIdConnectMessage ProtocolMessage { get; [param: NotNull] set; }
    }
}