using System.Net;

namespace Coding.Infrastructure.Authentication;

internal static class AccountEmailTemplates
{
    public static string Verification(string link) => Render(
        "Confirm your email",
        "Confirm this email address to finish creating your account. You cannot enter the workspace until verification is complete.",
        link);

    public static string PasswordReset(string link) => Render(
        "Reset password",
        "Use this secure link to choose a new password. If you did not request this, ignore the message.",
        link);

    public static string ProjectInvitation(
        string inviterName,
        string projectName,
        string role,
        DateTime expiresAt,
        string link) => Render(
        "Join project",
        $"{inviterName} invited you to {projectName} as {role}. This invitation expires {expiresAt:u}.",
        link);

    private static string Render(string heading, string description, string link) => $"""
        <!doctype html><html><body style="margin:0;background:#f4f6fb;font-family:Arial,sans-serif;color:#152039">
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:32px 16px">
        <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="max-width:100%;background:white;border:1px solid #e2e6ef;border-radius:14px">
        <tr><td style="padding:32px"><div style="font-size:20px;font-weight:800;color:#6256e8">Coding</div>
        <h1 style="font-size:24px;margin:28px 0 12px">{WebUtility.HtmlEncode(heading)}</h1><p style="color:#667085;line-height:1.6">{WebUtility.HtmlEncode(description)}</p>
        <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;margin-top:16px;padding:13px 20px;border-radius:8px;background:#6256e8;color:white;text-decoration:none;font-weight:700">{WebUtility.HtmlEncode(heading)}</a>
        <p style="margin-top:28px;color:#667085;font-size:13px">This link expires in one hour. If you did not create this account, you can safely ignore this email.</p>
        <p style="margin-top:16px;color:#98a2b3;font-size:12px;word-break:break-all">If the button does not work, open: {WebUtility.HtmlEncode(link)}</p></td></tr></table>
        </td></tr></table></body></html>
        """;
}
