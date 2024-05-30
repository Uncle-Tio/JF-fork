using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoMoq;
using Jellyfin.Api.Auth.FirstTimeSetupPolicy;
using Jellyfin.Api.Constants;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Auth.FirstTimeSetupPolicy
{
    public class FirstTimeSetupHandlerTests
    {
        private readonly Mock<IConfigurationManager> _configurationManagerMock;
        private readonly List<IAuthorizationRequirement> _requirements;
        private readonly FirstTimeSetupHandler _firstTimeSetupHandler;
        private readonly Mock<IUserManager> _userManagerMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessor;

        public FirstTimeSetupHandlerTests()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            _configurationManagerMock = fixture.Freeze<Mock<IConfigurationManager>>();
            _requirements = new List<IAuthorizationRequirement> { new FirstTimeSetupRequirement() };
            _userManagerMock = fixture.Freeze<Mock<IUserManager>>();
            _httpContextAccessor = fixture.Freeze<Mock<IHttpContextAccessor>>();

            _firstTimeSetupHandler = fixture.Create<FirstTimeSetupHandler>();
        }

        [Theory]
        [InlineData(UserRoles.Administrator)]
        [InlineData(UserRoles.Guest)]
        [InlineData(UserRoles.User)]
        public async Task ShouldSucceedIfStartupWizardIncomplete(string userRole)
        {
            TestHelpers.SetupConfigurationManager(_configurationManagerMock, false);
            var claims = TestHelpers.SetupUser(
                _userManagerMock,
                _httpContextAccessor,
                userRole);

            var context = new AuthorizationHandlerContext(_requirements, claims, null);

            await _firstTimeSetupHandler.HandleAsync(context);
            Assert.True(context.HasSucceeded);
        }

        [Theory]
        [InlineData(UserRoles.Administrator, false)]
        [InlineData(UserRoles.Guest, true)]
        [InlineData(UserRoles.User, true)]
        public async Task ShouldRequireAdministratorIfStartupWizardComplete(string userRole, bool shouldFail)
        {
            TestHelpers.SetupConfigurationManager(_configurationManagerMock, true);
            var claims = TestHelpers.SetupUser(
                _userManagerMock,
                _httpContextAccessor,
                userRole);

            var context = new AuthorizationHandlerContext(_requirements, claims, null);

            await _firstTimeSetupHandler.HandleAsync(context);
            Assert.Equal(shouldFail, context.HasFailed);
        }

        [Theory]
        [InlineData(UserRoles.Administrator)]
        [InlineData(UserRoles.Guest)]
        [InlineData(UserRoles.User)]
        public async Task ShouldDeferIfNotRequiresAdmin(string userRole)
        {
            TestHelpers.SetupConfigurationManager(_configurationManagerMock, true);
            var claims = TestHelpers.SetupUser(
                _userManagerMock,
                _httpContextAccessor,
                userRole);

            var context = new AuthorizationHandlerContext(
                new List<IAuthorizationRequirement> { new FirstTimeSetupRequirement(false, false) },
                claims,
                null);

            await _firstTimeSetupHandler.HandleAsync(context);
            Assert.False(context.HasSucceeded);
            Assert.False(context.HasFailed);
        }

        [Fact]
        public async Task ShouldAllowAdminApiKeyIfStartupWizardComplete()
        {
            TestHelpers.SetupConfigurationManager(_configurationManagerMock, true);
            var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim(InternalClaimTypes.IsApiKey, bool.TrueString)]));
            var context = new AuthorizationHandlerContext(_requirements, claims, null);

            await _firstTimeSetupHandler.HandleAsync(context);
            Assert.True(context.HasSucceeded);
        }
    }
}
