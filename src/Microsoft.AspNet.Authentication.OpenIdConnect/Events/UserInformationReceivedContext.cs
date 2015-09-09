// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNet.Authentication.OpenIdConnect
{
    public class UserInformationReceivedContext : BaseControlContext<OpenIdConnectAuthenticationOptions>
    {
        public UserInformationReceivedContext(HttpContext context, OpenIdConnectAuthenticationOptions options)
            : base(context, options)
        {
        }

        public OpenIdConnectMessage ProtocolMessage { get; set; }

        public JObject User { get; set; }
    }
}
