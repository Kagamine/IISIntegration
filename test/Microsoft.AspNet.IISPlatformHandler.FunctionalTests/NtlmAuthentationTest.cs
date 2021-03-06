// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNet.Server.Testing;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Framework.Logging;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNet.IISPlatformHandler.FunctionalTests
{
    // Uses ports ranging 5050 - 5060.
    public class NtlmAuthenticationTests
    {
        // TODO: The middleware needs to implement auth handlers.
        [ConditionalTheory, Trait("ServerComparison.FunctionalTests", "ServerComparison.FunctionalTests")]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [InlineData(ServerType.IISExpress, RuntimeFlavor.CoreClr, RuntimeArchitecture.x86, "http://localhost:5050/")]
        [InlineData(ServerType.IISExpress, RuntimeFlavor.Clr, RuntimeArchitecture.x64, "http://localhost:5051/")]
        public async Task NtlmAuthentication(ServerType serverType, RuntimeFlavor runtimeFlavor, RuntimeArchitecture architecture, string applicationBaseUrl)
        {
            var logger = new LoggerFactory()
                            .AddConsole()
                            .CreateLogger($"HttpsHelloWorld:{serverType}:{runtimeFlavor}:{architecture}");

            using (logger.BeginScope("NtlmAuthenticationTest"))
            {
                var deploymentParameters = new DeploymentParameters(Helpers.GetTestSitesPath(), serverType, runtimeFlavor, architecture)
                {
                    ApplicationBaseUriHint = applicationBaseUrl,
                    EnvironmentName = "NtlmAuthentication", // Will pick the Start class named 'StartupNtlmAuthentication'
                    ApplicationHostConfigTemplateContent = (serverType == ServerType.IISExpress) ? File.ReadAllText("NtlmAuthentation.config") : null,
                    SiteName = "NtlmAuthenticationTestSite", // This is configured in the NtlmAuthentication.config
                };

                using (var deployer = ApplicationDeployerFactory.Create(deploymentParameters, logger))
                {
                    var deploymentResult = deployer.Deploy();
                    var httpClientHandler = new HttpClientHandler();
                    var httpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri(deploymentResult.ApplicationBaseUri) };

                    // Request to base address and check if various parts of the body are rendered & measure the cold startup time.
                    var response = await RetryHelper.RetryRequest(() =>
                    {
                        return httpClient.GetAsync(string.Empty);
                    }, logger, deploymentResult.HostShutdownToken);

                    var responseText = await response.Content.ReadAsStringAsync();
                    try
                    {
                        // TODO: Currently we do not implement mixed auth.
                        // https://github.com/aspnet/IISIntegration/issues/1
                        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                        Assert.Contains("NTLM", response.Headers.WwwAuthenticate.ToString());
                        Assert.Contains("Negotiate", response.Headers.WwwAuthenticate.ToString());

                        /*
                        Assert.Equal("Hello World", responseText);

                        responseText = await httpClient.GetStringAsync("/Anonymous");
                        Assert.Equal("Anonymous?True", responseText);

                        response = await httpClient.GetAsync("/Restricted");
                        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                        Assert.Contains("NTLM", response.Headers.WwwAuthenticate.ToString());
                        Assert.Contains("Negotiate", response.Headers.WwwAuthenticate.ToString());

                        response = await httpClient.GetAsync("/RestrictedNTLM");
                        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                        Assert.Contains("NTLM", response.Headers.WwwAuthenticate.ToString());
                        // Note we can't restrict a challenge to a specific auth type, the native auth modules always add themselves.
                        Assert.Contains("Negotiate", response.Headers.WwwAuthenticate.ToString());

                        response = await httpClient.GetAsync("/Forbidden");
                        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                        */
                        httpClientHandler = new HttpClientHandler() { UseDefaultCredentials = true };
                        httpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri(deploymentResult.ApplicationBaseUri) };

                        responseText = await httpClient.GetStringAsync("/Anonymous");
                        Assert.Equal("Anonymous?False", responseText);

                        /*
                        response = await httpClient.GetAsync("/AutoForbid");
                        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

                        responseText = await httpClient.GetStringAsync("/Restricted");
                        Assert.Equal("Negotiate", responseText);

                        responseText = await httpClient.GetStringAsync("/RestrictedNegotiate");
                        Assert.Equal("Negotiate", responseText);

                        response = await httpClient.GetAsync("/RestrictedNTLM");
                        // This isn't a Forbidden because we authenticate with Negotiate and challenge for NTLM.
                        // Note we can't restrict a challenge to a specific auth type, the native auth modules always add themselves,
                        // so both Negotiate and NTLM get sent again.
                        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                        */
                    }
                    catch (XunitException)
                    {
                        logger.LogWarning(response.ToString());
                        logger.LogWarning(responseText);
                        throw;
                    }
                }
            }
        }
    }
}
