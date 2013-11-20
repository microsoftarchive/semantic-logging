// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using SlabReconfigurationWebRole.Models;

namespace SlabReconfigurationWebRole.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Success()
        {
            return View();
        }

        [HttpPost]
        public ActionResult SendMessage(MessageModel messageModel)
        {
            if (!this.ModelState.IsValid)
            {
                return View("Index");
            }

            try
            {
                MvcApplication.MessageSender.SendMessage(messageModel.Recipient, messageModel.Message);
            }
            catch (InvalidOperationException e)
            {
                return View("Error", (object)e.Message);
            }

            return RedirectToAction("Success");
        }

    }
}
