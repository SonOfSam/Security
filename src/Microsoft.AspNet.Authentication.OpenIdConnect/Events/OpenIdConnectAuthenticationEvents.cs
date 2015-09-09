// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.AspNet.Authentication.OpenIdConnect
{
    /// <summary>
    /// Specifies events which the <see cref="OpenIdConnectAuthenticationMiddleware" />invokes to enable developer control over the authentication process.
    /// </summary>
    public class OpenIdConnectAuthenticationEvents : IOpenIdConnectAuthenticationEvents
    {
        /// <summary>
        /// Invoked when the authentication process completes.
        /// </summary>
        public Func<AuthenticationCompletedContext, Task> OnAuthenticationCompleted { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked if exceptions are thrown during request processing. The exceptions will be re-thrown after this event unless suppressed.
        /// </summary>
        public Func<AuthenticationFailedContext, Task> OnAuthenticationFailed { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked after security token validation if an authorization code is present in the protocol message.
        /// </summary>
        public Func<AuthorizationCodeReceivedContext, Task> OnAuthorizationCodeReceived { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked after "authorization code" is redeemed for tokens at the token endpoint.
        /// </summary>
        public Func<AuthorizationCodeRedeemedContext, Task> OnAuthorizationCodeRedeemed { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked when a protocol message is first received.
        /// </summary>
        public Func<MessageReceivedContext, Task> OnMessageReceived { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked before redirecting to the identity provider to authenticate.
        /// </summary>
        public Func<RedirectForAuthenticationContext, Task> OnRedirectForAuthentication { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked before redirecting to the identity provider to sign out.
        /// </summary>
        public Func<RedirectForSignOutContext, Task> OnRedirectForSignOut { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked with the security token that has been extracted from the protocol message.
        /// </summary>
        public Func<SecurityTokenReceivedContext, Task> OnSecurityTokenReceived { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked after the security token has passed validation and a ClaimsIdentity has been generated.
        /// </summary>
        public Func<SecurityTokenValidatedContext, Task> OnSecurityTokenValidated { get; set; } = context => Task.FromResult(0);

        /// <summary>
        /// Invoked when user information is retrieved from the UserInfoEndpoint.
        /// </summary>
        public Func<UserInformationReceivedContext, Task> OnUserInformationReceived { get; set; } = context => Task.FromResult(0);

        public virtual Task AuthenticationCompleted(AuthenticationCompletedContext context) => OnAuthenticationCompleted(context);

        public virtual Task AuthenticationFailed(AuthenticationFailedContext context) => OnAuthenticationFailed(context);

        public virtual Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context) => OnAuthorizationCodeReceived(context);

        public virtual Task AuthorizationCodeRedeemed(AuthorizationCodeRedeemedContext context) => OnAuthorizationCodeRedeemed(context);

        public virtual Task MessageReceived(MessageReceivedContext context) => OnMessageReceived(context);

        public virtual Task RedirectForAuthentication(RedirectForAuthenticationContext context) => OnRedirectForAuthentication(context);

        public virtual Task RedirectForSignOut(RedirectForSignOutContext context) => OnRedirectForSignOut(context);

        public virtual Task SecurityTokenReceived(SecurityTokenReceivedContext context) => OnSecurityTokenReceived(context);

        public virtual Task SecurityTokenValidated(SecurityTokenValidatedContext context) => OnSecurityTokenValidated(context);

        public virtual Task UserInformationReceived(UserInformationReceivedContext context) => OnUserInformationReceived(context);
    }
}