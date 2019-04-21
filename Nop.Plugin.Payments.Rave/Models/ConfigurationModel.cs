using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
namespace Nop.Plugin.Payments.Rave.Models
{
    public class ConfigurationModel
    {
        #region Ctor

        public ConfigurationModel()
        {
        }

        #endregion

        #region Properties

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.SecretKey")]
        public string SecretKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.PublicKey")]
        public string Publickey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.EncryptKey")]
        public string EncryptKey { get; set; }

        #endregion
    }
}
