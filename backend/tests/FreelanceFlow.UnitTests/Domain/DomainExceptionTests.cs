using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.UnitTests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_sets_Code_and_Message_from_code_only()
    {
        var ex = new DomainException("project.not_found");

        ex.Code.Should().Be("project.not_found");
        ex.Message.Should().Be("project.not_found");
        ex.Detail.Should().BeNull();
    }

    [Fact]
    public void Constructor_sets_Detail_as_Message_when_provided()
    {
        var ex = new DomainException("project.not_found", "Project 123 does not exist");

        ex.Code.Should().Be("project.not_found");
        ex.Detail.Should().Be("Project 123 does not exist");
        ex.Message.Should().Be("Project 123 does not exist");
    }

    [Fact]
    public void Is_an_Exception()
    {
        var ex = new DomainException("any.code");
        ex.Should().BeAssignableTo<Exception>();
    }
}
