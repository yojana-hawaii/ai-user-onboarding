using System.ComponentModel.DataAnnotations;
using DaHub.Core.Entities;

namespace DaHub.Core.Employees;

public static class EmployeeProfile
{
    public static void ValidateVerified(Employee employee, IReadOnlyCollection<bool> departments,
        IReadOnlyCollection<bool> locations, IReadOnlyCollection<bool> jobTitles, IReadOnlyCollection<bool> specialties)
    {
        if (employee.VerificationStatus != "Verified" ||
            string.IsNullOrWhiteSpace(employee.LegalFirstName) ||
            string.IsNullOrWhiteSpace(employee.LegalLastName) ||
            string.IsNullOrWhiteSpace(employee.Username) || string.IsNullOrWhiteSpace(employee.Email) ||
            employee.EmploymentType is not ("FT" or "PT" or "Contractor" or "Volunteer" or "Student" or "Temporary") ||
            employee.StartDate is null || employee.EndDate < employee.StartDate ||
            employee.ManagerId is <= 0 || (employee.ManagerId is not null && employee.ManagerId == employee.Id) ||
            string.IsNullOrWhiteSpace(employee.VerifiedByUPN) || employee.VerifiedAt is null ||
            employee.VerifiedAt.Value.Kind == DateTimeKind.Local)
            throw new ValidationException("Verified workers require complete identity, employment dates and HR verification evidence.");

        RequirePrimary(departments, "department");
        RequirePrimary(locations, "location");
        RequirePrimary(jobTitles, "job title");
        if (specialties.Count > 0) RequirePrimary(specialties, "specialty");
    }

    private static void RequirePrimary(IReadOnlyCollection<bool> assignments, string field)
    {
        if (assignments.Count(x => x) != 1)
            throw new ValidationException($"Verified workers require exactly one primary {field}.");
    }
}
