﻿using System.Threading.Tasks;
using Bonsai.Areas.Front.Logic;
using Bonsai.Areas.Front.Logic.Auth;
using Bonsai.Code.Mvc;
using Bonsai.Code.Utils;
using Bonsai.Code.Utils.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bonsai.Areas.Front.Controllers
{
    /// <summary>
    /// The root controller for pages.
    /// </summary>
    [Area("Front")]
    [Route("p")]
    [Authorize(Policy = AuthRequirement.Name)]
    public class PageController: AppControllerBase
    {
        public PageController(PagePresenterService pages)
        {
            _pages = pages;
        }

        private readonly PagePresenterService _pages;

        /// <summary>
        /// Displays the page description.
        /// </summary>
        [Route("{key}")]
        public async Task<ActionResult> Description(string key)
        {
            var encKey = PageHelper.EncodeTitle(key);
            if (encKey != key)
                return RedirectToActionPermanent("Description", new {key = encKey});

            try
            {
                var vm = await _pages.GetPageDescriptionAsync(encKey)
                                     .ConfigureAwait(false);
                return View(vm);
            }
            catch (RedirectRequiredException ex)
            {
                return RedirectToActionPermanent("Description", new {key = ex.Key});
            }
        }


        /// <summary>
        /// Displays the related media files.
        /// </summary>
        [Route("{key}/media")]
        public async Task<ActionResult> Media(string key)
        {
            var encKey = PageHelper.EncodeTitle(key);
            if (encKey != key)
                return RedirectToActionPermanent("Media", new { key = encKey });

            try
            {
                var vm = await _pages.GetPageMediaAsync(encKey)
                                     .ConfigureAwait(false);
                return View(vm);
            }
            catch(RedirectRequiredException ex)
            {
                return RedirectToActionPermanent("Description", new { key = ex.Key });
            }
        }
    }
}
