using FluentAssertions;
using HireLens.Infrastructure.Btp;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class HanaConnectionTests
{
    [Fact]
    public void FromVcap_builds_ado_string_from_hana_binding()
    {
        const string vcap = """
            {
              "hana": [{
                "name": "hana_dev",
                "credentials": {
                  "host": "example.hanacloud.ondemand.com",
                  "port": "443",
                  "user": "DBADMIN",
                  "password": "secret",
                  "schema": "HIRELENS",
                  "url": "jdbc:sap://example.hanacloud.ondemand.com:443"
                }
              }]
            }
            """;

        var connection = HanaConnection.FromVcap(vcap);

        connection.Should().Contain("ServerNode=example.hanacloud.ondemand.com:443");
        connection.Should().Contain("UID=DBADMIN");
        connection.Should().Contain("PWD=secret");
        connection.Should().Contain("CurrentSchema=HIRELENS");
        connection.Should().NotContain("jdbc:");
    }

    [Fact]
    public void FromVcap_uses_explicit_ado_connection_on_user_provided_service()
    {
        const string vcap = """
            {
              "user-provided": [{
                "name": "hana_dev",
                "credentials": {
                  "HANA_CONNECTION": "ServerNode=local:39013;UID=user;PWD=pass;"
                }
              }]
            }
            """;

        HanaConnection.FromVcap(vcap).Should().Be("ServerNode=local:39013;UID=user;PWD=pass;");
    }
}
