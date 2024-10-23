﻿using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RiskFirst.Hateoas.Implementation;
using RiskFirst.Hateoas.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using System.Collections;
using System.Linq;
using RiskFirst.Hateoas.Models;
using Microsoft.Extensions.Primitives;

namespace RiskFirst.Hateoas.Tests
{

    [Trait("Category", "Service")]
    public class DefaultLinkServiceTests 
    {
        private LinksServiceTestCase ConfigureTestCase(Action<TestCaseBuilder> configureTest)
        {
            var builder = new TestCaseBuilder();
            configureTest?.Invoke(builder);
            return builder.BuildLinksServiceTestCase();
        }

        [AutonamedFact]
        [Trait("PolicySelection", "DefaultFallback")]
        public async Task GivenNoDefinedPolicy_FallbackToDefaultPolicy()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                      .DefaultPolicy(policy =>
                      {
                          policy.RequireSelfLink();
                      });
            });

            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("Route1", model.Links["self"].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<ILinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<SelfLinkRequirement<ILinkContainer>>()), Times.Once());
        }

        #region SelfLink Tests
        [AutonamedFact]
        [Trait("Requirement","SelfLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenNoRouteValues_AddsLinkToModel()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireSelfLink();
                     });
            });


            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("Route1", model.Links["self"].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<SelfLinkRequirement<TestLinkContainer>>()), Times.Once());
        }        
        [AutonamedFact]
        [Trait("Requirement", "SelfLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenRouteValues_AddsLinkToModel()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireSelfLink();
                     })
                     .AddRouteValues(routeData => routeData.Values.Add("Foo", "Bar"));
            });


            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("Route1?Foo=Bar", model.Links["self"].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<SelfLinkRequirement<TestLinkContainer>>()), Times.Once());
        }
        #endregion

        #region RoutedLink Tests
        [AutonamedTheory]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        public async Task GivenValidRoute_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0  };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenValidRouteWithAssertion_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id }, condition => condition.Assert(x => true));
                     });
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);


            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenValidRouteWithNegativeAssertion_DoesNotAddLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id }, condition => condition.Assert(x => false));
                     });
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Skipped(It.IsAny<RouteLinkRequirement<TestLinkContainer>>(), LinkRequirementSkipReason.Assertion, null));
        }

        [AutonamedTheory]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenValidAndInvalidRoutes_AddsLinkToModelAndSkipsInvalidLink(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id })
                                .RequireRoutedLink("badLink", "BadRoute");
                     });
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();

            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
            contextMock.Verify(x => x.Skipped(It.IsAny<RouteLinkRequirement<TestLinkContainer>>(), LinkRequirementSkipReason.Error, It.IsRegex(".*BadRoute")), Times.Once());
           

        }

        [AutonamedTheory]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenValidRouteRequireingAuth_AddsLinkToModel_WhenGranted(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id }, condition => condition.AuthorizeRoute());
                     });
            });

            testCase.AuthServiceMock.Setup(x => x.AuthorizeLink(It.IsAny<LinkAuthorizationContext<TestLinkContainer>>()))
                                    .Returns(Task.FromResult(true));

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenValidRouteRequireingAuth_DoesNotAddLinkToModel_WhenDenied(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id }, condition => condition.AuthorizeRoute());
                     });
            });

            testCase.AuthServiceMock.Setup(x => x.AuthorizeLink(It.IsAny<LinkAuthorizationContext<TestLinkContainer>>()))
                                    .Returns(Task.FromResult(false));

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Skipped(It.IsAny<RouteLinkRequirement<TestLinkContainer>>(),LinkRequirementSkipReason.Authorization,null), Times.Once());
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "MethodOverride")]
        public async Task GivenValidRouteWithRouteOverride_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          var overrideMock = new Mock<IControllerMethodInfo>();
                          var attr = new LinksAttribute() { Policy = "OverridePolicy" };
                          overrideMock.Setup(x => x.GetAttributes<LinksAttribute>()).Returns(new[] { attr });

                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, overrideMock.Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("OverrideRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id });
                     })
                     .AddPolicy<TestLinkContainer>("OverridePolicy", policy =>
                     {
                         policy.RequireRoutedLink("testLink", "OverrideRoute", x => !id.HasValue ? null : new { id = x.Id });
                     }); 
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("OverrideRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "MethodOverride")]
        public async Task GivenValidRouteWithEmptyRouteOverride_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          var overrideMock = new Mock<IControllerMethodInfo>();
                          var attr = new LinksAttribute() ;
                          overrideMock.Setup(x => x.GetAttributes<LinksAttribute>()).Returns(new[] { attr });

                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, overrideMock.Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("OverrideRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "OverrideRoute", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });

            // Act
            var model = new TestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("OverrideRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "MethodOverride")]
        public async Task GivenInvalidRouteWithRouteOverride_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          var overrideMock = new Mock<IControllerMethodInfo>();
                          var attr = new LinksAttribute() { Policy = "OverridePolicy" };
                          overrideMock.Setup(x => x.GetAttributes<LinksAttribute>()).Returns(new[] { attr });

                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, overrideMock.Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute");
                     }) ;
            });

            // Act
            var model = new TestLinkContainer();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                    testCase.UnderTest.AddLinksAsync(model));
            
            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");
            Assert.Contains("OverridePolicy", ex.Message);
        }

        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "MethodOverride")]
        public async Task GivenInvalidRouteWithEmptyRouteOverride_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          var overrideMock = new Mock<IControllerMethodInfo>();
                          var attr = new LinksAttribute();
                          overrideMock.Setup(x => x.GetAttributes<LinksAttribute>()).Returns(new[] { attr });

                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, overrideMock.Object))
                              .SetCurrentRoute("Route1");
                      });
            });

            // Act
            var model = new TestLinkContainer();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    testCase.UnderTest.AddLinksAsync(model));

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "TypeOverride")]
        public async Task GivenValidRouteWithTypeOverride_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {                         
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("OverrideRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<OverrideTestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id });
                     })
                     .AddPolicy<OverrideTestLinkContainer>("OverridePolicy", policy =>
                     {
                         policy.RequireRoutedLink("testLink", "OverrideRoute", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });

            // Act
            var model = new OverrideTestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("OverrideRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<OverrideTestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<OverrideTestLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "TypeOverride")]
        public async Task GivenValidRouteWithEmptyTypeOverride_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("OverrideRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<EmptyOverrideTestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "OverrideRoute", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });

            // Act
            var model = new EmptyOverrideTestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("OverrideRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<EmptyOverrideTestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<EmptyOverrideTestLinkContainer>>()), Times.Once());
        }

        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "TypeOverride")]
        public async Task GivenInvalidRouteWithTypeOverride_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {                          
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute");
                     });
            });

            // Act
            var model = new OverrideTestLinkContainer();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    testCase.UnderTest.AddLinksAsync(model));

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");
            Assert.Contains("OverridePolicy", ex.Message);
        }

        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "TypeOverride")]
        public async Task GivenInvalidRouteWithEmptyTypeOverride_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      });
            });

            // Act
            var model = new EmptyOverrideTestLinkContainer();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    testCase.UnderTest.AddLinksAsync(model));

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "NamedOverride")]
        public async Task GivenValidRouteWithNamedOverride_AddsLinkToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("OverrideRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<OverrideTestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute", x => !id.HasValue ? null : new { id = x.Id });
                     })
                     .AddPolicy<OverrideTestLinkContainer>("OverridePolicy", policy =>
                     {
                         policy.RequireRoutedLink("testLink", "OverrideRoute", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });

            // Act
            var model = new OverrideTestLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model,"OverridePolicy");

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("OverrideRoute" + expectation.ExpectedQueryString, model.Links[expectation.Id].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<OverrideTestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<OverrideTestLinkContainer>>()), Times.Once());
        }

        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "NamedOverride")]
        public async Task GivenInvalidRouteWithNamedOverride_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink", "TestRoute");
                     });
            });

            // Act
            var model = new OverrideTestLinkContainer();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    testCase.UnderTest.AddLinksAsync(model,"OverridePolicy"));

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");
            Assert.Contains("OverridePolicy", ex.Message);
        }

        public static IEnumerable<object[]> CreateRoutedLinkTestData()
        {
            yield return new object[] { null, new RoutedLinkExpectation() { Id = "testLink" } };
            yield return new object[] { 123, new RoutedLinkExpectation() { Id = "testLink", ExpectedQueryString="?id=123" } };
        }

        public class RoutedLinkExpectation
        {
            public string Id { get; set; }
            public string ExpectedQueryString { get; set; }
        }
        #endregion

        #region PagedLinks Tests
        [AutonamedTheory]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("Requirement", "PagedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenMultiplePages_AddsCorrectLinksToModel(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks();
                     })
                    .WithQueryParams(queryParams);
            });

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            foreach (var exp in linkExpectations)
            {
                if (exp.ShouldExist)
                {
                    Assert.True(model.Links.ContainsKey(exp.Id));
                    if (queryParams != null && queryParams.Any()) {
                        var queryParamsString = string.Join("&", queryParams.Where(x => x.Key != "pagenumber" && x.Key != "pagesize").Select(x => x.Key + "=" + x.Value));
                        Assert.Equal($"PagedRoute?{queryParamsString}&pagenumber={exp.ExpectedPageNumber}&pagesize={exp.ExpectedPageSize}", model.Links[exp.Id].Href);
                    } else {
                        Assert.Equal($"PagedRoute?pagenumber={exp.ExpectedPageNumber}&pagesize={exp.ExpectedPageSize}", model.Links[exp.Id].Href);
                    }
                }
                else
                {
                    Assert.False(model.Links.ContainsKey(exp.Id));
                }
            }

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<PagingLinksRequirement<TestPagedLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [Trait("Requirement", "PagedLink")]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenMultiplePagesWithAssertion_AddsCorrectLinksToModel(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks(condition => condition.Assert(x => true));
                     });
            });

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            foreach (var exp in linkExpectations)
            {
                if (exp.ShouldExist)
                {
                    Assert.True(model.Links.ContainsKey(exp.Id));
                    Assert.Equal($"PagedRoute?pagenumber={exp.ExpectedPageNumber}&pagesize={exp.ExpectedPageSize}", model.Links[exp.Id].Href);
                }
                else
                {
                    Assert.False(model.Links.ContainsKey(exp.Id));
                }
            }

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<PagingLinksRequirement<TestPagedLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [Trait("Requirement", "PagedLink")]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenMultiplePagesWithNegativeAssertion_DoesNotAddLinksToModel(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks(condition => condition.Assert(x => false));
                     });
            });

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Skipped(It.IsAny<PagingLinksRequirement<TestPagedLinkContainer>>(), LinkRequirementSkipReason.Assertion, null),Times.Once());
        }

        [AutonamedTheory]
        [Trait("Requirement", "PagedLink")]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenMultiplePagesRequireingAuth_AddsLinksToModel_WhenGranted(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks(condition => condition.AuthorizeRoute());
                     });
            });
            testCase.AuthServiceMock.Setup(x => x.AuthorizeLink(It.IsAny<LinkAuthorizationContext<TestPagedLinkContainer>>()))
                                   .Returns(Task.FromResult(true));

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            foreach (var exp in linkExpectations)
            {
                if (exp.ShouldExist)
                {
                    Assert.True(model.Links.ContainsKey(exp.Id));
                    Assert.Equal($"PagedRoute?pagenumber={exp.ExpectedPageNumber}&pagesize={exp.ExpectedPageSize}", model.Links[exp.Id].Href);
                }
                else
                {
                    Assert.False(model.Links.ContainsKey(exp.Id));
                }
            }

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<PagingLinksRequirement<TestPagedLinkContainer>>()), Times.Once());
        }

        [AutonamedTheory]
        [Trait("Requirement", "PagedLink")]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("PolicySelection", "DefaultType")]
        public async Task GivenMultiplePagesRequireingAuth_DoesNotAddLinksToModel_WhenDenied(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks(condition => condition.AuthorizeRoute());
                     });
            });
            testCase.AuthServiceMock.Setup(x => x.AuthorizeLink(It.IsAny<LinkAuthorizationContext<TestPagedLinkContainer>>()))
                                   .Returns(Task.FromResult(false));

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Skipped(It.IsAny<PagingLinksRequirement<TestPagedLinkContainer>>(),LinkRequirementSkipReason.Authorization,null), Times.Once());
        }

        public static IEnumerable<object[]> CreatePagedLinkTestData() {
            // int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations

            // Test case 1: first page of multiple pages
            yield return new object[] { 50, 3, 1, null,
                new[] { new PagedLinkExpectation("currentPage",true,1,50), new PagedLinkExpectation("nextPage", true, 2, 50), new PagedLinkExpectation("previousPage", false) } };
            // Test case 2: mid page of multiple pages
            yield return new object[] { 50, 3, 2, null,
                new[] { new PagedLinkExpectation("currentPage",true,2,50), new PagedLinkExpectation("nextPage", true, 3, 50), new PagedLinkExpectation("previousPage", true, 1, 50) } };
            // Test case 3: last page of multiple pages
            yield return new object[] { 50, 3, 3, null,
                new[] { new PagedLinkExpectation("currentPage",true,3,50), new PagedLinkExpectation("nextPage", false), new PagedLinkExpectation("previousPage", true, 2, 50) } };
            // Test case 4: first page of single page
            yield return new object[] { 50, 1, 1, null,
                new[] { new PagedLinkExpectation("currentPage",true,1,50), new PagedLinkExpectation("nextPage", false), new PagedLinkExpectation("previousPage", false) } };
            // Test case 5: first page of single page with extra query params
            yield return new object[] { 50, 1, 1, new Dictionary<string, StringValues>
                {
                    { "pagenumber", new StringValues("1")},
                    { "pagesize", new StringValues("50")},
                    { "firstParamKey", new StringValues("test")},
                    { "secondParmaKey", new StringValues("1,2,3")}
                },
                new[] { new PagedLinkExpectation("currentPage",true,1,50), new PagedLinkExpectation("nextPage", false), new PagedLinkExpectation("previousPage", false) } };
        }

        public class PagedLinkExpectation
        {
            public string Id { get; }
            public bool ShouldExist { get; }
            public int ExpectedPageNumber { get; set; }
            public int ExpectedPageSize { get; set; }
            public PagedLinkExpectation(string id, bool shouldExist, int expectedPageNumber = 0, int expectedPageSize = 0)
            {
                this.Id = id;
                this.ShouldExist = shouldExist;
                this.ExpectedPageNumber = expectedPageNumber;
                this.ExpectedPageSize = expectedPageSize;
            }
        }
        #endregion

        #region CustomRequirement Tests
        [AutonamedFact]
        [Trait("Requirement","Custom")]
        public async Task CustomRequirement_GivenCustomRequirementAndHandler_AddsLinkToModel()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                    .WithTestRouteMap(routes =>
                    {
                        routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                            .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                            .SetCurrentRoute("Route1");
                    })
                    .WithHandler<TestRequirementHandler<TestLinkContainer>>()
                    .AddPolicy<TestLinkContainer>(policy =>
                    {
                        policy.Requires<TestRequirement<TestLinkContainer>>();
                    });
            });

            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute", model.Links["testLink"].Href);
            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<TestRequirement<TestLinkContainer>>()), Times.Once());
        }

        [AutonamedFact]
        [Trait("Requirement", "Custom")]
        public async Task CustomRequirement_GivenCustomRequirementWithoutHandler_WritesWarning()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                    .WithTestRouteMap(routes =>
                    {
                        routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                            .SetCurrentRoute("Route1");
                    })
                    .AddPolicy<TestLinkContainer>(policy =>
                    {
                        policy.Requires<TestRequirement<TestLinkContainer>>();
                    });
            });

            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 0, "Incorrect number of links applied");

            testCase.ServiceLoggerMock.Verify(x => x.Log(LogLevel.Warning, 0, It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()));
        }

        [AutonamedFact]
        [Trait("Requirement", "Custom")]
        public async Task CustomRequirement_GivenCustomRequirementAndHandlerWhichThrowsException_AddsOtherLinkAndWritesWarning()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                    .WithTestRouteMap(routes =>
                    {
                        routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                            .AddRoute(new RouteInfo("TestRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                            .SetCurrentRoute("Route1");
                    })
                    .WithHandler<ExceptionRequirementHandler<TestLinkContainer>>()
                    .AddPolicy<TestLinkContainer>(policy =>
                    {
                        policy.Requires<TestRequirement<TestLinkContainer>>()
                                .RequireRoutedLink("testLink","TestRoute");
                    });
            });

            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 1, "Incorrect number of links applied");
            Assert.Equal("TestRoute", model.Links["testLink"].Href);
            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<TestRequirement<TestLinkContainer>>()), Times.Never());
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once());
            testCase.ServiceLoggerMock.Verify(x => x.Log(LogLevel.Warning, 0, It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()));
        }
        #endregion

        #region Combining Policies Tests
        [AutonamedFact]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "NamedOverride")]
        public async Task WhenCombiningSameType_AddsCorrectLinksToModel()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute2", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink1", "TestRoute1");
                     })
                     .AddPolicy<TestLinkContainer>("MoreInfo", policy =>
                     {
                         policy.Combine(builder.GetPolicy<TestLinkContainer>())
                                .RequireRoutedLink("testLink2", "TestRoute2");
                     });
            });


            // Act
            var model = new TestLinkContainer();
            await testCase.UnderTest.AddLinksAsync(model,"MoreInfo");

            // Assert
            Assert.True(model.Links.Count == 2, "Incorrect number of links applied");
            Assert.Equal("TestRoute1", model.Links["testLink1"].Href);
            Assert.Equal("TestRoute2", model.Links["testLink2"].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Exactly(2));
        }

        [AutonamedTheory()]
        [MemberData(nameof(CreateRoutedLinkTestData))]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task WhenCombiningWithBaseType_AddsCorrectLinksToModel(int? id, RoutedLinkExpectation expectation)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute2", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink1", "TestRoute1", x => !id.HasValue ? null : new { id = x.Id });
                     })
                     .AddPolicy<DerivedLinkContainer>(policy =>
                     {
                         policy.Combine(builder.GetPolicy<TestLinkContainer>())
                                .RequireRoutedLink("testLink2", "TestRoute2", x => !id.HasValue ? null : new { id = x.Id });
                     });
            });


            // Act
            var model = new DerivedLinkContainer() { Id = id ?? 0 };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            Assert.True(model.Links.Count == 2, "Incorrect number of links applied");
            Assert.Equal("TestRoute1" + expectation.ExpectedQueryString, model.Links["testLink1"].Href);
            Assert.Equal("TestRoute2" + expectation.ExpectedQueryString, model.Links["testLink2"].Href);

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<TestLinkContainer>>()), Times.Once);
            contextMock.Verify(x => x.Handled(It.IsAny<RouteLinkRequirement<DerivedLinkContainer>>()), Times.Once);
        }

        [AutonamedTheory]
        [MemberData(nameof(CreatePagedLinkTestData))]
        [Trait("Requirement", "PagedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task WhenCombiningWithInterfaceType_AddsCorrectLinksToModel(int pageSize, int pageCount, int pageNumber, Dictionary<string, StringValues> queryParams, IEnumerable<PagedLinkExpectation> linkExpectations)
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("PagedRoute", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("PagedRoute");
                      })
                     .AddPolicy<IPagedLinkContainer>(policy =>
                     {
                         policy.RequiresPagingLinks();
                     })
                     .AddPolicy<TestPagedLinkContainer>(policy =>
                     {
                         policy.Combine(builder.GetPolicy<IPagedLinkContainer>())
                                .RequireSelfLink();
                     });
            });

            // Act
            var model = new TestPagedLinkContainer()
            {
                PageSize = pageSize,
                PageCount = pageCount,
                PageNumber = pageNumber
            };
            await testCase.UnderTest.AddLinksAsync(model);

            // Assert
            foreach (var exp in linkExpectations)
            {
                if (exp.ShouldExist)
                {
                    Assert.True(model.Links.ContainsKey(exp.Id));
                    Assert.Equal($"PagedRoute?pagenumber={exp.ExpectedPageNumber}&pagesize={exp.ExpectedPageSize}", model.Links[exp.Id].Href);
                }
                else
                {
                    Assert.False(model.Links.ContainsKey(exp.Id));
                }
            }
            Assert.True(model.Links.ContainsKey("self"), "Missing self link");

            var contextMock = testCase.LinksHandlerContextFactory.GetLinksHandlerContextMock<TestPagedLinkContainer>();
            contextMock.Verify(x => x.Handled(It.IsAny<PagingLinksRequirement<IPagedLinkContainer>>()), Times.Once());
            contextMock.Verify(x => x.Handled(It.IsAny<SelfLinkRequirement<TestPagedLinkContainer>>()), Times.Once());
        }

        [AutonamedFact()]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task WhenCombiningWithDerivedType_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute2", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<DerivedLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink1", "TestRoute1");
                     })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.Combine(builder.GetPolicy<DerivedLinkContainer>())
                                .RequireRoutedLink("testLink2", "TestRoute2");
                     });
            });


            // Act
            var model = new TestLinkContainer();


            // Assert
            await Assert.ThrowsAsync<InvalidCastException>(() => testCase.UnderTest.AddLinksAsync(model));
        }

        [AutonamedFact()]
        [Trait("Requirement", "RoutedLink")]
        [Trait("PolicySelection", "DefaultType")]
        public async Task WhenCombiningWithUnrelatedType_ThrowsException()
        {
            // Arrange
            var testCase = ConfigureTestCase(builder =>
            {
                builder.UseBasicTransformations()
                      .WithTestRouteMap(routes =>
                      {
                          routes.AddRoute(new RouteInfo("Route1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute1", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .AddRoute(new RouteInfo("TestRoute2", HttpMethod.Get, new Mock<IControllerMethodInfo>().Object))
                              .SetCurrentRoute("Route1");
                      })
                     .AddPolicy<UnrelatedLinkContainer>(policy =>
                     {
                         policy.RequireRoutedLink("testLink1", "TestRoute1");
                     })
                     .AddPolicy<TestLinkContainer>(policy =>
                     {
                         policy.Combine(builder.GetPolicy<UnrelatedLinkContainer>())
                                .RequireRoutedLink("testLink2", "TestRoute2");
                     });
            });


            // Act
            var model = new TestLinkContainer();


            // Assert
            await Assert.ThrowsAsync<InvalidCastException>(() => testCase.UnderTest.AddLinksAsync(model));
        }
        #endregion
    }

   
}