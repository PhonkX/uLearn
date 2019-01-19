﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Configuration;
using System.Web.Mvc;
using LtiLibrary.Core.Extensions;
using Ulearn.Common.Extensions;
using Ulearn.Core;
using Ulearn.Core.Configuration;
using Web.Api.Configuration;

namespace uLearn.Web.Controllers
{
	public class SpaController : Controller
	{
		private static string IndexHtmlPath => WebConfigurationManager.AppSettings["ulearn.react.index.html"];
		private static DirectoryInfo AppDirectory => new DirectoryInfo(Utils.GetAppPath());
		
		private readonly List<string> excludedPrefixes;
		private readonly byte[] content;
		
		public SpaController()
			:this(excludedPrefixes: new List<string>
			{
				"/elmah/",
				"/Certificate/",
				"/Analytics/ExportCourseStatisticsAs",
				"/Exercise/StudentZip",
				"/Content/"
			})
		{}
		
		public SpaController(List<string> excludedPrefixes)
		{
			this.excludedPrefixes = excludedPrefixes;
			content = GetSpaIndexHtml();
		}

		public static byte[] GetSpaIndexHtml()
		{
			var file = AppDirectory.GetFile(IndexHtmlPath);
			var content = System.IO.File.ReadAllBytes(file.FullName);
			return InsertFrontendConfiguration(content);
		}
		
		private static byte[] InsertFrontendConfiguration(byte[] content)
		{
			var configuration = ApplicationConfiguration.Read<WebApiConfiguration>();
			var frontendConfigJson = configuration.Frontend.ToJsonString();
			var decodedContent = Encoding.UTF8.GetString(content);
			var regex = new Regex(@"(window.config\s*=\s*)(\{\})");
			var contentWithConfig = regex.Replace(decodedContent, "$1" + frontendConfigJson);

			return Encoding.UTF8.GetBytes(contentWithConfig);
		}
		
		public ActionResult IndexHtml()
		{
			var httpContext = HttpContext;
			
			foreach (var prefix in excludedPrefixes)
				if (httpContext.Request.Url != null && httpContext.Request.Url.LocalPath.StartsWith(prefix))
					return RedirectToAction("Error404", "Errors");
			
			var acceptHeader = httpContext.Request.Headers["Accept"] ?? "";
			var cspHeader = WebConfigurationManager.AppSettings["ulearn.web.cspHeader"] ?? "";
			if (acceptHeader.Contains("text/html") && httpContext.Request.HttpMethod == "GET")
			{
				httpContext.Response.Headers.Add("Content-Security-Policy-Report-Only", cspHeader);
				return new FileContentResult(content, "text/html");
			}

			return RedirectToAction("Error404", "Errors");
		}
	}
}