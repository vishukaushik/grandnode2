﻿using Grand.Web.Common.Controllers;
using Grand.Web.Common.Filters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Grand.Web.Vendor.Extensions;

namespace Grand.Web.Vendor.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Area(Constants.AreaVendor)]
    [AuthorizeVendor]
    [AuthorizeMenu]
    public abstract class BaseVendorController : BaseController
    {
        /// <summary>
        /// Save selected TAB index
        /// </summary>
        /// <param name="index">Index to save; null to automatically detect it</param>
        /// <param name="persistForTheNextRequest">A value indicating whether a message should be persisted for the next request</param>
        protected async Task SaveSelectedTabIndex(int? index = null, bool persistForTheNextRequest = true)
        {
            if (!index.HasValue)
            {
                var form = await HttpContext.Request.ReadFormAsync();
                var tabindex = form["selected-tab-index"];
                if (tabindex.Count > 0)
                {
                    if (int.TryParse(tabindex[0], out var tmp))
                    {
                        index = tmp;
                    }
                }
                else
                    index = 1;
            }

            if (index.HasValue)
            {
                var dataKey = "Grand.selected-tab-index";
                if (persistForTheNextRequest)
                {
                    TempData[dataKey] = index;
                }
                else
                {
                    ViewData[dataKey] = index;
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="T:System.Web.Mvc.JsonResult"/> object that serializes the specified object to JavaScript Object Notation (JSON) format using the content type, content encoding, and the JSON request behavior.
        /// </summary>
        /// 
        /// <returns>
        /// The result object that serializes the specified object to JSON format.
        /// </returns>
        /// <param name="data">The JavaScript object graph to serialize.</param>
        public override JsonResult Json(object data)
        {
            var serializerSettings = new JsonSerializerSettings {
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
            return base.Json(data, serializerSettings);
        }
    }
}