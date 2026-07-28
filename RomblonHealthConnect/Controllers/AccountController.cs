using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Identity;
using RomblonHealthConnect.Services;
using RomblonHealthConnect.ViewModels.Account;

namespace RomblonHealthConnect.Controllers;

/// <summary>
/// Sign-in, sign-out, and password management.
///
/// Every failed path returns the same generic message so the response cannot be
/// used to discover whether an account exists.
/// </summary>
[Authorize]
public class AccountController : Controller
{
    /// <summary>Shown for every failed sign-in, whatever the underlying cause.</summary>
    private const string GenericLoginFailure = "Invalid sign-in attempt. Check your details and try again.";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
        _logger = logger;
    }

    /* ------------------------------------------------------------------ */
    /* Sign in                                                             */
    /* ------------------------------------------------------------------ */

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["Title"] = "Sign in";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sign in";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Accept either identifier without revealing which one matched.
        var user = await _userManager.FindByNameAsync(model.UserNameOrEmail)
                   ?? await _userManager.FindByEmailAsync(model.UserNameOrEmail);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, GenericLoginFailure);
            await _audit.LogAsync(AuditActions.LoginFailed, nameof(ApplicationUser), null,
                "Sign-in attempted with an unrecognised identifier.", cancellationToken: cancellationToken);
            return View(model);
        }

        // Deactivated accounts are refused before the password is checked.
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, GenericLoginFailure);
            await _audit.LogAsync(AuditActions.LoginBlockedInactive, nameof(ApplicationUser), user.Id,
                $"Sign-in refused for deactivated account '{user.UserName}'.",
                cancellationToken: cancellationToken);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "This account is temporarily locked after repeated failed attempts. Try again later.");
            await _audit.LogAsync(AuditActions.LoginLockedOut, nameof(ApplicationUser), user.Id,
                $"Sign-in blocked by lockout for '{user.UserName}'.", cancellationToken: cancellationToken);
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, GenericLoginFailure);
            await _audit.LogAsync(AuditActions.LoginFailed, nameof(ApplicationUser), user.Id,
                $"Incorrect password for '{user.UserName}'.", cancellationToken: cancellationToken);
            return View(model);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync(AuditActions.LoginSucceeded, nameof(ApplicationUser), user.Id,
            $"'{user.UserName}' signed in.", cancellationToken: cancellationToken);

        _logger.LogInformation("User {UserName} signed in.", user.UserName);

        return RedirectToLocal(model.ReturnUrl);
    }

    /* ------------------------------------------------------------------ */
    /* Sign out                                                            */
    /* ------------------------------------------------------------------ */

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        var userName = User.Identity?.Name;

        await _signInManager.SignOutAsync();

        await _audit.LogAsync(AuditActions.Logout, nameof(ApplicationUser), userId,
            $"'{userName}' signed out.", cancellationToken: cancellationToken);

        return RedirectToAction(nameof(Login));
    }

    /* ------------------------------------------------------------------ */
    /* Access denied                                                       */
    /* ------------------------------------------------------------------ */

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> AccessDenied(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Access denied";
        ViewData["ReturnUrl"] = returnUrl;

        if (User.Identity?.IsAuthenticated == true)
        {
            await _audit.LogAsync(AuditActions.AccessDenied, "Authorization", null,
                $"Access denied for '{User.Identity.Name}' at '{returnUrl}'.",
                cancellationToken: cancellationToken);
        }

        return View();
    }

    /* ------------------------------------------------------------------ */
    /* Change password                                                     */
    /* ------------------------------------------------------------------ */

    [HttpGet]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change password";
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Change password";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // Re-issue the cookie so the new stamp takes effect immediately.
        await _signInManager.RefreshSignInAsync(user);

        await _audit.LogAsync(AuditActions.PasswordChanged, nameof(ApplicationUser), user.Id,
            $"'{user.UserName}' changed their password.", cancellationToken: cancellationToken);

        TempData["StatusMessage"] = "Your password has been changed.";
        return RedirectToAction(nameof(ChangePassword));
    }

    /* ------------------------------------------------------------------ */
    /* Forgot / reset password                                             */
    /* ------------------------------------------------------------------ */

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot password";
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["Title"] = "Forgot password";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // The same confirmation is shown whether or not the address exists, so
        // the page cannot be used to enumerate accounts.
        if (user is not null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // No mail transport is configured yet. The token is written to the
            // server log for development only and never shown to the browser.
            _logger.LogInformation(
                "Password reset requested for {Email}. Reset token generated (development only).",
                model.Email);
            _logger.LogDebug("Reset token for {Email}: {Token}", model.Email, token);
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        ViewData["Title"] = "Check your email";
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email = null, string? token = null)
    {
        ViewData["Title"] = "Reset password";

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("A password reset token is required.");
        }

        return View(new ResetPasswordViewModel { Email = email ?? string.Empty, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["Title"] = "Reset password";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Same redirect either way, again to avoid enumeration.
        if (user is null)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        ViewData["Title"] = "Password reset";
        return View();
    }

    /* ------------------------------------------------------------------ */

    /// <summary>Only ever redirects within the application.</summary>
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
