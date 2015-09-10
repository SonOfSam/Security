// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Microsoft.AspNet.Authentication.OpenIdConnect
{
    public class IdTokenValidatedContext : BaseControlContext<OpenIdConnectAuthenticationOptions>
    {
        public IdTokenValidatedContext(HttpContext context, OpenIdConnectAuthenticationOptions options)
            : base(context, options)
        {
        }

        public OpenIdConnectMessage ProtocolMessage { get; set; }
    }
}
