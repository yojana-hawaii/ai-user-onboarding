using System.ComponentModel.DataAnnotations;
using DaHub.Core.Entities;
using DaHub.Core.Employees;
using Xunit;

namespace DaHub.Tests.Unit;

public sealed class EmployeeProfileTests
{
    public static Employee Complete() => new()
    {
        Id = 42,
        LegalFirstName = "Synthetic",
        LegalLastName = "Worker",
        Username = "synthetic.worker",
        Email = "worker@example.test",
        EmploymentType = "FT",
        StartDate = new DateOnly(2026, 11, 1),
        VerificationStatus = "Verified",
        VerifiedByUPN = "hr@example.test",
        VerifiedAt = DateTime.UtcNow
    };

    [Theory]
    [InlineData("FT")]
    [InlineData("PT")]
    [InlineData("Contractor")]
    [InlineData("Volunteer")]
    [InlineData("Student")]
    [InlineData("Temporary")]
    public void SupportsEveryWorkerTypeAndConfirmedNoManager(string workerType)
    {
        var employee = Complete();
        employee.EmploymentType = workerType;
        EmployeeProfile.ValidateVerified(employee, [true, false], [true], [true], []);
        employee.ManagerId = 3;
        employee.EndDate = employee.StartDate;
        EmployeeProfile.ValidateVerified(employee, [true], [true], [true], [false, true]);
    }

    [Theory]
    [InlineData("LegalFirstName")]
    [InlineData("LegalLastName")]
    [InlineData("Username")]
    [InlineData("Email")]
    [InlineData("EmploymentType")]
    [InlineData("StartDate")]
    [InlineData("VerificationStatus")]
    [InlineData("VerifiedByUPN")]
    [InlineData("VerifiedAt")]
    public void RejectsMissingVerificationDetails(string field)
    {
        var employee = Complete();
        var property = typeof(Employee).GetProperty(field)!;
        property.SetValue(employee, property.PropertyType == typeof(string) ? " " : null);
        Assert.Throws<ValidationException>(() => EmployeeProfile.ValidateVerified(employee, [true], [true], [true], []));
    }

    [Fact]
    public void RejectsInvalidDatesWorkerTypeAndSelfManager()
    {
        var employee = Complete();
        employee.EmploymentType = "Other";
        Invalid(employee);
        employee = Complete();
        employee.ManagerId = employee.Id;
        Invalid(employee);
        employee = Complete();
        employee.ManagerId = 0;
        Invalid(employee);
        employee = Complete();
        employee.EndDate = employee.StartDate!.Value.AddDays(-1);
        Invalid(employee);
        employee = Complete();
        employee.VerifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);
        Invalid(employee);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RequiresOnePrimaryPerSelectedAssignment(int dimension)
    {
        foreach (var invalid in new bool[][] { [], [false], [true, true] })
        {
            if (dimension == 3 && invalid.Length == 0) continue;
            var assignments = new bool[][] { [true], [true], [true], [] };
            assignments[dimension] = invalid;
            Assert.Throws<ValidationException>(() => EmployeeProfile.ValidateVerified(Complete(),
                assignments[0], assignments[1], assignments[2], assignments[3]));
        }
    }

    private static void Invalid(Employee employee) => Assert.Throws<ValidationException>(() =>
        EmployeeProfile.ValidateVerified(employee, [true], [true], [true], []));
}
