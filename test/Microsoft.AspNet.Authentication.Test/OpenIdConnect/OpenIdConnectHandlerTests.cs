// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authentication.OpenIdConnect;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.AspNet.TestHost;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Microsoft.Framework.WebEncoders;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.AspNet.Authentication.Tests.OpenIdConnect
{
    /// <summary>
    /// These tests are designed to test OpenIdConnectAuthenticationHandler.
    /// </summary>
    public class OpenIdConnectHandlerTests
    {
        private const string nonceForJwt = "abc";
        private static SecurityToken specCompliantJwt = new JwtSecurityToken("issuer", "audience", new List<Claim> { new Claim("iat", EpochTime.GetIntDate(DateTime.UtcNow).ToString()), new Claim("nonce", nonceForJwt) }, DateTime.UtcNow, DateTime.UtcNow + TimeSpan.FromDays(1));
        private const string ExpectedStateParameter = "expectedState";

        /// <summary>
        /// Sanity check that logging is filtering, hi / low water marks are checked
        /// </summary>
        [Fact]
        public void LoggingLevel()
        {
            var logger = new InMemoryLogger(LogLevel.Debug);
            logger.IsEnabled(LogLevel.Critical).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Debug).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Error).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Information).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Verbose).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Warning).ShouldBe<bool>(true);

            logger = new InMemoryLogger(LogLevel.Critical);
            logger.IsEnabled(LogLevel.Critical).ShouldBe<bool>(true);
            logger.IsEnabled(LogLevel.Debug).ShouldBe<bool>(false);
            logger.IsEnabled(LogLevel.Error).ShouldBe<bool>(false);
            logger.IsEnabled(LogLevel.Information).ShouldBe<bool>(false);
            logger.IsEnabled(LogLevel.Verbose).ShouldBe<bool>(false);
            logger.IsEnabled(LogLevel.Warning).ShouldBe<bool>(false);
        }

        [Theory, MemberData("AuthenticateCoreStateDataSet")]
        public async Task AuthenticateCoreState(Action<OpenIdConnectOptions> action, OpenIdConnectMessage message)
        {
            var handler = new OpenIdConnectHandlerForTestingAuthenticate();
            var server = CreateServer(new ConfigureOptions<OpenIdConnectOptions>(action), UrlEncoder.Default, handler);
            await server.CreateClient().PostAsync("http://localhost", new FormUrlEncodedContent(message.Parameters.Where(pair => pair.Value != null)));
        }

        public static TheoryData<Action<OpenIdConnectOptions>, OpenIdConnectMessage> AuthenticateCoreStateDataSet
        {
            get
            {
                var formater = new AuthenticationPropertiesFormaterKeyValue();
                var properties = new AuthenticationProperties();
                var dataset = new TheoryData<Action<OpenIdConnectOptions>, OpenIdConnectMessage>();

                // expected user state is added to the message.Parameters.Items[ExpectedStateParameter]
                // Userstate == null
                var message = new OpenIdConnectMessage();
                message.State = UrlEncoder.Default.UrlEncode(formater.Protect(properties));
                message.Code = Guid.NewGuid().ToString();
                message.Parameters.Add(ExpectedStateParameter, null);
                dataset.Add(SetStateOptions, message);

                // Userstate != null
                message = new OpenIdConnectMessage();
                properties.Items.Clear();
                var userstate = Guid.NewGuid().ToString();
                message.Code = Guid.NewGuid().ToString();
                properties.Items.Add(OpenIdConnectDefaults.UserstatePropertiesKey, userstate);
                message.State = UrlEncoder.Default.UrlEncode(formater.Protect(properties));
                message.Parameters.Add(ExpectedStateParameter, userstate);
                dataset.Add(SetStateOptions, message);
                return dataset;
            }
        }

        // Setup an event to check for expected state.
        // The state gets set by the runtime after the 'MessageReceivedContext'
        private static void SetStateOptions(OpenIdConnectOptions options)
        {
            options.AuthenticationScheme = "OpenIdConnectHandlerTest";
            options.ConfigurationManager = TestUtilities.DefaultOpenIdConnectConfigurationManager;
            options.ClientId = Guid.NewGuid().ToString();
            options.StateDataFormat = new AuthenticationPropertiesFormaterKeyValue();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthorizationCodeRedeemed = context =>
                {
                    context.HandleResponse();
                    if (context.ProtocolMessage.State == null && !context.ProtocolMessage.Parameters.ContainsKey(ExpectedStateParameter))
                        return Task.FromResult<object>(null);

                    if (context.ProtocolMessage.State == null || !context.ProtocolMessage.Parameters.ContainsKey(ExpectedStateParameter))
                        Assert.True(false, "(context.ProtocolMessage.State=!= null || !context.ProtocolMessage.Parameters.ContainsKey(expectedState)");

                    Assert.Equal(context.ProtocolMessage.State, context.ProtocolMessage.Parameters[ExpectedStateParameter]);
                    return Task.FromResult<object>(null);
                }
            };
        }

        [Theory, MemberData("AuthenticateCoreDataSet")]
        public async Task AuthenticateCore(LogLevel logLevel, int[] expectedLogIndexes, Action<OpenIdConnectOptions> action, OpenIdConnectMessage message)
        {
            var errors = new List<Tuple<LogEntry, LogEntry>>();
            var expectedLogs = LoggingUtilities.PopulateLogEntries(expectedLogIndexes);
            var handler = new OpenIdConnectHandlerForTestingAuthenticate();
            var loggerFactory = new InMemoryLoggerFactory(logLevel);
            var server = CreateServer(new ConfigureOptions<OpenIdConnectOptions>(action), UrlEncoder.Default, loggerFactory, handler);

            await server.CreateClient().PostAsync("http://localhost", new FormUrlEncodedContent(message.Parameters));
            LoggingUtilities.CheckLogs(loggerFactory.Logger.Logs, expectedLogs, errors);
            Debug.WriteLine(LoggingUtilities.LoggingErrors(errors));
            Assert.True(errors.Count == 0, LoggingUtilities.LoggingErrors(errors));
        }

        public static TheoryData<LogLevel, int[], Action<OpenIdConnectOptions>, OpenIdConnectMessage> AuthenticateCoreDataSet
        {
            get
            {
                var formater = new AuthenticationPropertiesFormaterKeyValue();
                var dataset = new TheoryData<LogLevel, int[], Action<OpenIdConnectOptions>, OpenIdConnectMessage>();
                var properties = new AuthenticationProperties();
                var message = new OpenIdConnectMessage();
                var validState = UrlEncoder.Default.UrlEncode(formater.Protect(properties));
                message.State = validState;

                // MessageReceived - Handled / Skipped
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 2 }, MessageReceivedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 2 }, MessageReceivedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, MessageReceivedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 3 }, MessageReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 3 }, MessageReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, MessageReceivedSkippedOptions, message);

                // State - null, empty string, invalid
                message = new OpenIdConnectMessage();
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 7, 28 }, StateNullOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 7 }, StateNullOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, StateNullOptions, message);

                message = new OpenIdConnectMessage();
                message.State = string.Empty;
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 7, 28 }, StateEmptyOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 7 }, StateEmptyOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, StateEmptyOptions, message);

                message = new OpenIdConnectMessage();
                message.State = Guid.NewGuid().ToString();
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 5 }, StateInvalidOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 5 }, StateInvalidOptions, message);
                dataset.Add(LogLevel.Error, new int[] { 5 }, StateInvalidOptions, message);

                // OpenIdConnectMessage.Error != null
                message = new OpenIdConnectMessage();
                message.Error = "Error";
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 6, 17, 18 }, MessageWithErrorOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 6, 17, 18 }, MessageWithErrorOptions, message);
                dataset.Add(LogLevel.Error, new int[] { 6, 17 }, MessageWithErrorOptions, message);

                // SecurityTokenReceived - Handled / Skipped 
                message = new OpenIdConnectMessage();
                message.IdToken = "invalid";
                message.State = validState;
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 8 }, SecurityTokenReceivedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 8 }, SecurityTokenReceivedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, SecurityTokenReceivedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 9 }, SecurityTokenReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 9 }, SecurityTokenReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, SecurityTokenReceivedSkippedOptions, message);

                // SecurityTokenValidation - ReturnsNull, Throws, Validates
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 11, 17, 18 }, SecurityTokenValidatorCannotReadToken, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 11, 17, 18 }, SecurityTokenValidatorCannotReadToken, message);
                dataset.Add(LogLevel.Error, new int[] { 11, 17 }, SecurityTokenValidatorCannotReadToken, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 17, 21, 18 }, SecurityTokenValidatorThrows, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 17, 21, 18 }, SecurityTokenValidatorThrows, message);
                dataset.Add(LogLevel.Error, new int[] { 17 }, SecurityTokenValidatorThrows, message);

                message.Nonce = nonceForJwt;
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20 }, SecurityTokenValidatorValidatesAllTokens, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7 }, SecurityTokenValidatorValidatesAllTokens, message);
                dataset.Add(LogLevel.Error, new int[] { }, SecurityTokenValidatorValidatesAllTokens, message);

                // SecurityTokenValidation - Handled / Skipped
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 12 }, SecurityTokenValidatedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 12 }, SecurityTokenValidatedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, SecurityTokenValidatedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 13 }, SecurityTokenValidatedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 13 }, SecurityTokenValidatedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, SecurityTokenValidatedSkippedOptions, message);

                // AuthenticationCodeReceived - Handled / Skipped 
                message = new OpenIdConnectMessage();
                message.Code = Guid.NewGuid().ToString();
                message.State = validState;
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 14, 15 }, AuthorizationCodeReceivedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 15 }, AuthorizationCodeReceivedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, AuthorizationCodeReceivedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 14, 16 }, AuthorizationCodeReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 16 }, AuthorizationCodeReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, AuthorizationCodeReceivedSkippedOptions, message);

                message = new OpenIdConnectMessage();
                message.Code = Guid.NewGuid().ToString();
                message.State = validState;
                message.IdToken = "test token";
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 14, 15 }, AuthorizationCodeReceivedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 15 }, AuthorizationCodeReceivedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, AuthorizationCodeReceivedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 7, 20, 14, 16 }, AuthorizationCodeReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 7, 16 }, AuthorizationCodeReceivedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, AuthorizationCodeReceivedSkippedOptions, message);

                // CodeReceivedAndRedeemed and GetUserInformationFromUIEndpoint
                message = new OpenIdConnectMessage();
                message.IdToken = null;
                message.Code = Guid.NewGuid().ToString();
                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 7, 14, 23, 25, 26 }, CodeReceivedAndRedeemedHandledOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 7, 26 }, CodeReceivedAndRedeemedHandledOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, CodeReceivedAndRedeemedHandledOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 7, 14, 23, 25, 27 }, CodeReceivedAndRedeemedSkippedOptions, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 7, 27 }, CodeReceivedAndRedeemedSkippedOptions, message);
                dataset.Add(LogLevel.Error, new int[] { }, CodeReceivedAndRedeemedSkippedOptions, message);

                dataset.Add(LogLevel.Debug, new int[] { 0, 1, 4, 7, 14, 23, 25, 24, 12 }, GetUserInfoFromUIEndpoint, message);
                dataset.Add(LogLevel.Verbose, new int[] { 4, 7, 12 }, GetUserInfoFromUIEndpoint, message);
                dataset.Add(LogLevel.Error, new int[] { }, GetUserInfoFromUIEndpoint, message);

                return dataset;
            }
        }

