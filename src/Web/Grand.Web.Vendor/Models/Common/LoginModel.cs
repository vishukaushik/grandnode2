﻿using Grand.Infrastructure.ModelBinding;
using Grand.Infrastructure.Models;
using System.ComponentModel.DataAnnotations;

namespace Grand.Web.Vendor.Models.Common
{
    public class LoginModel : BaseModel
    {
        public LoginModel()
        {
            Captcha = new CaptchaModel();
        }
        [DataType(DataType.EmailAddress)]
        [GrandResourceDisplayName("Account.Login.Fields.Email")]
        public string Email { get; set; }

        public bool UsernamesEnabled { get; set; }
        [GrandResourceDisplayName("Account.Login.Fields.UserName")]
        public string Username { get; set; }

        [DataType(DataType.Password)]
        [GrandResourceDisplayName("Account.Login.Fields.Password")]
        public string Password { get; set; }

        [GrandResourceDisplayName("Account.Login.Fields.RememberMe")]
        public bool RememberMe { get; set; }

        public bool DisplayCaptcha { get; set; }
        
        public ICaptchaValidModel Captcha { get; set; }
    }
}