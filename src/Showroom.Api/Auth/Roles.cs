using Microsoft.AspNetCore.Authorization;

namespace Showroom.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string SalesRep = "SalesRep";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, SalesRep, Customer];
}

public static class Policies
{
    /// <summary>Add, update, change status: Admin or Sales Representative.</summary>
    public const string ManageVehicles = "ManageVehicles";

    /// <summary>Delete: Admin only.</summary>
    public const string DeleteVehicles = "DeleteVehicles";

    /// <summary>
    /// Registers the access rules. Used by Program.cs AND by the unit tests,
    /// so the tests check the real policies, not a copy.
    /// </summary>
    public static AuthorizationOptions AddShowroomPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ManageVehicles, p => p.RequireRole(Roles.Admin, Roles.SalesRep));
        options.AddPolicy(DeleteVehicles, p => p.RequireRole(Roles.Admin));

        // Secure by default: every endpoint without [AllowAnonymous] needs a valid token
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        return options;
    }
}
