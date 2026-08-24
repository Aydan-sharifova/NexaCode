using System;
namespace Coding.Enums
{
    public enum ProjectRole
    {
        Owner = 0,
        Admin = 1,
        // Developer intentionally retains the former Member numeric value so
        // existing project memberships are upgraded without privilege drift.
        Developer = 2,
        Maintainer = 3,
        Viewer = 4
    }
}
