﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestToolkit.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RestToolkit.Base
{
    [Authorize]
    [Route("api/[controller]/[action]")]
    public abstract class ToolkitAccountController<TIAdditionalUserInfo, TUser, TAdditionalUserInfoViewModel> : Controller
        where TUser : ToolkitUser, TIAdditionalUserInfo, new()
        where TAdditionalUserInfoViewModel : TIAdditionalUserInfo
    {
        private readonly IConfiguration _config;
        private readonly UserManager<TUser> _userManager;
        private readonly SignInManager<TUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ToolkitAccountController<TIAdditionalUserInfo, TUser, TAdditionalUserInfoViewModel>> _logger;

        public ToolkitAccountController(IConfiguration config,
            ILogger<ToolkitAccountController<TIAdditionalUserInfo, TUser, TAdditionalUserInfoViewModel>> logger,
            UserManager<TUser> userManager,
            SignInManager<TUser> signInManager,
            IEmailSender emailSender)
        {
            _config = config;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        public class SendTokenModel
        {
            [EmailAddress, Required]
            public string Email { get; set; }
        }

        [HttpPost]
        [AllowAnonymous]
        [ServiceFilter(typeof(ValidateRecaptchaAttribute))]
        // This accespts form data (as opposed to json body) 
        // to make ValidateRecaptchaAttribute easier to work with
        public async Task<IActionResult> SendToken([FromForm]SendTokenModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var token = await _userManager.GenerateUserTokenAsync(new TUser
            {
                Id = 0,
                Email = model.Email,
                SecurityStamp = _config["AuthTokenSecret"]
            }, "Email", "Choice");

            // Will not wait for email to be sent
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            SendTokenAsync(model.Email, token);
#pragma warning restore CS4014

            _logger.LogInformation($"Token sent to '{model.Email}'.");

            return Ok(new
            {
                model.Email
            });
        }

        public class LoginModel
        {
            [MinLength(6)]
            public string Token { get; set; }
            [EmailAddress]
            public string Email { get; set; }
            public bool IsRegistering { get; set; } = false;
            public bool RememberMe { get; set; } = true;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody]LoginModel model, [FromBody]TAdditionalUserInfoViewModel additionalUserInfo)
        {
            if (User.Identity.IsAuthenticated)
                return BadRequest("Already authenticated.");

            model.Token = model.Token.Trim().Replace(" ", "");

            var isTokenValid = await _userManager.VerifyUserTokenAsync(new TUser
            {
                Id = 0,
                Email = model.Email,
                SecurityStamp = _config["AuthTokenSecret"]
            }, "Email", "Choice", model.Token);

            if (!isTokenValid)
                return Unauthorized();

            _logger.LogInformation($"Token for '{model.Email}' verified.");

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                if (!model.IsRegistering)
                    return Accepted();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                user = new TUser();
                Map(additionalUserInfo, ref user);

                user.Email = model.Email;
                user.EmailConfirmed = true;

                var userCreateResult = await _userManager.CreateAsync(user);

                if (!userCreateResult.Succeeded)
                    return BadRequest();

                _logger.LogInformation($"User with ID '{user.Id}' created an account.");
            }

            await _signInManager.SignInAsync(user, model.RememberMe);

            _logger.LogInformation($"User with ID '{user.Id}' logged in.");

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email
            });
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            _logger.LogInformation($"User with ID '{User.GetUserId()}' logged out.");

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody]TAdditionalUserInfoViewModel additionalUserInfo)
        {
            //var user = additionalUserInfo.ToApplicationUser();
            var user = await _userManager.GetUserAsync(User);

            Map(additionalUserInfo, ref user);

            var update = await _userManager.UpdateAsync(user);

            _logger.LogInformation($"User with ID '{user.Id}' attempted an update.");

            if (update.Succeeded)
                return Ok();
            else
                throw new InvalidOperationException($"Unexpected error updating user with ID '{user.Id}'.");

        }


        // Code from razor identity
        [HttpGet]
        public async Task<IActionResult> DownloadPersonalInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation($"User with ID '{_userManager.GetUserId(User)}' asked for their personal data.");

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(TUser).GetProperties().Where(
                            prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            Response.Headers.Add("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(personalData)), "text/json");
        }

        // Code from razor identity
        [HttpPost]
        public async Task<IActionResult> Delete()
        {
            var user = await _userManager.GetUserAsync(User);
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error deleteing user with ID '{user.Id}'.");
            }

            await _signInManager.SignOutAsync();

            _logger.LogInformation($"User with ID '{user.Id}' deleted themselves.");

            return Ok();
        }
        
        private async Task SendTokenAsync(string email, string token)
        {
            token = String.Concat(token.SelectMany((c, i) => (i + 1) % 3 == 0 ? $"{c} " : $"{c}")).Trim();

            var subject = "Continue authentication";
            var message = $"To continue to your account, please <a href='TODO'>click here</a>. <br/>" +
                $"Alternatively, use this code: {token}.";

            await _emailSender.SendEmailAsync(email, subject, message);
        }

        /// Maps additionalUserInfo properties to user
        /// user.UserName = additionalUserInfo.UserName;
        protected abstract void Map(TAdditionalUserInfoViewModel additionalUserInfo, ref TUser user);
    }
}
