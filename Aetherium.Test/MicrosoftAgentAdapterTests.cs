using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents;
using System.Text.Json;

namespace Aetherium.Test
{
    [TestFixture]
    public class MicrosoftAgentAdapterTests
    {
        [Test]
        public async Task DecideAsync_ValidResponse_ReturnsDecision()
        {
            // Arrange
            var mockResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = @"{""action"":""move"",""args"":{""direction"":""F""}}"
                        }
                    }
                }
            };

            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
            var httpClient = new HttpClient(handler);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{""playerLocation"":{""x"":0,""y"":0,""z"":0}}", CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Action, Is.EqualTo("move"));
            Assert.That(decision.Args, Is.Not.Null);
            Assert.That(decision.Args!["direction"], Is.EqualTo("F"));
        }

        [Test]
        public async Task DecideAsync_PickupAction_ReturnsPickupDecision()
        {
            // Arrange
            var mockResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = @"{""action"":""pickup"",""args"":{""targetEntityId"":""item:123""}}"
                        }
                    }
                }
            };

            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
            var httpClient = new HttpClient(handler);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{""visibleItems"":[]}", CancellationToken.None);

            // Assert
            Assert.That(decision.Action, Is.EqualTo("pickup"));
            Assert.That(decision.Args!["targetEntityId"], Is.EqualTo("item:123"));
        }

        [Test]
        public async Task DecideAsync_InvalidJson_FallsBackToMove()
        {
            // Arrange
            var mockResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "not valid json"
                        }
                    }
                }
            };

            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
            var httpClient = new HttpClient(handler);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{}", CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Action, Is.EqualTo("move"));
            Assert.That(decision.Args!["direction"], Is.EqualTo("F"));
        }

        [Test]
        public async Task DecideAsync_HttpError_FallsBackToMove()
        {
            // Arrange
            var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Server error");
            var httpClient = new HttpClient(handler);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{}", CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Action, Is.EqualTo("move"));
        }

        [Test]
        public async Task DecideAsync_Timeout_FallsBackToMove()
        {
            // Arrange
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}", delay: TimeSpan.FromSeconds(15));
            var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{}", CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Action, Is.EqualTo("move"));
        }

        [Test]
        public async Task DecideAsync_WrappedJson_ExtractsCorrectly()
        {
            // Arrange - LLM sometimes wraps JSON in text
            var mockResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = @"Here's my decision: {""action"":""use"",""args"":{""itemEntityId"":""key:456"",""onEntityId"":""door:789""}} - that's what I'll do."
                        }
                    }
                }
            };

            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
            var httpClient = new HttpClient(handler);
            var adapter = new MicrosoftAgentAdapter(httpClient);

            // Act
            var decision = await adapter.DecideAsync(@"{}", CancellationToken.None);

            // Assert
            Assert.That(decision.Action, Is.EqualTo("use"));
            Assert.That(decision.Args!["itemEntityId"], Is.EqualTo("key:456"));
            Assert.That(decision.Args!["onEntityId"], Is.EqualTo("door:789"));
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;
            private readonly TimeSpan? _delay;

            public MockHttpMessageHandler(HttpStatusCode statusCode, string content, TimeSpan? delay = null)
            {
                _statusCode = statusCode;
                _content = content;
                _delay = delay;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_delay.HasValue)
                {
                    await Task.Delay(_delay.Value, cancellationToken);
                }

                return new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}