#region Configure Options for AuthenticateCore variations

        private static void DefaultOptions(OpenIdConnectOptions options)
        {
            options.AuthenticationScheme = "OpenIdConnectHandlerTest";
            options.SignInScheme = "OpenIdConnectHandlerTest";
            options.ConfigurationManager = TestUtilities.DefaultOpenIdConnectConfigurationManager;
            options.ClientId = Guid.NewGuid().ToString();
            options.StateDataFormat = new AuthenticationPropertiesFormaterKeyValue();
        }

        private static void AuthorizationCodeReceivedHandledOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.ProtocolValidator = MockProtocolValidator();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthorizationCodeReceived = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void AuthorizationCodeReceivedSkippedOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.ProtocolValidator = MockProtocolValidator();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthorizationCodeReceived = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void AuthenticationErrorHandledOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.ProtocolValidator = MockProtocolValidator();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthenticationFailed = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void AuthenticationErrorSkippedOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.ProtocolValidator = MockProtocolValidator();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthenticationFailed = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void MessageReceivedHandledOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnMessageReceived = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void CodeReceivedAndRedeemedHandledOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.ResponseType = OpenIdConnectResponseTypes.Code;
            options.StateDataFormat = new AuthenticationPropertiesFormaterKeyValue();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthorizationCodeRedeemed = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void CodeReceivedAndRedeemedSkippedOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.ResponseType = OpenIdConnectResponseTypes.Code;
            options.StateDataFormat = new AuthenticationPropertiesFormaterKeyValue();
            options.Events = new OpenIdConnectEvents()
            {
                OnAuthorizationCodeRedeemed = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void GetUserInfoFromUIEndpoint(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.ResponseType = OpenIdConnectResponseTypes.Code;
            options.ProtocolValidator.RequireNonce = false;
            options.StateDataFormat = new AuthenticationPropertiesFormaterKeyValue();
            options.GetClaimsFromUserInfoEndpoint = true;
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.Events = new OpenIdConnectEvents()
            {
                OnSecurityTokenValidated = (context) =>
                {
                    var claimValue = context.AuthenticationTicket.Principal.FindFirst("test claim");
                    Assert.Equal(claimValue.Value, "test value");
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }
        private static void MessageReceivedSkippedOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnMessageReceived = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void MessageWithErrorOptions(OpenIdConnectOptions options)
        {
            AuthenticationErrorHandledOptions(options);
        }

        private static void SecurityTokenReceivedHandledOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnSecurityTokenReceived = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void SecurityTokenReceivedSkippedOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnSecurityTokenReceived = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static ISecurityTokenValidator MockSecurityTokenValidator()
        {
            var mockValidator = new Mock<ISecurityTokenValidator>();
            mockValidator.Setup(v => v.ValidateToken(It.IsAny<string>(), It.IsAny<TokenValidationParameters>(), out specCompliantJwt)).Returns(new ClaimsPrincipal());
            mockValidator.Setup(v => v.CanReadToken(It.IsAny<string>())).Returns(true);
            return mockValidator.Object;
        }

        private static OpenIdConnectProtocolValidator MockProtocolValidator()
        {
            var mockProtocolValidator = new Mock<OpenIdConnectProtocolValidator>();
            mockProtocolValidator.Setup(v => v.Validate(It.IsAny<JwtSecurityToken>(), It.IsAny<OpenIdConnectProtocolValidationContext>()));
            return mockProtocolValidator.Object;
        }

        private static void SecurityTokenValidatorCannotReadToken(OpenIdConnectOptions options)
        {
            AuthenticationErrorHandledOptions(options);
            var mockValidator = new Mock<ISecurityTokenValidator>();
            SecurityToken jwt = null;
            mockValidator.Setup(v => v.ValidateToken(It.IsAny<string>(), It.IsAny<TokenValidationParameters>(), out jwt)).Returns(new ClaimsPrincipal());
            mockValidator.Setup(v => v.CanReadToken(It.IsAny<string>())).Returns(false);
            options.SecurityTokenValidator = mockValidator.Object;
        }

        private static void SecurityTokenValidatorThrows(OpenIdConnectOptions options)
        {
            AuthenticationErrorHandledOptions(options);
            var mockValidator = new Mock<ISecurityTokenValidator>();
            SecurityToken jwt = null;
            mockValidator.Setup(v => v.ValidateToken(It.IsAny<string>(), It.IsAny<TokenValidationParameters>(), out jwt)).Throws<SecurityTokenSignatureKeyNotFoundException>();
            mockValidator.Setup(v => v.CanReadToken(It.IsAny<string>())).Returns(true);
            options.SecurityTokenValidator = mockValidator.Object;
        }

        private static void SecurityTokenValidatorValidatesAllTokens(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
            options.SecurityTokenValidator = MockSecurityTokenValidator();
            options.ProtocolValidator.RequireTimeStampInNonce = false;
            options.ProtocolValidator.RequireNonce = false;
        }

        private static void SecurityTokenValidatedHandledOptions(OpenIdConnectOptions options)
        {
            SecurityTokenValidatorValidatesAllTokens(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnSecurityTokenValidated = (context) =>
                {
                    context.HandleResponse();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void SecurityTokenValidatedSkippedOptions(OpenIdConnectOptions options)
        {
            SecurityTokenValidatorValidatesAllTokens(options);
            options.Events = new OpenIdConnectEvents()
            {
                OnSecurityTokenValidated = (context) =>
                {
                    context.SkipToNextMiddleware();
                    return Task.FromResult<object>(null);
                }
            };
        }

        private static void StateNullOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
        }

        private static void StateEmptyOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
        }

        private static void StateInvalidOptions(OpenIdConnectOptions options)
        {
            DefaultOptions(options);
        }

#endregion

        private static Task EmptyTask() { return Task.FromResult(0); }

        private static TestServer CreateServer(ConfigureOptions<OpenIdConnectOptions> options, IUrlEncoder encoder, OpenIdConnectHandler handler = null)
        {
            return TestServer.Create(
                app =>
                {
                    app.UseMiddleware<OpenIdConnectMiddlewareForTestingAuthenticate>(options, encoder, handler);
                    app.Use(async (context, next) =>
                    {
                        await next();
                    });
                },
                services =>
                {
                    services.AddWebEncoders();
                    services.AddDataProtection();
                }
            );
        }

        private static TestServer CreateServer(ConfigureOptions<OpenIdConnectOptions> configureOptions, IUrlEncoder encoder, ILoggerFactory loggerFactory, OpenIdConnectHandler handler = null)
        {
            return TestServer.Create(
                app =>
                {
                    app.UseMiddleware<OpenIdConnectMiddlewareForTestingAuthenticate>(configureOptions, encoder, loggerFactory, handler);
                    app.Use(async (context, next) =>
                    {
                        await next();
                    });
                },
                services =>
                {
                    services.AddWebEncoders();
                    services.AddDataProtection();
                }
            );
        }
    }
}
