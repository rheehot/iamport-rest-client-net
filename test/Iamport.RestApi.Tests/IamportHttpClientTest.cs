﻿using Iamport.RestApi.Models;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.OptionsModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Iamport.RestApi.Tests
{
    public class IamportHttpClientTest
    {
        [Fact]
        public void GuardClause()
        {
            Assert.Throws<ArgumentNullException>(
                () => new IamportHttpClient(null));
        }

        [Fact]
        public void Creates_a_new_instance()
        {
            // arrange/act
            var sut = GetDefaultSut();

            // assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Creates_a_new_instance_via_config_json_file()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("AppSettings:Iamport:IamportHttpClientOptions");
            var accessor = GetAccessor(configuration);

            // act
            var sut = new IamportHttpClient(accessor);

            // assert
            Assert.NotNull(sut);
        }

        [Theory]
        [InlineData("ImportId", null)]
        [InlineData("ApiKey", null)]
        [InlineData("ApiSecret", null)]
        [InlineData("AuthorizationHeaderName", "")]
        [InlineData("BaseUrl", "")]
        public void GuardClause_for_options(string fieldName, string value)
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ImportId"] = "abcd",
                    ["ApiKey"] = "1234",
                    ["ApiSecret"] = "5678",
                    ["AuthorizationHeaderName"] = "xxxx",
                    ["BaseUrl"] = "uuuu",
                })
                .Build();
            configuration[fieldName] = value;
            var accessor = GetAccessor(configuration);

            // act/assert
            Assert.Throws<ArgumentNullException>(
                () => new IamportHttpClient(accessor));
        }

        [Theory]
        [InlineData("uuu")]
        [InlineData("a/b/c/d")]
        public void Throws_BaseUrl_is_not_Uri(string value)
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ImportId"] = "abcd",
                    ["ApiKey"] = "1234",
                    ["ApiSecret"] = "5678",
                    ["BaseUrl"] = value,
                })
                .Build();
            var accessor = GetAccessor(configuration);

            // act/assert
            Assert.Throws<UriFormatException>(
                () => new IamportHttpClient(accessor));
        }

        [Theory]
        [InlineData("files://test.txt")]
        [InlineData("app://test.txt")]
        public void Throws_BaseUrl_is_not_http_nor_https_scheme(string value)
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ImportId"] = "abcd",
                    ["ApiKey"] = "1234",
                    ["ApiSecret"] = "5678",
                    ["BaseUrl"] = value,
                })
                .Build();
            var accessor = GetAccessor(configuration);

            // act/assert
            Assert.Throws<ArgumentException>(
                () => new IamportHttpClient(accessor));
        }

        [Fact]
        public void Disposes()
        {
            // arrange
            var sut = GetDefaultSut();
            // act
            sut.Dispose();
            // assert
            Assert.True(sut.IsDisposed);
        }

        [Fact]
        public async Task GuardClause_RequestIamportRequest()
        {
            // arrange
            var sut = GetDefaultSut();
            // act/assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.RequestAsync<object, object>(null));
        }

        [Fact]
        public async Task GuardClause_RequestHttpRequest()
        {
            // arrange
            var sut = GetDefaultSut();
            // act/assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.RequestAsync<object>(null));
        }

        [Fact]
        public async Task RequestIamportRequest_throws_if_disposed()
        {
            // arrange
            var sut = GetDefaultSut();
            sut.Dispose();
            // act/assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.RequestAsync<object, object>(new IamportRequest<object>()));
        }

        [Fact]
        public async Task RequestHttpRequest_throws_if_disposed()
        {
            // arrange
            var sut = GetDefaultSut();
            sut.Dispose();
            // act/assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.RequestAsync<object>(new HttpRequestMessage()));
        }

        [Fact]
        public async Task Authorize_throws_if_disposed()
        {
            // arrange
            var sut = GetDefaultSut();
            sut.Dispose();
            // act/assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.AuthorizeAsync());
        }

        [Fact]
        public async Task Authorize_calls_RequestHttpRequest()
        {
            // arrange
            var sut = GetMockSut();
            var defaultOptions = new IamportHttpClientOptions();
            var expectedUrl = ApiPathUtility.Build(defaultOptions.BaseUrl, "/users/getToken");
            // act
            await sut.AuthorizeAsync();
            // assert
            Assert.Equal(expectedUrl, sut.Messages.Single().RequestUri.ToString());
        }

        [Fact]
        public async Task RequestIamportRequest_calls_self_url()
        {
            // arrange
            var sut = GetMockSut();
            var defaultOptions = new IamportHttpClientOptions();
            var request = new IamportRequest<object>
            {
                ApiPathAndQueryString = "/self",
                RequireAuthorization = false
            };
            var expectedUrl = ApiPathUtility.Build(defaultOptions.BaseUrl, "/self");
            // act
            var result = await sut.RequestAsync<object, object>(request);
            // assert
            Assert.NotNull(result);
            Assert.Equal(expectedUrl, sut.Messages.Single().RequestUri.ToString());
        }

        [Fact]
        public async Task RequestIamportRequest_calls_also_Authorize_and_self()
        {
            // arrange
            var sut = GetMockSut();
            var defaultOptions = new IamportHttpClientOptions();
            var request = new IamportRequest<object>
            {
                ApiPathAndQueryString = "/self",
                RequireAuthorization = true
            };
            var expectedAuthUrl = ApiPathUtility.Build(defaultOptions.BaseUrl, "/users/getToken");
            var expectedUrl = ApiPathUtility.Build(defaultOptions.BaseUrl, "/self");
            // act
            var result = await sut.RequestAsync<object, object>(request);
            // assert
            Assert.NotNull(result);
            Assert.Equal(expectedAuthUrl, sut.Messages.First().RequestUri.ToString());
            Assert.Equal(expectedUrl, sut.Messages.Last().RequestUri.ToString());
        }

        private IamportHttpClient GetDefaultSut()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ImportId"] = "abcd",
                    ["ApiKey"] = "1234",
                    ["ApiSecret"] = "5678",
                })
                .Build();
            var accessor = GetAccessor(configuration);
            return new IamportHttpClient(accessor);
        }
        private MockClient GetMockSut()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ImportId"] = "abcd",
                    ["ApiKey"] = "1234",
                    ["ApiSecret"] = "5678",
                })
                .Build();
            var accessor = GetAccessor(configuration);
            return new MockClient(accessor);
        }
        private IOptions<IamportHttpClientOptions> GetAccessor(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions();
            services.Configure<IamportHttpClientOptions>(configuration);
            var provider = services.BuildServiceProvider();
            return provider.GetService<IOptions<IamportHttpClientOptions>>();
        }

        private class MockClient : IamportHttpClient
        {
            public IList<HttpRequestMessage> Messages { get; set; } = new List<HttpRequestMessage>();

            public MockClient(IOptions<IamportHttpClientOptions> optionsAccessor) : base(optionsAccessor)
            {
            }

            public async override Task<IamportResponse<TResult>> RequestAsync<TResult>(HttpRequestMessage request)
            {
                Messages.Add(request);
                return await Task.FromResult(new IamportResponse<TResult>());
            }

            public override async Task AuthorizeAsync()
            {
                // ignore authorize for test.
                try
                {
                    await base.AuthorizeAsync();
                }
                catch
                {
                }
                await Task.FromResult(0);
            }
        }
    }
}