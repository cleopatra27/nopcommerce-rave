using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
namespace Nop.Plugin.Payments.Rave.Models
{
    public class ConfigurationModel : BaseNopModel
    {

        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.SecretKey")]
        public string SecretKey { get; set; }
        public bool SecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.PublicKey")]
        public string Publickey { get; set; }
        public bool Publickey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Rave.Fields.EncryptKey")]
        public string EncryptKey { get; set; }
        public bool EncryptKey_OverrideForStore { get; set; }

        //[NopResourceDisplayName("Plugins.Payments.Rave.Fields.Redirect_url")]
        //public string Redirect_url { get; set; }
        //public bool Redirect_url_OverrideForStore { get; set; }

        #endregion
    }
}
